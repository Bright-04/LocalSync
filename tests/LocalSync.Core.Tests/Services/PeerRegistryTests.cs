using LocalSync.Core.Models;
using LocalSync.Core.Security;
using LocalSync.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace LocalSync.Core.Tests.Services;

public class PeerRegistryTests
{
    private static readonly DateTimeOffset Origin = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static FakeTimeProvider Clock() => new(Origin);

    private static Peer MakePeer(DeviceId? id = null, string alias = "Laptop", int port = 53318) => new()
    {
        Id = id ?? DeviceId.CreateRandom(),
        Alias = alias,
        Address = "192.168.1.20",
        Port = port,
        FirstSeen = Origin,
        LastSeen = Origin,
    };

    [Fact]
    public void Observe_AddsAPeerAndReportsItAsNew()
    {
        var registry = new PeerRegistry(Clock());
        var peer = MakePeer();

        Assert.True(registry.Observe(peer));
        Assert.Single(registry.GetPeers());
        Assert.NotNull(registry.Find(peer.Id));
    }

    [Fact]
    public void Observe_IsIdempotentForTheSameIdentity()
    {
        var registry = new PeerRegistry(Clock());
        var peer = MakePeer();

        Assert.True(registry.Observe(peer));
        Assert.False(registry.Observe(peer));
        Assert.Single(registry.GetPeers());
    }

    [Fact]
    public void Observe_RefreshesTheAddressWithoutLosingFirstSeen()
    {
        var clock = Clock();
        var registry = new PeerRegistry(clock);
        var peer = MakePeer();
        registry.Observe(peer);

        clock.Advance(TimeSpan.FromSeconds(5));
        registry.Observe(peer with { Address = "192.168.1.99", Alias = "Renamed" });

        var stored = registry.Find(peer.Id);
        Assert.NotNull(stored);
        Assert.Equal("192.168.1.99", stored.Address);
        Assert.Equal("Renamed", stored.Alias);
        Assert.Equal(Origin, stored.FirstSeen);
        Assert.Equal(Origin.AddSeconds(5), stored.LastSeen);
    }

    [Fact]
    public void Observe_RaisesPeerAppearedOnlyOnce()
    {
        var registry = new PeerRegistry(Clock());
        var peer = MakePeer();
        var appearances = 0;
        registry.PeerAppeared += (_, _) => appearances++;

        registry.Observe(peer);
        registry.Observe(peer);
        registry.Observe(peer);

        Assert.Equal(1, appearances);
    }

    [Fact]
    public void EvictExpired_RemovesPeersPastTheTtl()
    {
        var clock = Clock();
        var registry = new PeerRegistry(clock, TimeSpan.FromSeconds(15));
        var peer = MakePeer();
        registry.Observe(peer);

        clock.Advance(TimeSpan.FromSeconds(14));
        Assert.Empty(registry.EvictExpired());
        Assert.Single(registry.GetPeers());

        clock.Advance(TimeSpan.FromSeconds(2));
        var evicted = registry.EvictExpired();

        Assert.Single(evicted);
        Assert.Empty(registry.GetPeers());
    }

    [Fact]
    public void EvictExpired_SparesPeersThatKeptAnnouncing()
    {
        var clock = Clock();
        var registry = new PeerRegistry(clock, TimeSpan.FromSeconds(15));
        var peer = MakePeer();
        registry.Observe(peer);

        for (var i = 0; i < 10; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            registry.Observe(peer);
            Assert.Empty(registry.EvictExpired());
        }

        Assert.Single(registry.GetPeers());
    }

    [Fact]
    public void EvictExpired_RaisesPeerDisappeared()
    {
        var clock = Clock();
        var registry = new PeerRegistry(clock, TimeSpan.FromSeconds(15));
        var peer = MakePeer();
        registry.Observe(peer);

        var disappeared = new List<Peer>();
        registry.PeerDisappeared += (_, p) => disappeared.Add(p);

        clock.Advance(TimeSpan.FromSeconds(20));
        registry.EvictExpired();

        Assert.Single(disappeared);
        Assert.Equal(peer.Id, disappeared[0].Id);
    }

    [Fact]
    public void Remove_ReportsWhetherThePeerWasPresent()
    {
        var registry = new PeerRegistry(Clock());
        var peer = MakePeer();
        registry.Observe(peer);

        Assert.True(registry.Remove(peer.Id));
        Assert.False(registry.Remove(peer.Id));
    }

    [Fact]
    public void CapacityIsBounded_SoForgedAnnouncementsCannotExhaustMemory()
    {
        var clock = Clock();
        var registry = new PeerRegistry(clock, TimeSpan.FromMinutes(10), capacity: 32);

        for (var i = 0; i < 500; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(10));
            registry.Observe(MakePeer(alias: $"forged-{i}"));
        }

        Assert.Equal(32, registry.GetPeers().Count);
    }

    [Fact]
    public void CapacityEviction_DropsTheLeastRecentlySeen()
    {
        var clock = Clock();
        var registry = new PeerRegistry(clock, TimeSpan.FromMinutes(10), capacity: 2);

        var oldest = MakePeer(alias: "oldest");
        registry.Observe(oldest);
        clock.Advance(TimeSpan.FromSeconds(1));
        var middle = MakePeer(alias: "middle");
        registry.Observe(middle);
        clock.Advance(TimeSpan.FromSeconds(1));
        registry.Observe(MakePeer(alias: "newest"));

        Assert.Null(registry.Find(oldest.Id));
        Assert.NotNull(registry.Find(middle.Id));
        Assert.Equal(2, registry.GetPeers().Count);
    }

    [Fact]
    public void Constructor_RejectsNonsenseArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new PeerRegistry(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeerRegistry(Clock(), capacity: 0));
    }
}
