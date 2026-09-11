using System.Reflection;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Services;
using LocalSync.Host.Endpoints;
using LocalSync.Host.Services;
using LocalSync.Infrastructure.Discovery;
using LocalSync.Infrastructure.Networking;
using LocalSync.Infrastructure.Security;
using LocalSync.Infrastructure.Storage;
using LocalSync.Protocol;
using LocalSync.Protocol.Transfer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalSync.Host;

public sealed class DaemonOptions
{
    public int HttpPort { get; set; } = 53318;

    public int DiscoveryPort { get; set; } = DiscoveryOptions.DefaultPort;

    public string? Alias { get; set; }

    public string? ReceiveDirectory { get; set; }

    /// <summary>Bind to loopback only. The default until pairing exists.</summary>
    public bool LoopbackOnly { get; set; }

    /// <summary>
    /// Path of the Unix domain socket or named pipe carrying the CLI's
    /// requests. Null disables it.
    /// </summary>
    public string? ControlSocketPath { get; set; }

    public Action<IEndpointRouteBuilderAdapter>? MapAdditionalEndpoints { get; set; }
}

/// <summary>Lets a host add routes without taking an ASP.NET dependency.</summary>
public interface IEndpointRouteBuilderAdapter
{
    Microsoft.AspNetCore.Routing.IEndpointRouteBuilder Routes { get; }
}

internal sealed record EndpointRouteBuilderAdapter(
    Microsoft.AspNetCore.Routing.IEndpointRouteBuilder Routes) : IEndpointRouteBuilderAdapter;

public static class LocalSyncHost
{
    public static WebApplication Build(DaemonOptions options, string[] args)
    {
        ArgumentNullException.ThrowIfNull(options);

        // CreateSlimBuilder, not CreateBuilder: it drops the hosting startup
        // discovery, regex route constraints, and other reflection-dependent
        // machinery that NativeAOT cannot support.
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Logging.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        });

        // Source-generated resolvers only. Reflection-based serialisation is
        // annotated RequiresDynamicCode and fails under AOT.
        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, LocalSyncJsonContext.Default));

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            if (options.LoopbackOnly)
            {
                kestrel.ListenLocalhost(options.HttpPort);
            }
            else
            {
                kestrel.ListenAnyIP(options.HttpPort);
            }

            // The CLI talks over a socket the filesystem protects, so local
            // control needs no bearer token and no open TCP port. On Unix the
            // socket is created 0600; on Windows the named pipe ACL does the
            // same job.
            if (options.ControlSocketPath is { Length: > 0 } controlPath)
            {
                if (OperatingSystem.IsWindows())
                {
                    kestrel.ListenNamedPipe(Path.GetFileName(controlPath));
                }
                else
                {
                    if (File.Exists(controlPath))
                    {
                        // A socket left behind by a crashed daemon would make
                        // bind fail with EADDRINUSE.
                        File.Delete(controlPath);
                    }

                    // sockaddr_un.sun_path is a fixed 104-byte field on
                    // macOS and 108 on Linux. Exceeding it otherwise surfaces
                    // as an opaque ArgumentOutOfRangeException from deep inside
                    // Kestrel's constructor.
                    const int MaxUnixSocketPath = 104;
                    if (controlPath.Length > MaxUnixSocketPath)
                    {
                        throw new InvalidOperationException(
                            $"Control socket path is {controlPath.Length} characters; the platform " +
                            $"limit is {MaxUnixSocketPath}. Pass a shorter --control-socket, or set " +
                            "XDG_RUNTIME_DIR to a shorter directory.");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(controlPath)!);
                    kestrel.ListenUnixSocket(controlPath);
                }
            }

            kestrel.Limits.MaxRequestBodySize = TransferEndpoints.MaxChunkBytes * 2L;
        });

        RegisterServices(builder.Services, options);

        var app = builder.Build();

        if (options.ControlSocketPath is { Length: > 0 } socketPath && !OperatingSystem.IsWindows())
        {
            app.Lifetime.ApplicationStarted.Register(() => RestrictSocketPermissions(socketPath));
            app.Lifetime.ApplicationStopped.Register(() =>
            {
                try
                {
                    File.Delete(socketPath);
                }
                catch (IOException)
                {
                    // Best effort; a stale socket is cleaned up on next start.
                }
            });
        }

        app.MapPeerEndpoints();
        app.MapTransferEndpoints();
        app.MapEventEndpoints();
        app.MapGet("/health", () => Microsoft.AspNetCore.Http.Results.Ok(new HealthResponse()));

        options.MapAdditionalEndpoints?.Invoke(new EndpointRouteBuilderAdapter(app));

        return app;
    }

    /// <summary>
    /// Narrows the socket to owner-only, since Kestrel creates it with the
    /// process umask and a group-writable socket would let another local user
    /// drive this daemon.
    /// </summary>
    private static void RestrictSocketPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch (IOException)
        {
            // Non-fatal: the daemon still works, the socket is just broader
            // than intended.
        }
    }

    private static void RegisterServices(IServiceCollection services, DaemonOptions options)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAppPaths>(_ => new AppPaths(options.ReceiveDirectory));
        services.AddSingleton<DeviceIdentityStore>();

        services.AddSingleton(sp =>
        {
            var identity = sp.GetRequiredService<DeviceIdentityStore>().LoadOrCreate();
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
            return new DaemonIdentity(identity, options.Alias ?? Environment.MachineName, version);
        });

        services.AddSingleton<ReceivedFilePublisher>();
        services.AddSingleton<ServerSentEventPublisher>();
        services.AddSingleton<ITransferEventSink>(sp => sp.GetRequiredService<ServerSentEventPublisher>());

        services.AddSingleton<IPeerRegistry>(sp =>
            new PeerRegistry(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ITransferManager, TransferManager>();

        services.AddSingleton(sp => new DiscoveryOptions
        {
            MulticastPort = options.DiscoveryPort,
            CompatPort = options.DiscoveryPort,
            NativePort = options.HttpPort,
            Alias = sp.GetRequiredService<DaemonIdentity>().Alias,
        });

        services.AddSingleton(sp => new MulticastResponder(
            sp.GetRequiredService<DaemonIdentity>().Id,
            sp.GetRequiredService<DiscoveryOptions>(),
            sp.GetRequiredService<IPeerRegistry>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<MulticastResponder>>()));

        services.AddHttpClient<IOutboundTransferService, HttpTransferTransport>();

        services.AddHostedService<DiscoveryBackgroundService>();
        services.AddHostedService<TransferProgressBroadcaster>();
    }
}
