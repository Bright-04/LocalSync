namespace LocalSync.Core.Interfaces;

public interface ITransferClient
{
    Task SendFileAsync(Guid targetDeviceId, string filePath, CancellationToken ct = default);
}
