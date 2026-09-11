using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using LocalSync.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace LocalSync.Core.Tests.Services;

public class TransferManagerTests
{
    private sealed class RecordingSink : ITransferEventSink
    {
        private readonly Lock _gate = new();

        public List<TransferSession> Created { get; } = [];

        public List<(Guid Id, TransferState From, TransferState To)> Transitions { get; } = [];

        public ValueTask TransferCreatedAsync(TransferSession session, CancellationToken ct = default)
        {
            lock (_gate)
            {
                Created.Add(session);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask StateChangedAsync(
            Guid transferId, TransferState previous, TransferState current, CancellationToken ct = default)
        {
            lock (_gate)
            {
                Transitions.Add((transferId, previous, current));
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask ProgressBatchAsync(
            IReadOnlyList<TransferProgress> batch, CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask PeerAppearedAsync(Peer peer, CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask PeerDisappearedAsync(Peer peer, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private static (TransferManager Manager, RecordingSink Sink) Build()
    {
        var sink = new RecordingSink();
        return (new TransferManager(sink, new FakeTimeProvider()), sink);
    }

    private static CreateSessionOptions Options(long size = 1000) =>
        new("report.pdf", size, TransferDirection.Receive);

    [Fact]
    public async Task CreateSession_StartsPendingAndNotifies()
    {
        var (manager, sink) = Build();

        var session = await manager.CreateSessionAsync(Options());

        Assert.Equal(TransferState.Pending, session.State);
        Assert.Equal(0, session.TransferredSize);
        Assert.Single(sink.Created);
    }

    [Fact]
    public async Task CreateSession_RejectsNegativeSize() =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Build().Manager.CreateSessionAsync(Options(size: -1)));

    [Fact]
    public async Task Progress_AdvancesToTheHighWaterMark()
    {
        var (manager, _) = Build();
        var session = await manager.CreateSessionAsync(Options());

        await manager.RecordProgressAsync(session.Id, 400);
        Assert.Equal(400, session.TransferredSize);

        await manager.RecordProgressAsync(session.Id, 900);
        Assert.Equal(900, session.TransferredSize);
    }

    [Fact]
    public async Task Progress_IsIdempotentWhenAChunkIsRetried()
    {
        var (manager, _) = Build();
        var session = await manager.CreateSessionAsync(Options());

        // The same chunk delivered three times. Under the previous
        // accumulate-a-delta design this reported 1200 of 1000 bytes and
        // completed the transfer early.
        await manager.RecordProgressAsync(session.Id, 400);
        await manager.RecordProgressAsync(session.Id, 400);
        await manager.RecordProgressAsync(session.Id, 400);

        Assert.Equal(400, session.TransferredSize);
        Assert.Equal(TransferState.Active, session.State);
    }

    [Fact]
    public async Task Progress_NeverGoesBackwardsOnOutOfOrderArrival()
    {
        var (manager, _) = Build();
        var session = await manager.CreateSessionAsync(Options());

        await manager.RecordProgressAsync(session.Id, 900);
        await manager.RecordProgressAsync(session.Id, 200);

        Assert.Equal(900, session.TransferredSize);
    }

    [Fact]
    public async Task Progress_CompletesExactlyOnceWhenTheSizeIsReached()
    {
        var (manager, sink) = Build();
        var session = await manager.CreateSessionAsync(Options());

        await manager.RecordProgressAsync(session.Id, 500);
        await manager.RecordProgressAsync(session.Id, 1000);
        await manager.RecordProgressAsync(session.Id, 1000);

        Assert.Equal(TransferState.Completed, session.State);
        Assert.NotNull(session.CompletedAt);
        Assert.Single(sink.Transitions, t => t.To == TransferState.Completed);
    }

    [Fact]
    public async Task Progress_UnderConcurrencyCompletesOnceAndReportsExactly()
    {
        var (manager, sink) = Build();
        const long Total = 8 * 1024 * 1024;
        const int ChunkSize = 64 * 1024;
        var session = await manager.CreateSessionAsync(Options(Total));

        var offsets = Enumerable.Range(0, (int)(Total / ChunkSize))
            .Select(i => (long)(i + 1) * ChunkSize)
            .ToArray();

        // Every chunk delivered twice, concurrently and out of order: exactly
        // the shape that made the old read-modify-write lose updates.
        await Parallel.ForEachAsync(
            offsets.Concat(offsets).OrderBy(_ => Random.Shared.Next()),
            async (endOffset, ct) => await manager.RecordProgressAsync(session.Id, endOffset, ct));

        Assert.Equal(Total, session.TransferredSize);
        Assert.Equal(TransferState.Completed, session.State);
        Assert.Single(sink.Transitions, t => t.To == TransferState.Completed);
    }

    [Fact]
    public async Task Fail_RecordsTheReasonAndBecomesTerminal()
    {
        var (manager, _) = Build();
        var session = await manager.CreateSessionAsync(Options());

        await manager.FailAsync(session.Id, "disk full");

        Assert.Equal(TransferState.Failed, session.State);
        Assert.Equal("disk full", session.ErrorMessage);
        Assert.True(session.IsTerminal);
    }

    [Fact]
    public async Task Transition_IsANoOpWhenAlreadyInTheTargetState()
    {
        var (manager, _) = Build();
        var session = await manager.CreateSessionAsync(Options());

        Assert.True(await manager.TransitionAsync(session.Id, TransferState.Cancelled));
        Assert.False(await manager.TransitionAsync(session.Id, TransferState.Cancelled));
    }

    [Fact]
    public async Task UnknownSessionsAreIgnoredRatherThanThrowing()
    {
        var (manager, _) = Build();

        await manager.RecordProgressAsync(Guid.NewGuid(), 100);
        await manager.FailAsync(Guid.NewGuid(), "nope");

        Assert.False(await manager.TransitionAsync(Guid.NewGuid(), TransferState.Completed));
        Assert.Null(manager.GetSession(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetSessions_IsNewestFirst()
    {
        var clock = new FakeTimeProvider();
        var manager = new TransferManager(new RecordingSink(), clock);

        var first = await manager.CreateSessionAsync(Options());
        clock.Advance(TimeSpan.FromSeconds(1));
        var second = await manager.CreateSessionAsync(Options());

        var sessions = manager.GetSessions();

        Assert.Equal(second.Id, sessions[0].Id);
        Assert.Equal(first.Id, sessions[1].Id);
    }
}
