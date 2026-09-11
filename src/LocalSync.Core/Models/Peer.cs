using LocalSync.Core.Security;

namespace LocalSync.Core.Models;

/// <summary>How a peer was discovered, and therefore how far it can be trusted.</summary>
public enum PeerProtocol
{
    /// <summary>Native LocalSync peer.</summary>
    LocalSync,

    /// <summary>LocalSend v2.2 compatibility peer. Identity is unverifiable.</summary>
    LocalSendCompat,
}

/// <summary>
/// A device seen on the local network.
/// </summary>
/// <remarks>
/// Everything here except <see cref="Id"/> arrives over unauthenticated
/// multicast and is attacker-controlled. <see cref="Alias"/> in particular must
/// never be rendered without its trust indicator alongside it.
/// </remarks>
public sealed record Peer
{
    public required DeviceId Id { get; init; }

    /// <summary>Remote-supplied display name. Untrusted.</summary>
    public required string Alias { get; init; }

    public required string Address { get; init; }

    public required int Port { get; init; }

    public PeerProtocol Protocol { get; init; } = PeerProtocol.LocalSync;

    public string? DeviceModel { get; init; }

    public string DeviceType { get; init; } = "desktop";

    public required DateTimeOffset FirstSeen { get; init; }

    public required DateTimeOffset LastSeen { get; init; }

    public string Endpoint => $"{Address}:{Port}";
}
