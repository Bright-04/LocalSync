using LocalSync.Core.Models;
using LocalSync.Core.Security;

namespace LocalSync.Core.Interfaces;

/// <summary>Tracks peers currently visible on the local network.</summary>
public interface IPeerRegistry
{
    IReadOnlyList<Peer> GetPeers();

    Peer? Find(DeviceId id);

    /// <summary>Records a sighting. Returns true if this peer was not previously known.</summary>
    bool Observe(Peer peer);

    bool Remove(DeviceId id);

    /// <summary>Drops peers not seen within the TTL. Returns those removed.</summary>
    IReadOnlyList<Peer> EvictExpired();

    event EventHandler<Peer>? PeerAppeared;

    event EventHandler<Peer>? PeerDisappeared;
}
