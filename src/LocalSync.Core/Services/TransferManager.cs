using System.Collections.Concurrent;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;

namespace LocalSync.Core.Services;

public class TransferManager : ITransferManager
{
    private readonly ConcurrentDictionary<Guid, TransferSession> _sessions = new();
    private readonly ITransferNotificationService _notificationService;

    public TransferManager(ITransferNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<TransferSession> CreateSessionAsync(Guid targetDeviceId, string fileName, long totalSize)
    {
        var session = new TransferSession
        {
            Id = Guid.NewGuid(),
            TargetDeviceId = targetDeviceId,
            FileName = fileName,
            TotalSize = totalSize,
            TransferredSize = 0,
            State = TransferState.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _sessions.TryAdd(session.Id, session);
        
        await _notificationService.NotifySessionCreatedAsync(session);
        return session;
    }

    public TransferSession? GetSession(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public IReadOnlyCollection<TransferSession> GetAllSessions()
    {
        return _sessions.Values.OrderByDescending(s => s.CreatedAt).ToList();
    }

    public async Task UpdateSessionStateAsync(Guid sessionId, TransferState state)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.State = state;
            if (state == TransferState.Completed || state == TransferState.Failed || state == TransferState.Cancelled)
            {
                session.CompletedAt = DateTime.UtcNow;
            }
            await _notificationService.NotifyStateChangedAsync(sessionId, state);
        }
    }

    public async Task UpdateProgressAsync(Guid sessionId, long bytesTransferred)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.TransferredSize += bytesTransferred;
            await _notificationService.NotifyProgressAsync(sessionId, session.TransferredSize);
            
            if (session.TransferredSize >= session.TotalSize)
            {
                session.State = TransferState.Completed;
                session.CompletedAt = DateTime.UtcNow;
                await _notificationService.NotifyStateChangedAsync(sessionId, TransferState.Completed);
            }
        }
    }
}
