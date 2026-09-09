using LocalSync.Core.Interfaces;
using LocalSync.Core.Security;
using LocalSync.Host.Services;
using LocalSync.Protocol.Transfer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalSync.Host.Endpoints;

public static class PeerEndpoints
{
    public static IEndpointRouteBuilder MapPeerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/localsync/v1");

        group.MapGet("/peers", GetPeers);
        group.MapGet("/info", GetInfo);
        group.MapPost("/send", SendAsync);

        return routes;
    }

    private static IResult GetPeers(IPeerRegistry registry) =>
        Results.Ok(registry.GetPeers().Select(ServerSentEventPublisher.ToView).ToArray());

    private static IResult GetInfo(DaemonIdentity identity, IAppPaths paths) =>
        Results.Ok(new DaemonInfo
        {
            DeviceId = identity.Id.ToString(),
            DisplayId = identity.Id.ToDisplayString(),
            Mnemonic = identity.Id.ToMnemonic(),
            Alias = identity.Alias,
            Version = identity.Version,
            ReceiveDirectory = paths.ReceiveDirectory,
        });

    private static async Task<IResult> SendAsync(
        SendFileRequest request,
        IOutboundTransferService sender,
        CancellationToken ct)
    {
        if (!DeviceId.TryParse(request.PeerId, out var peerId))
        {
            return Results.BadRequest(new ErrorResponse { Error = "invalid_peer_id" });
        }

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return Results.BadRequest(new ErrorResponse { Error = "path_required" });
        }

        var result = await sender.SendFileAsync(peerId, request.Path, ct).ConfigureAwait(false);

        return result.Outcome switch
        {
            SendOutcome.Completed or SendOutcome.AlreadyPresent =>
                Results.Ok(new ErrorResponse { Error = result.Outcome.ToString(), Message = result.Detail }),
            SendOutcome.PeerUnknown =>
                Results.NotFound(new ErrorResponse { Error = "peer_unknown", Message = result.Detail }),
            SendOutcome.PeerRejected =>
                Results.Conflict(new ErrorResponse { Error = "peer_rejected", Message = result.Detail }),
            _ => Results.Problem(result.Detail ?? "Send failed."),
        };
    }
}

/// <summary>This daemon's own identity, resolved once at startup.</summary>
public sealed record DaemonIdentity(DeviceId Id, string Alias, string Version);
