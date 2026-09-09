using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Security;
using LocalSync.Protocol;
using LocalSync.Protocol.Transfer;
using LocalSync.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace LocalSync.Infrastructure.Networking;

/// <summary>
/// Sends a file to a peer over the native HTTP transfer endpoints.
/// </summary>
/// <remarks>
/// Phase 1 is a single sequential stream over plain HTTP. TLS with pinned
/// identities arrives in Phase 2a and block-addressed parallel resume in 2b;
/// the seams that make those possible are the session negotiation and the
/// offset-addressed chunk upload already used here.
/// </remarks>
public sealed class HttpTransferTransport : IOutboundTransferService
{
    /// <summary>Matches the browser uploader so both sides frame identically.</summary>
    public const int ChunkSize = 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly IPeerRegistry _registry;
    private readonly ILogger<HttpTransferTransport> _logger;

    public HttpTransferTransport(
        HttpClient httpClient,
        IPeerRegistry registry,
        ILogger<HttpTransferTransport> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _registry = registry;
        _logger = logger;
    }

    public async Task<SendResult> SendFileAsync(DeviceId target, string filePath, CancellationToken ct = default)
    {
        var peer = _registry.Find(target);
        if (peer is null)
        {
            return new SendResult(SendOutcome.PeerUnknown, $"No peer with id {target}");
        }

        var file = new FileInfo(filePath);
        if (!file.Exists)
        {
            return new SendResult(SendOutcome.Failed, $"File not found: {filePath}");
        }

        var baseUrl = $"http://{peer.Address}:{peer.Port}";
        var sha256 = await ComputeSha256Async(filePath, ct).ConfigureAwait(false);

        var request = new CreateSessionRequest
        {
            FileName = file.Name,
            TotalSize = file.Length,
            TargetDeviceId = target.ToString(),
            LastModified = file.LastWriteTimeUtc,
            Sha256 = sha256,
        };

        using var sessionResponse = await _httpClient.PostAsJsonAsync(
                $"{baseUrl}/api/localsync/v1/session",
                request,
                LocalSyncJsonContext.Default.CreateSessionRequest,
                ct)
            .ConfigureAwait(false);

        if (sessionResponse.StatusCode == HttpStatusCode.NoContent)
        {
            return new SendResult(SendOutcome.AlreadyPresent, "Peer already has this content");
        }

        if (sessionResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Conflict)
        {
            return new SendResult(SendOutcome.PeerRejected, $"Peer responded {(int)sessionResponse.StatusCode}");
        }

        if (!sessionResponse.IsSuccessStatusCode)
        {
            return new SendResult(SendOutcome.Failed, $"Session creation failed: {(int)sessionResponse.StatusCode}");
        }

        // Deserialised through the source-generated context, not <dynamic>.
        // JsonSerializer materialises dynamic as a boxed JsonElement, so member
        // access against it throws at runtime and cannot be AOT compiled.
        var session = await sessionResponse.Content
            .ReadFromJsonAsync(LocalSyncJsonContext.Default.CreateSessionResponse, ct)
            .ConfigureAwait(false);

        if (session is null || string.IsNullOrWhiteSpace(session.Id))
        {
            return new SendResult(SendOutcome.Failed, "Peer returned an unusable session");
        }

        var sent = await UploadChunksAsync(baseUrl, session.Id, filePath, file.Length, ct)
            .ConfigureAwait(false);

        _logger.FileSent(file.Name, sent, peer.Alias);

        return new SendResult(SendOutcome.Completed, BytesSent: sent);
    }

    private async Task<long> UploadChunksAsync(
        string baseUrl,
        string sessionId,
        string filePath,
        long totalLength,
        CancellationToken ct)
    {
        var buffer = new byte[ChunkSize];
        long offset = 0;

        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ChunkSize,
            useAsync: true);
        await using var scope = stream.ConfigureAwait(false);

        while (offset < totalLength)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, ChunkSize), ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            using var content = new ByteArrayContent(buffer, 0, read);
            using var response = await _httpClient
                .PutAsync(
                    $"{baseUrl}/api/localsync/v1/session/{sessionId}/chunk?offset={offset}",
                    content,
                    ct)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            offset += read;
        }

        return offset;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ChunkSize,
            useAsync: true);
        await using var scope = stream.ConfigureAwait(false);

        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }
}
