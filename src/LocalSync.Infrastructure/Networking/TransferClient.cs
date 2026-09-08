// PHASE 1 REPLACEMENT PENDING - suppressions expire with this file.
//
// This file is deleted in Phase 1 (see docs/adr/, docs/architecture.md) and
// replaced by Networking/HttpTransferTransport.cs (HTTP/2, TLS 1.3
// with SPKI pinning, idempotent block PUTs). The diagnostics below are
// real and are fixed BY that replacement, not by editing this file:
//   IL3050 - 'dynamic' requires runtime code generation and cannot be AOT
//   IL2026   compiled. It is also a live runtime bug: ReadFromJsonAsync
//            <dynamic> yields a boxed JsonElement, so `session?.id` binds
//            against a type with no such member and throws. Both are fixed
//            by a source-generated JsonSerializerContext.
//   CA5351 - MD5 whole-file hash; becomes SHA-256 on the wire (which also
//            matches LocalSend v2.2) plus per-block BLAKE3.
//   CA1848 - LoggerMessage delegates; also CA1873 for the same call sites.
//
// Scoped per-file, not per-project, so new code in LocalSync.Infrastructure
// stays strict. Do not copy this block into a new file.
#pragma warning disable IL2026, IL3050, CA5351, CA1848, CA1873

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
            fileHash = Convert.ToHexStringLower(hashBytes);
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
        var sessionRes = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/transfer/session", sessionReq, ct)
            .ConfigureAwait(false);

        if (sessionRes.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogInformation("Target has a newer or identical version of {FileName}. Sync skipped.", fileInfo.Name);
            return;
        }

        sessionRes.EnsureSuccessStatusCode();
        var session = await sessionRes.Content.ReadFromJsonAsync<dynamic>(cancellationToken: ct)
            .ConfigureAwait(false);
        string sessionId = session?.id ?? throw new InvalidOperationException("Invalid session response");

        // 2. Upload Chunks (1MB size)
        const int chunkSize = 1024 * 1024;
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await using var streamScope = stream.ConfigureAwait(false);
        var buffer = new byte[chunkSize];
        long offset = 0;

        while (offset < fileInfo.Length)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, chunkSize), ct).ConfigureAwait(false);
            if (bytesRead == 0) break;

            var content = new MultipartFormDataContent();
            content.Add(new StringContent(sessionId), "sessionId");
            content.Add(new StringContent(offset.ToString()), "offset");
            content.Add(new StringContent((offset + bytesRead >= fileInfo.Length).ToString()), "isLastChunk");

            var chunkContent = new ByteArrayContent(buffer, 0, bytesRead);
            content.Add(chunkContent, "chunk", fileInfo.Name);

            var uploadRes = await _httpClient.PostAsync($"{baseUrl}/api/transfer/chunk", content, ct)
                .ConfigureAwait(false);
            uploadRes.EnsureSuccessStatusCode();

            offset += bytesRead;
        }
    }
}
