using System.Net.Sockets;

namespace LocalSync.Cli;

/// <summary>Locates the daemon's local control endpoint.</summary>
public static class ControlSocket
{
    private const string SocketFileName = "localsync.sock";

    /// <summary>Named pipe on Windows, Unix domain socket elsewhere.</summary>
    public static string DefaultPath
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return "localsync";
            }

            // XDG_RUNTIME_DIR is per-user and cleared on logout, which is
            // exactly the lifetime a control socket should have. Not every
            // system defines it, hence the temp fallback.
            var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (!string.IsNullOrEmpty(runtime))
            {
                return Path.Combine(runtime, SocketFileName);
            }

            return Path.Combine(
                Path.GetTempPath(),
                $"localsync-{Environment.UserName}",
                SocketFileName);
        }
    }

    public static bool Exists(string path) =>
        OperatingSystem.IsWindows()
            ? Directory.Exists(@"\\.\pipe\") && File.Exists($@"\\.\pipe\{path}")
            : File.Exists(path);

    /// <summary>
    /// A handler that dials the control socket instead of a TCP port.
    /// </summary>
    /// <remarks>
    /// The URI still carries a host so <c>HttpClient</c> can build a request
    /// line, but no TCP connection is ever made: the connect callback replaces
    /// the transport entirely.
    /// </remarks>
    public static SocketsHttpHandler CreateHandler(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return new SocketsHttpHandler
            {
                ConnectCallback = async (_, ct) =>
                {
                    var pipe = new System.IO.Pipes.NamedPipeClientStream(
                        ".", path, System.IO.Pipes.PipeDirection.InOut,
                        System.IO.Pipes.PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(ct).ConfigureAwait(false);
                    return pipe;
                },
            };
        }

        return new SocketsHttpHandler
        {
            ConnectCallback = async (_, ct) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), ct).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            },
        };
    }
}
