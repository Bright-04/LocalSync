using System.Text;
using LocalSync.Host.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalSync.Host.Endpoints;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/localsync/v1/events", StreamAsync);
        return routes;
    }

    /// <summary>
    /// Server-Sent Events stream of transfer and peer activity.
    /// </summary>
    /// <remarks>
    /// Consumed with the browser's native EventSource, so the client needs no
    /// library. That keeps the zero-install guest page small and removes
    /// SignalR, which is only partially AOT-supported.
    /// </remarks>
    private static async Task StreamAsync(
        HttpContext context, ServerSentEventPublisher stream, CancellationToken ct)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache,no-transform";
        context.Response.Headers.Connection = "keep-alive";
        // Without this an intermediary proxy may buffer the stream and the UI
        // sits silent until the response completes, which for SSE is never.
        context.Response.Headers["X-Accel-Buffering"] = "no";

        var subscriber = stream.Subscribe();

        try
        {
            await context.Response.Body
                .WriteAsync(Encoding.UTF8.GetBytes(": connected\n\n"), ct)
                .ConfigureAwait(false);
            await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);

            await foreach (var payload in subscriber.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await context.Response.Body
                    .WriteAsync(Encoding.UTF8.GetBytes(payload), ct)
                    .ConfigureAwait(false);
                await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The client navigated away; not an error.
        }
        finally
        {
            stream.Unsubscribe(subscriber);
        }
    }
}
