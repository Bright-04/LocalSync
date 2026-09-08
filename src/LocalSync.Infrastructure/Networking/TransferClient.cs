using System.Net.Http.Json;
using LocalSync.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LocalSync.Infrastructure.Networking;

public class TransferClient : ITransferClient
{
    private readonly HttpClient _httpClient;
    private readonly IDeviceManager _deviceManager;
    private readonly ILogger<TransferClient> _logger;

    public TransferClient(HttpClient httpClient, IDeviceManager deviceManager, ILogger<TransferClient> logger)
    {
        _httpClient = httpClient;
        _deviceManager = deviceManager;
        _logger = logger;
    }

    public async Task SendFileAsync(Guid targetDeviceId, string filePath, CancellationToken ct = default)
    {
        var devices = _deviceManager.GetDevices();
        var targetDevice = devices.FirstOrDefault(d => d.Id == targetDeviceId);

        if (targetDevice == null || !targetDevice.IsOnline)
        {
            _logger.LogWarning("Target device {DeviceId} is offline or unknown. Cannot send file.", targetDeviceId);
            return;
        }

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists) return;

        var baseUrl = $"http://{targetDevice.IpAddress}:{targetDevice.Port}";

        string fileHash = string.Empty;
        using (var md5 = System.Security.Cryptography.MD5.Create())
        using (var fstream = System.IO.File.OpenRead(filePath))
        {
            var hashBytes = md5.ComputeHash(fstream);
            fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        // 1. Create Session
        var sessionReq = new 
        { 
            TargetDeviceId = Guid.Empty, 
            FileName = fileInfo.Name, 
            TotalSize = fileInfo.Length,
            LastModified = fileInfo.LastWriteTimeUtc,
            FileHash = fileHash
        };
        var sessionRes = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/transfer/session", sessionReq, ct);
        
        if (sessionRes.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogInformation("Target has a newer or identical version of {FileName}. Sync skipped.", fileInfo.Name);
            return;
        }
        
        sessionRes.EnsureSuccessStatusCode();
        var session = await sessionRes.Content.ReadFromJsonAsync<dynamic>(cancellationToken: ct);
        string sessionId = session?.id ?? throw new Exception("Invalid session response");

        // 2. Upload Chunks (1MB size)
        const int chunkSize = 1024 * 1024;
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[chunkSize];
        long offset = 0;

        while (offset < fileInfo.Length)
        {
            int bytesRead = await stream.ReadAsync(buffer, 0, chunkSize, ct);
            if (bytesRead == 0) break;

            var content = new MultipartFormDataContent();
            content.Add(new StringContent(sessionId), "sessionId");
            content.Add(new StringContent(offset.ToString()), "offset");
            content.Add(new StringContent((offset + bytesRead >= fileInfo.Length).ToString()), "isLastChunk");

            var chunkContent = new ByteArrayContent(buffer, 0, bytesRead);
            content.Add(chunkContent, "chunk", fileInfo.Name);

            var uploadRes = await _httpClient.PostAsync($"{baseUrl}/api/transfer/chunk", content, ct);
            uploadRes.EnsureSuccessStatusCode();

            offset += bytesRead;
        }
    }
}
