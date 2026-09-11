namespace LocalSync.Core.Models;

public enum TransferState
{
    Pending,
    Active,
    Paused,
    Verifying,
    Completed,
    Failed,
    Cancelled,
}

public enum TransferDirection
{
    Send,
    Receive,
}

/// <summary>
/// One in-flight transfer.
/// </summary>
/// <remarks>
/// Progress is stored as the high-water mark of contiguous bytes written, not
/// as a running total. Accumulating deltas made a retried chunk count twice,
/// which inflated the total past the file size and tripped completion early;
/// a high-water mark makes the same chunk arriving twice a no-op.
/// <para>
/// Phase 2b replaces this with a per-block bitmap, which additionally makes
/// out-of-order and parallel arrival exact rather than merely monotonic.
/// </para>
/// </remarks>
public sealed class TransferSession
{
    private long _transferredSize;
    private int _state = (int)TransferState.Pending;

    public required Guid Id { get; init; }

    public required string FileName { get; init; }

    public required long TotalSize { get; init; }

    public required TransferDirection Direction { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public string? PeerId { get; init; }

    public string? Sha256 { get; init; }

    public string? StagingPath { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public long TransferredSize => Interlocked.Read(ref _transferredSize);

    public TransferState State => (TransferState)Volatile.Read(ref _state);

    public bool IsTerminal => State is TransferState.Completed or TransferState.Failed or TransferState.Cancelled;

    /// <summary>
    /// Raises the high-water mark to <paramref name="endOffset"/>.
    /// </summary>
    /// <returns>True if this call advanced it; false if the bytes were already accounted for.</returns>
    public bool AdvanceTo(long endOffset)
    {
        while (true)
        {
            var current = Interlocked.Read(ref _transferredSize);
            if (endOffset <= current)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _transferredSize, endOffset, current) == current)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Moves to <paramref name="next"/> only from <paramref name="expected"/>.
    /// </summary>
    /// <remarks>
    /// Compare-and-exchange so that concurrent chunk completions cannot both
    /// observe "the last byte arrived" and finalise the transfer twice.
    /// </remarks>
    public bool TryTransition(TransferState expected, TransferState next) =>
        Interlocked.CompareExchange(ref _state, (int)next, (int)expected) == (int)expected;

    public TransferState ForceState(TransferState next) =>
        (TransferState)Interlocked.Exchange(ref _state, (int)next);
}
