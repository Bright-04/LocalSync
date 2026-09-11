using System.Collections.Concurrent;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;

namespace LocalSync.Core.Services;

public sealed class TransferManager : ITransferManager
{
    private readonly ConcurrentDictionary<Guid, TransferSession> _sessions = new();
    private readonly ITransferEventSink _events;
    private readonly TimeProvider _timeProvider;

    public TransferManager(ITransferEventSink events, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _events = events;
        _timeProvider = timeProvider;
    }

    public async Task<TransferSession> CreateSessionAsync(
        CreateSessionOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(options.TotalSize);

        var session = new TransferSession
        {
            Id = Guid.NewGuid(),
            FileName = options.FileName,
            TotalSize = options.TotalSize,
            Direction = options.Direction,
            PeerId = options.PeerId,
            Sha256 = options.Sha256,
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        _sessions[session.Id] = session;
        await _events.TransferCreatedAsync(session, ct).ConfigureAwait(false);
        return session;
    }

    public TransferSession? GetSession(Guid sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? session : null;

    public IReadOnlyList<TransferSession> GetSessions() =>
        _sessions.Values.OrderByDescending(s => s.CreatedAt).ToList();

    public async Task RecordProgressAsync(Guid sessionId, long endOffset, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        session.AdvanceTo(endOffset);

        if (session.State == TransferState.Pending)
        {
            session.TryTransition(TransferState.Pending, TransferState.Active);
        }

        // Completion is decided by the high-water mark reaching the declared
        // size, and the transition is a compare-and-exchange, so exactly one
        // caller finalises no matter how many chunks land at once.
        if (session.TransferredSize >= session.TotalSize
            && session.TryTransition(TransferState.Active, TransferState.Completed))
        {
            session.CompletedAt = _timeProvider.GetUtcNow();
            await _events
                .StateChangedAsync(sessionId, TransferState.Active, TransferState.Completed, ct)
                .ConfigureAwait(false);
        }
    }

    public async Task<bool> TransitionAsync(
        Guid sessionId, TransferState target, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }

        var previous = session.ForceState(target);
        if (previous == target)
        {
            return false;
        }

        if (target is TransferState.Completed or TransferState.Failed or TransferState.Cancelled)
        {
            session.CompletedAt = _timeProvider.GetUtcNow();
        }

        await _events.StateChangedAsync(sessionId, previous, target, ct).ConfigureAwait(false);
        return true;
    }

    public async Task FailAsync(Guid sessionId, string reason, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.ErrorMessage = reason;
        }

        await TransitionAsync(sessionId, TransferState.Failed, ct).ConfigureAwait(false);
    }
}
