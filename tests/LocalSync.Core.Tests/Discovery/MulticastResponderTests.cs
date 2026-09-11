using System.Net;
using LocalSync.Core.Security;
using LocalSync.Core.Services;
using LocalSync.Infrastructure.Discovery;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalSync.Core.Tests.Discovery;

/// <summary>
/// Exercises the real socket stack. Uses a dedicated multicast group and port
/// so a developer running LocalSync on the same machine cannot interfere.
/// </summary>
[Trait("Category", "Integration")]
public class MulticastResponderTests
{
    private static readonly IPAddress TestGroup = IPAddress.Parse("224.0.0.199");
    private const int TestPort = 53399;

    private static DiscoveryOptions Options(string alias, int nativePort) => new()
    {
        MulticastGroup = TestGroup,
        MulticastPort = TestPort,
        NativePort = nativePort,
        CompatPort = TestPort,
        Alias = alias,
        AnnounceInterval = TimeSpan.FromMilliseconds(250),
        MulticastLoopback = true,
    };

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return condition();
    }

    [Fact]
    public async Task TwoResponders_DiscoverEachOther()
    {
        var idA = DeviceId.CreateRandom();
        var idB = DeviceId.CreateRandom();
        var registryA = new PeerRegistry(TimeProvider.System);
        var registryB = new PeerRegistry(TimeProvider.System);

        await using var responderA = new MulticastResponder(
            idA, Options("peer-a", 60001), registryA, TimeProvider.System,
            NullLogger<MulticastResponder>.Instance);
        await using var responderB = new MulticastResponder(
            idB, Options("peer-b", 60002), registryB, TimeProvider.System,
            NullLogger<MulticastResponder>.Instance);

        responderA.Start();
        responderB.Start();

        var found = await WaitUntilAsync(
            () => registryA.Find(idB) is not null && registryB.Find(idA) is not null,
            TimeSpan.FromSeconds(10));

        Assert.True(found,
            $"A saw {registryA.GetPeers().Count} peer(s), B saw {registryB.GetPeers().Count}. " +
            "If this fails only in CI, multicast is likely blocked on the runner.");

        var seenByA = registryA.Find(idB);
        Assert.NotNull(seenByA);
        Assert.Equal("peer-b", seenByA.Alias);
        Assert.Equal(60002, seenByA.Port);
    }

    [Fact]
    public async Task AResponderDoesNotDiscoverItself()
    {
        var id = DeviceId.CreateRandom();
        var registry = new PeerRegistry(TimeProvider.System);

        await using var responder = new MulticastResponder(
            id, Options("solo", 60003), registry, TimeProvider.System,
            NullLogger<MulticastResponder>.Instance);

        responder.Start();
        await Task.Delay(TimeSpan.FromSeconds(2));

        // The old implementation compared ports, so every peer on the default
        // port looked like self. Identity is what distinguishes us.
        Assert.Null(registry.Find(id));
    }

    [Fact]
    public async Task TwoRespondersOnTheSamePortAreStillDistinguished()
    {
        var idA = DeviceId.CreateRandom();
        var idB = DeviceId.CreateRandom();
        var registryA = new PeerRegistry(TimeProvider.System);
        var registryB = new PeerRegistry(TimeProvider.System);

        // Identical native ports: the normal case for two machines running
        // stock configuration, and precisely what the old port-equality
        // self-check got wrong.
        const int SharedPort = 60010;

        await using var responderA = new MulticastResponder(
            idA, Options("same-port-a", SharedPort), registryA, TimeProvider.System,
            NullLogger<MulticastResponder>.Instance);
        await using var responderB = new MulticastResponder(
            idB, Options("same-port-b", SharedPort), registryB, TimeProvider.System,
            NullLogger<MulticastResponder>.Instance);

        responderA.Start();
        responderB.Start();

        var found = await WaitUntilAsync(
            () => registryA.Find(idB) is not null && registryB.Find(idA) is not null,
            TimeSpan.FromSeconds(10));

        Assert.True(found, "Peers sharing a port must still see each other");
    }

    [Fact]
    public async Task BuildAnnouncement_CarriesBothProtocolsInOnePacket()
    {
        var id = DeviceId.CreateRandom();
        var registry = new PeerRegistry(TimeProvider.System);
        await using var responder = new MulticastResponder(
            id, Options("dual", 53318), registry, TimeProvider.System,
            NullLogger<MulticastResponder>.Instance);

        var built = responder.BuildAnnouncement(announce: true);

        Assert.Equal("2.2", built.Version);
        Assert.Equal(TestPort, built.Port);
        Assert.NotNull(built.LocalSync);
        Assert.Equal(53318, built.LocalSync.Port);
        Assert.Equal(id.ToString(), built.LocalSync.Id);
    }
}
