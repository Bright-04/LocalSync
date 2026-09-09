using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using LocalSync.Core.Security;
using LocalSync.Host.Services;
using LocalSync.Protocol.Transfer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalSync.Host.Endpoints;

public static class TransferEndpoints
{
    /// <summary>Largest single chunk accepted, matching the sender's framing.</summary>
    public const int MaxChunkBytes = 4 * 1024 * 1024;

    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/localsync/v1");

        group.MapPost("/session", CreateSessionAsync);
        group.MapPut("/session/{sessionId:guid}/chunk", UploadChunkAsync)
            .DisableAntiforgery();
        group.MapGet("/transfers", GetTransfers);
        group.MapGet("/transfers/{sessionId:guid}", GetTransfer);

        return routes;
    }

    private static async Task<IResult> CreateSessionAsync(
        CreateSessionRequest request,
        ITransferManager transfers,
        IAppPaths paths,
        CancellationToken ct)
    {
        // A peer-supplied name is validated before it is allowed near a path,
        // and rejected rather than rewritten so the sender learns why.
        if (!SafeFileName.TryValidate(request.FileName, out var safeName, out var rejection))
        {
            return Results.BadRequest(new ErrorResponse { Error = "invalid_file_name", Message = rejection });
        }

        if (request.TotalSize < 0)
        {
            return Results.BadRequest(new ErrorResponse
            {
                Error = "invalid_total_size",
                Message = "TotalSize must not be negative.",
            });
        }

        paths.EnsureCreated();

        var session = await transfers.CreateSessionAsync(
            new CreateSessionOptions(
                safeName,
                request.TotalSize,
                TransferDirection.Receive,
                request.TargetDeviceId,
                request.Sha256),
            ct).ConfigureAwait(false);

        // Staging uses the server-generated session id as the file name, so a
        // peer-supplied string never reaches a write path at all. The real name
        // is applied only when the completed file is published.
        session.StagingPath = Path.Combine(paths.StagingDirectory, $"{session.Id:N}.part");

        return Results.Ok(new CreateSessionResponse
        {
            Id = session.Id.ToString(),
            FileName = session.FileName,
            TotalSize = session.TotalSize,
            State = session.State.ToString(),
        });
    }

    private static async Task<IResult> UploadChunkAsync(
        Guid sessionId,
        long offset,
        HttpRequest request,
        ITransferManager transfers,
        ReceivedFilePublisher publisher,
        CancellationToken ct)
    {
        var session = transfers.GetSession(sessionId);
        if (session is null)
        {
            return Results.NotFound(new ErrorResponse { Error = "unknown_session" });
        }

        if (session.IsTerminal)
        {
            return Results.Conflict(new ErrorResponse { Error = "session_closed" });
        }

        var length = request.ContentLength ?? -1;
        if (length < 0)
        {
            return Results.BadRequest(new ErrorResponse { Error = "content_length_required" });
        }

        if (length > MaxChunkBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        // An unvalidated offset lets one request create a file of arbitrary
        // size: offset = 2^40 produces a 1 TiB sparse file, unauthenticated.
        if (offset < 0 || offset > session.TotalSize || offset + length > session.TotalSize)
        {
            return Results.BadRequest(new ErrorResponse
            {
                Error = "offset_out_of_bounds",
                Message = $"Offset {offset} + {length} exceeds declared size {session.TotalSize}.",
            });
        }

        if (string.IsNullOrEmpty(session.StagingPath))
        {
            return Results.Problem("Session has no staging path.");
        }

        try
        {
            using var handle = File.OpenHandle(
                session.StagingPath,
                FileMode.OpenOrCreate,
                FileAccess.Write,
                FileShare.ReadWrite,
                FileOptions.Asynchronous);

            var buffer = new byte[length];
            var read = 0;
            while (read < length)
            {
                var got = await request.Body
                    .ReadAsync(buffer.AsMemory(read, (int)(length - read)), ct)
                    .ConfigureAwait(false);
                if (got == 0)
                {
                    break;
                }

                read += got;
            }

            // Positional and stateless, so concurrent writers at distinct
            // offsets need no lock. This is what makes parallel block upload
            // possible in Phase 2b without reopening the file per chunk.
            await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, read), offset, ct)
                .ConfigureAwait(false);

            await transfers.RecordProgressAsync(sessionId, offset + read, ct).ConfigureAwait(false);

            if (session.State == TransferState.Completed)
            {
                var published = await publisher.PublishAsync(session, ct).ConfigureAwait(false);
                if (published.Ok)
                {
                    session.StagingPath = published.Path;
                }
                else
                {
                    await transfers.FailAsync(sessionId, published.Error ?? "Publish failed.", ct)
                        .ConfigureAwait(false);
                    return Results.Problem(published.Error ?? "Publish failed.");
                }
            }

            return Results.Ok(new ChunkAcceptedResponse { TransferredSize = session.TransferredSize });
        }
        catch (IOException ex)
        {
            await transfers.FailAsync(sessionId, ex.Message, ct).ConfigureAwait(false);
            return Results.Problem("Failed to write chunk.");
        }
    }

    private static IResult GetTransfers(ITransferManager transfers) =>
        Results.Ok(transfers.GetSessions().Select(ServerSentEventPublisher.ToView).ToArray());

    private static IResult GetTransfer(Guid sessionId, ITransferManager transfers)
    {
        var session = transfers.GetSession(sessionId);
        return session is null
            ? Results.NotFound(new ErrorResponse { Error = "unknown_session" })
            : Results.Ok(ServerSentEventPublisher.ToView(session));
    }
}
