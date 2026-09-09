using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using LocalSync.Core.Security;
using LocalSync.Infrastructure.Logging;
using LocalSync.Protocol.Discovery;
using Microsoft.Extensions.Logging;

namespace LocalSync.Infrastructure.Discovery;

/// <summary>
/// UDP multicast discovery, serving the native and LocalSend-compatible
/// protocols from a single socket.
/// </summary>
/// <remarks>
/// Replaces the previous mDNS/DNS-SD implementation. LocalSend discovery is
/// plain UDP with a JSON payload, so a DNS library bought nothing while adding
/// a trimming risk and a second discovery stack to keep working.
/// </remarks>
public sealed class MulticastResponder : IAsyncDisposable
{
    private readonly DiscoveryOptions _options;
    private readonly IPeerRegistry _registry;
    private readonly ILogger<MulticastResponder> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly DeviceId _selfId;

    private readonly List<Socket> _sendSockets = [];
    private readonly CancellationTokenSource _stopping = new();
    private readonly IPEndPoint _groupEndpoint;
    private Socket? _receiveSocket;
    private Socket? _unicastSocket;
    private Task? _receiveLoop;
    private Task? _announceLoop;

    public MulticastResponder(
        DeviceId selfId,
        DiscoveryOptions options,
        IPeerRegistry registry,
        TimeProvider timeProvider,
        ILogger<MulticastResponder> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _selfId = selfId;
        _options = options;
        _registry = registry;
        _timeProvider = timeProvider;
        _logger = logger;
        _groupEndpoint = new IPEndPoint(options.MulticastGroup, options.MulticastPort);
    }

    public void Start()
    {
        BindSockets();

        if (_receiveSocket is null || _sendSockets.Count == 0)
        {
            _logger.DiscoveryInactive();
            return;
        }

        _receiveLoop = Task.Run(() => ReceiveOnSocketAsync(_receiveSocket, _stopping.Token));
        _announceLoop = Task.Run(() => AnnounceLoopAsync(_stopping.Token));

        _logger.DiscoveryStarted(_options.MulticastGroup, _options.MulticastPort, _sendSockets.Count);
    }

    private void BindSockets()
    {
        var interfaces = GetLocalAddresses();

        // The receive socket MUST bind to INADDR_ANY. Binding it to a specific
        // unicast address compiles, runs, and silently receives no multicast at
        // all on Unix, because delivery is matched against the wildcard bind.
        try
        {
            var receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            receiver.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            EnableReusePort(receiver);
            receiver.Bind(new IPEndPoint(IPAddress.Any, _options.MulticastPort));

            foreach (var address in interfaces)
            {
                try
                {
                    receiver.SetSocketOption(
                        SocketOptionLevel.IP,
                        SocketOptionName.AddMembership,
                        new MulticastOption(_options.MulticastGroup, address));
                }
                catch (SocketException ex)
                {
                    _logger.DiscoveryInterfaceSkipped(ex, address);
                }
            }

            _receiveSocket = receiver;

            // A wildcard-bound socket for unicast replies, so the kernel picks
            // the route. Replying from a per-interface socket fails with
            // EADDRNOTAVAIL or EHOSTUNREACH whenever the peer is not on that
            // socket's subnet - loopback peers being the obvious case.
            _unicastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _unicastSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
        }
        catch (SocketException ex)
        {
            _logger.DiscoveryOperationFailed(ex, "bind");
            return;
        }

        // Sending needs one socket per interface so the outbound interface is
        // explicit; a wildcard socket would emit only on the default route.
        foreach (var address in interfaces)
        {
            Socket? sender = null;
            try
            {
                sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                EnableReusePort(sender);
                sender.Bind(new IPEndPoint(address, 0));
                sender.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
                sender.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.MulticastLoopback,
                    _options.MulticastLoopback);
                sender.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.MulticastInterface,
                    address.GetAddressBytes());

                _sendSockets.Add(sender);
            }
            catch (SocketException ex)
            {
                // A down VPN adapter or a duplicate bind must not stop the rest.
                _logger.DiscoveryInterfaceSkipped(ex, address);
                sender?.Dispose();
            }
        }
    }

    /// <summary>
    /// SO_REUSEPORT, which BSD-derived stacks require before a second socket
    /// may share a bound multicast port. .NET exposes no enum for it, and the
    /// constants differ per platform.
    /// </summary>
    private static void EnableReusePort(Socket socket)
    {
        const int SolSocketMacOs = 0xffff;
        const int SoReusePortMacOs = 0x0200;
        const int SolSocketLinux = 1;
        const int SoReusePortLinux = 15;

        try
        {
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
            {
                socket.SetRawSocketOption(SolSocketMacOs, SoReusePortMacOs, BitConverter.GetBytes(1));
            }
            else if (OperatingSystem.IsLinux())
            {
                socket.SetRawSocketOption(SolSocketLinux, SoReusePortLinux, BitConverter.GetBytes(1));
            }
        }
        catch (SocketException)
        {
            // Best effort: a kernel without SO_REUSEPORT still works for a
            // single daemon, which is the normal deployment.
        }
    }

    private static List<IPAddress> GetLocalAddresses()
    {
        var addresses = new List<IPAddress>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || !nic.SupportsMulticast)
            {
                continue;
            }

            if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            foreach (var info in nic.GetIPProperties().UnicastAddresses)
            {
                if (info.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    addresses.Add(info.Address);
                }
            }
        }

        if (!addresses.Contains(IPAddress.Loopback))
        {
            addresses.Add(IPAddress.Loopback);
        }

        return addresses;
    }

    private async Task AnnounceLoopAsync(CancellationToken ct)
    {
        // An immediate announcement means a freshly started peer is visible
        // within one round trip rather than after a full interval.
        await SendAnnouncementAsync(announce: true, ct).ConfigureAwait(false);

        using var timer = new PeriodicTimer(_options.AnnounceInterval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await SendAnnouncementAsync(announce: true, ct).ConfigureAwait(false);
                _registry.EvictExpired();
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    private async Task SendAnnouncementAsync(bool announce, CancellationToken ct)
    {
        var payload = AnnouncementCodec.Encode(BuildAnnouncement(announce));

        foreach (var socket in _sendSockets)
        {
            try
            {
                await socket.SendToAsync(payload, SocketFlags.None, _groupEndpoint, ct)
                    .ConfigureAwait(false);
            }
            catch (SocketException ex)
            {
                _logger.DiscoveryOperationFailed(ex, "announce");
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    internal Announcement BuildAnnouncement(bool announce) => new()
    {
        Alias = _options.Alias,
        Version = "2.2",
        DeviceModel = _options.DeviceModel,
        DeviceType = _options.DeviceType,
        Fingerprint = _selfId.ToString(),
        Port = _options.CompatPort,
        Protocol = "http",
        Download = false,
        Announce = announce,
        LocalSync = new LocalSyncExtension
        {
            Version = 1,
            Id = _selfId.ToString(),
            Port = _options.NativePort,
            Capabilities = ["send"],
        },
    };

    private async Task ReceiveOnSocketAsync(Socket socket, CancellationToken ct)
    {
        var buffer = new byte[_options.MaxDatagramBytes];
        var from = new IPEndPoint(IPAddress.Any, 0);

        while (!ct.IsCancellationRequested)
        {
            SocketReceiveFromResult result;
            try
            {
                result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, from, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException ex)
            {
                _logger.DiscoveryOperationFailed(ex, "receive");
                continue;
            }

            if (result.ReceivedBytes == 0 || result.RemoteEndPoint is not IPEndPoint sender)
            {
                continue;
            }

            HandleDatagram(buffer.AsSpan(0, result.ReceivedBytes), sender);
        }
    }

    private void HandleDatagram(ReadOnlySpan<byte> datagram, IPEndPoint sender)
    {
        var parsed = AnnouncementCodec.TryDecode(datagram, out var announcement) && announcement is not null;
        var peer = parsed
            ? AnnouncementCodec.ToPeer(announcement!, sender.Address.ToString(), _timeProvider.GetUtcNow())
            : null;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.DatagramReceived(
                datagram.Length,
                sender.ToString(),
                parsed,
                peer?.Id.ToString() ?? "-",
                peer?.Id == _selfId);
        }

        if (peer is null)
        {
            return;
        }

        // Identity, not port, decides what is ours. Comparing ports would make
        // every peer running on the default port look like this daemon.
        if (peer.Id == _selfId)
        {
            return;
        }

        if (_registry.Observe(peer))
        {
            _logger.PeerDiscovered(peer.Alias, peer.Endpoint, peer.Protocol);
        }

        // Reply directly so the sender learns about us without waiting for our
        // next interval. Only to announcements, never to replies: two peers
        // answering each other's answers never stops.
        if (announcement is { Announce: true })
        {
            _ = RespondAsync(new IPEndPoint(sender.Address, _options.MulticastPort));
        }
    }

    /// <summary>
    /// Unicasts our details straight back, so a peer learns about us without
    /// waiting for our next interval.
    /// </summary>
    /// <remarks>
    /// Addressed to the peer's discovery port, never to the source port of the
    /// datagram: announcements go out from an ephemeral send socket that
    /// nothing reads, so replying to it is silently discarded.
    /// </remarks>
    private async Task RespondAsync(IPEndPoint sender)
    {
        var socket = _unicastSocket;
        if (socket is null)
        {
            return;
        }

        try
        {
            var payload = AnnouncementCodec.Encode(BuildAnnouncement(announce: false));
            await socket.SendToAsync(payload, SocketFlags.None, sender, _stopping.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException or OperationCanceledException)
        {
            _logger.DiscoveryOperationFailed(ex, "reply");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stopping.IsCancellationRequested)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
        }

        _receiveSocket?.Dispose();
        _receiveSocket = null;
        _unicastSocket?.Dispose();
        _unicastSocket = null;

        foreach (var socket in _sendSockets)
        {
            socket.Dispose();
        }

        _sendSockets.Clear();

        foreach (var task in new[] { _receiveLoop, _announceLoop })
        {
            if (task is null)
            {
                continue;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        }

        _stopping.Dispose();
    }
}
