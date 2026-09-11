using System.Collections.Concurrent;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using LocalSync.Core.Security;

namespace LocalSync.Core.Services;

/// <summary>
/// In-memory peer table with time-to-live expiry.
/// </summary>
/// <remarks>
/// Bounded on purpose. Announcements are unauthenticated, so an attacker can
/// mint arbitrarily many identities; an unbounded table is a memory-exhaustion
/// vector. When full, the least recently seen peer is evicted.
/// </remarks>
public sealed class PeerRegistry : IPeerRegistry
{
    /// <summary>Roughly three missed announcement intervals.</summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(15);

    public const int DefaultCapacity = 256;

    private readonly ConcurrentDictionary<DeviceId, Peer> _peers = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _ttl;
    private readonly int _capacity;

    public event EventHandler<Peer>? PeerAppeared;

    public event EventHandler<Peer>? PeerDisappeared;

    public PeerRegistry(TimeProvider timeProvider, TimeSpan? ttl = null, int capacity = DefaultCapacity)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _timeProvider = timeProvider;
        _ttl = ttl ?? DefaultTtl;
        _capacity = capacity;
    }

    public IReadOnlyList<Peer> GetPeers() =>
        _peers.Values.OrderBy(p => p.Alias, StringComparer.OrdinalIgnoreCase).ToList();

    public Peer? Find(DeviceId id) => _peers.TryGetValue(id, out var peer) ? peer : null;

    public bool Observe(Peer peer)
    {
        ArgumentNullException.ThrowIfNull(peer);

        var now = _timeProvider.GetUtcNow();
        var isNew = false;

        _peers.AddOrUpdate(
            peer.Id,
            _ =>
            {
                isNew = true;
                return peer with { FirstSeen = now, LastSeen = now };
            },
            (_, existing) => existing with
            {
                Alias = peer.Alias,
                Address = peer.Address,
                Port = peer.Port,
                Protocol = peer.Protocol,
                DeviceModel = peer.DeviceModel,
                DeviceType = peer.DeviceType,
                LastSeen = now,
            });

        if (isNew)
        {
            EnforceCapacity();
            if (_peers.TryGetValue(peer.Id, out var stored))
            {
                PeerAppeared?.Invoke(this, stored);
            }
        }

        return isNew;
    }

    public bool Remove(DeviceId id)
    {
        if (!_peers.TryRemove(id, out var removed))
        {
            return false;
        }

        PeerDisappeared?.Invoke(this, removed);
        return true;
    }

    public IReadOnlyList<Peer> EvictExpired()
    {
        var cutoff = _timeProvider.GetUtcNow() - _ttl;
        var expired = new List<Peer>();

        foreach (var (id, peer) in _peers)
        {
            if (peer.LastSeen > cutoff)
            {
                continue;
            }

            if (_peers.TryRemove(id, out var removed))
            {
                expired.Add(removed);
            }
        }

        foreach (var peer in expired)
        {
            PeerDisappeared?.Invoke(this, peer);
        }

        return expired;
    }

    private void EnforceCapacity()
    {
        while (_peers.Count > _capacity)
        {
            var oldest = _peers.Values.MinBy(p => p.LastSeen);
            if (oldest is null || !_peers.TryRemove(oldest.Id, out var removed))
            {
                return;
            }

            PeerDisappeared?.Invoke(this, removed);
        }
    }
}
