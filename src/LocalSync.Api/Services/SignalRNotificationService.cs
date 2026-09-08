using LocalSync.Api.Hubs;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace LocalSync.Api.Services;

public class SignalRNotificationService : ITransferNotificationService
{
    private readonly IHubContext<TransferHub> _hubContext;

    public SignalRNotificationService(IHubContext<TransferHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifySessionCreatedAsync(TransferSession session)
    {
        await _hubContext.Clients.All.SendAsync("SessionCreated", session);
    }

    public async Task NotifyProgressAsync(Guid sessionId, long transferredSize)
    {
        await _hubContext.Clients.All.SendAsync("ProgressUpdated", sessionId, transferredSize);
    }

    public async Task NotifyStateChangedAsync(Guid sessionId, TransferState state)
    {
        await _hubContext.Clients.All.SendAsync("StateChanged", sessionId, state);
    }
}
