using LocalSync.Core.Models;

namespace LocalSync.Core.Interfaces;

public interface ITransferNotificationService
{
    Task NotifySessionCreatedAsync(TransferSession session);
    Task NotifyProgressAsync(Guid sessionId, long transferredSize);
    Task NotifyStateChangedAsync(Guid sessionId, TransferState state);
}
