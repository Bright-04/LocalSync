using System.Net.Http.Json;
using LocalSync.Protocol;
using LocalSync.Protocol.Transfer;

namespace LocalSync.Cli;

/// <summary>Talks to a running daemon over its local HTTP surface.</summary>
public sealed class DaemonClient : IDisposable
{
    private readonly HttpClient _http;

    /// <summary>
    /// Connects over the control socket when one exists, otherwise loopback TCP.
    /// </summary>
    /// <remarks>
    /// The socket path is preferred because filesystem permissions authenticate
    /// the caller, so no token and no listening TCP port are involved.
    /// </remarks>
    public DaemonClient(int port, string? socketPath = null)
    {
        var path = socketPath ?? ControlSocket.DefaultPath;

        _http = ControlSocket.Exists(path)
            ? new HttpClient(ControlSocket.CreateHandler(path))
            {
                // The authority is a placeholder: the connect callback has
                // already replaced the transport.
                BaseAddress = new Uri("http://localsync"),
                Timeout = TimeSpan.FromMinutes(30),
            }
            : new HttpClient
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}"),
                Timeout = TimeSpan.FromMinutes(30),
            };
    }

    public async Task<PeerView[]> GetPeersAsync(CancellationToken ct) =>
        await _http.GetFromJsonAsync(
            "/api/localsync/v1/peers", LocalSyncJsonContext.Default.PeerViewArray, ct)
            .ConfigureAwait(false) ?? [];

    public async Task<DaemonInfo?> GetInfoAsync(CancellationToken ct) =>
        await _http.GetFromJsonAsync(
            "/api/localsync/v1/info", LocalSyncJsonContext.Default.DaemonInfo, ct)
            .ConfigureAwait(false);

    public async Task<(bool Ok, string Detail)> SendAsync(
        string peerId, string path, CancellationToken ct)
    {
        var request = new SendFileRequest { PeerId = peerId, Path = path };

        using var response = await _http.PostAsJsonAsync(
                "/api/localsync/v1/send",
                request,
                LocalSyncJsonContext.Default.SendFileRequest,
                ct)
            .ConfigureAwait(false);

        var body = await response.Content
            .ReadFromJsonAsync(LocalSyncJsonContext.Default.ErrorResponse, ct)
            .ConfigureAwait(false);

        return (response.IsSuccessStatusCode, body?.Message ?? body?.Error ?? response.StatusCode.ToString());
    }

    public void Dispose() => _http.Dispose();
}
