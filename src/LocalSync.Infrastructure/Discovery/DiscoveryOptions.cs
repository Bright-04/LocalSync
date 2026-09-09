using System.Net;

namespace LocalSync.Infrastructure.Discovery;

/// <summary>Configuration for the multicast discovery responder.</summary>
public sealed class DiscoveryOptions
{
    /// <summary>
    /// LocalSend's multicast group. Inside 224.0.0.0/24, the local network
    /// control block, so TTL is 1 and routers never forward it: discovery is
    /// single-subnet by construction. Cross-subnet peers need a manual address.
    /// </summary>
    public static readonly IPAddress DefaultGroup = IPAddress.Parse("224.0.0.167");

    public const int DefaultPort = 53317;

    public IPAddress MulticastGroup { get; set; } = DefaultGroup;

    public int MulticastPort { get; set; } = DefaultPort;

    /// <summary>The native LocalSync listener's port, advertised under "lsx".</summary>
    public int NativePort { get; set; } = 53318;

    /// <summary>The LocalSend-compatible listener's port.</summary>
    public int CompatPort { get; set; } = DefaultPort;

    public string Alias { get; set; } = Environment.MachineName;

    public string DeviceType { get; set; } = "desktop";

    public string? DeviceModel { get; set; }

    public TimeSpan AnnounceInterval { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan PeerTtl { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Enabled so two daemons on one host can see each other, which is what
    /// makes single-machine testing possible.
    /// </summary>
    public bool MulticastLoopback { get; set; } = true;

    /// <summary>Largest announcement accepted. Anything bigger is not ours.</summary>
    public int MaxDatagramBytes { get; set; } = 8 * 1024;
}
