using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using LocalSync.Core.Services;
using LocalSync.Host.Services;
using Microsoft.Extensions.Time.Testing;

namespace LocalSync.Core.Tests.Services;

public class TransferProgressBroadcasterTests
{
    private sealed class NullSink : ITransferEventSink
    {
        public ValueTask TransferCreatedAsync(TransferSession s, CancellationToken ct = default) => default;

        public ValueTask StateChangedAsync(
            Guid id, TransferState previous, TransferState current, CancellationToken ct = default) => default;

        public ValueTask ProgressBatchAsync(
            IReadOnlyList<TransferProgress> batch, CancellationToken ct = default) => default;

        public ValueTask PeerAppearedAsync(Peer peer, CancellationToken ct = default) => default;

        public ValueTask PeerDisappearedAsync(Peer peer, CancellationToken ct = default) => default;
    }

    private static (TransferManager Manager, TransferProgressBroadcaster Broadcaster) Build()
    {
        var clock = new FakeTimeProvider();
        var sink = new NullSink();
        var manager = new TransferManager(sink, clock);
        return (manager, new TransferProgressBroadcaster(manager, sink, clock));
    }

    [Fact]
    public async Task PublishesOnlyWhenTheByteCountMoves()
    {
        var (manager, broadcaster) = Build();
        var session = await manager.CreateSessionAsync(
            new CreateSessionOptions("f.bin", 1000, TransferDirection.Receive));

        Assert.Single(broadcaster.CollectChanged());
        Assert.Empty(broadcaster.CollectChanged());

        await manager.RecordProgressAsync(session.Id, 500);
        Assert.Single(broadcaster.CollectChanged());
        Assert.Empty(broadcaster.CollectChanged());
    }

    [Fact]
    public async Task CoalescesManyChunksIntoOneTick()
    {
        var (manager, broadcaster) = Build();
        var session = await manager.CreateSessionAsync(
            new CreateSessionOptions("f.bin", 100_000, TransferDirection.Receive));

        broadcaster.CollectChanged();

        // 50 chunks between ticks must produce one update, not fifty. The old
        // per-chunk broadcast is what this design exists to prevent.
        for (var i = 1; i <= 50; i++)
        {
            await manager.RecordProgressAsync(session.Id, i * 1000);
        }

        var batch = broadcaster.CollectChanged();

        Assert.Single(batch);
        Assert.Equal(50_000, batch[0].TransferredSize);
    }

    [Fact]
    public async Task ATerminalTransferIsPublishedOnceAndThenStaysQuiet()
    {
        var (manager, broadcaster) = Build();
        var session = await manager.CreateSessionAsync(
            new CreateSessionOptions("f.bin", 1000, TransferDirection.Receive));

        broadcaster.CollectChanged();
        await manager.RecordProgressAsync(session.Id, 1000);
        Assert.Equal(TransferState.Completed, session.State);

        Assert.Single(broadcaster.CollectChanged());

        // Regression: dropping the last-published entry on completion made the
        // next tick see no previous value and republish, every 250ms forever.
        for (var tick = 0; tick < 20; tick++)
        {
            Assert.Empty(broadcaster.CollectChanged());
        }
    }

    [Fact]
    public async Task AFailedTransferAlsoStopsRepublishing()
    {
        var (manager, broadcaster) = Build();
        var session = await manager.CreateSessionAsync(
            new CreateSessionOptions("f.bin", 1000, TransferDirection.Receive));

        broadcaster.CollectChanged();
        await manager.RecordProgressAsync(session.Id, 400);
        broadcaster.CollectChanged();
        await manager.FailAsync(session.Id, "nope");

        broadcaster.CollectChanged();

        for (var tick = 0; tick < 10; tick++)
        {
            Assert.Empty(broadcaster.CollectChanged());
        }
    }
}
