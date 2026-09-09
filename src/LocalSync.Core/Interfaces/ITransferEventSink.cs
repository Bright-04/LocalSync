using LocalSync.Core.Models;

namespace LocalSync.Core.Interfaces;

/// <summary>A progress snapshot for one transfer.</summary>
public readonly record struct TransferProgress(
    Guid TransferId,
    TransferState State,
    long TransferredSize,
    long TotalSize);

/// <summary>
/// Receives transfer lifecycle events for onward delivery to clients.
/// </summary>
/// <remarks>
/// There is deliberately no per-chunk progress method. The previous design
/// broadcast on every chunk, which at 1 MiB chunks and line rate is thousands
/// of serialisations per second for a UI that repaints 60 times a second.
/// Progress is published as a coalesced batch on a timer instead, and leaving
/// the per-chunk method out of the contract is what stops that regressing.
/// </remarks>
public interface ITransferEventSink
{
    ValueTask TransferCreatedAsync(TransferSession session, CancellationToken ct = default);

    ValueTask StateChangedAsync(
        Guid transferId, TransferState previous, TransferState current, CancellationToken ct = default);

    ValueTask ProgressBatchAsync(
        IReadOnlyList<TransferProgress> batch, CancellationToken ct = default);

    ValueTask PeerAppearedAsync(Peer peer, CancellationToken ct = default);

    ValueTask PeerDisappearedAsync(Peer peer, CancellationToken ct = default);
}
