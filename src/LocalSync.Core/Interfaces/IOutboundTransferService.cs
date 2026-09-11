using LocalSync.Core.Security;

namespace LocalSync.Core.Interfaces;

public enum SendOutcome
{
    Completed,
    PeerUnknown,
    PeerRejected,
    AlreadyPresent,
    Failed,
}

public sealed record SendResult(SendOutcome Outcome, string? Detail = null, long BytesSent = 0)
{
    public bool Succeeded => Outcome is SendOutcome.Completed or SendOutcome.AlreadyPresent;
}

/// <summary>Sends a local file to a discovered peer.</summary>
public interface IOutboundTransferService
{
    Task<SendResult> SendFileAsync(DeviceId target, string filePath, CancellationToken ct = default);
}
