using LocalSync.Core.Models;

namespace LocalSync.Core.Interfaces;

public interface ITransferManager
{
    Task<TransferSession> CreateSessionAsync(Guid targetDeviceId, string fileName, long totalSize);
    TransferSession? GetSession(Guid sessionId);
    IReadOnlyCollection<TransferSession> GetAllSessions();
    Task UpdateSessionStateAsync(Guid sessionId, TransferState state);
    Task UpdateProgressAsync(Guid sessionId, long bytesTransferred);
}
