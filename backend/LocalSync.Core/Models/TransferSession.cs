namespace LocalSync.Core.Models;

public enum TransferState
{
    Pending,
    Active,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public class TransferSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TargetDeviceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long TransferredSize { get; set; }
    public TransferState State { get; set; } = TransferState.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
