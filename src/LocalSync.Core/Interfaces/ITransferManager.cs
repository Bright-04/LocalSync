using LocalSync.Core.Models;

namespace LocalSync.Core.Interfaces;

public sealed record CreateSessionOptions(
    string FileName,
    long TotalSize,
    TransferDirection Direction,
    string? PeerId = null,
    string? Sha256 = null);

public interface ITransferManager
{
    Task<TransferSession> CreateSessionAsync(CreateSessionOptions options, CancellationToken ct = default);

    TransferSession? GetSession(Guid sessionId);

    IReadOnlyList<TransferSession> GetSessions();

    /// <summary>
    /// Records that bytes up to <paramref name="endOffset"/> are on disk, and
    /// completes the transfer once that reaches the declared size.
    /// </summary>
    /// <remarks>
    /// Takes an absolute end offset rather than a delta so that a retried or
    /// duplicated chunk is idempotent instead of double-counted.
    /// </remarks>
    Task RecordProgressAsync(Guid sessionId, long endOffset, CancellationToken ct = default);

    Task<bool> TransitionAsync(Guid sessionId, TransferState target, CancellationToken ct = default);

    Task FailAsync(Guid sessionId, string reason, CancellationToken ct = default);
}
