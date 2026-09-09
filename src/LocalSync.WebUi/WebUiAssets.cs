using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;

namespace LocalSync.WebUi;

/// <summary>
/// Serves the dashboard from resources embedded in the executable.
/// </summary>
/// <remarks>
/// MapStaticAssets is deliberately not used: it writes an endpoints manifest
/// next to the binary and serves from wwwroot on disk, which would mean the
/// daemon is no longer a single self-contained file. StaticFiles over an
/// embedded provider is fully AOT-supported and keeps everything in one binary.
/// </remarks>
public static class WebUiAssets
{
    // Manifest-based, because a dotted resource name cannot unambiguously
    // represent a nested path once filenames themselves contain dots.
    private static readonly ManifestEmbeddedFileProvider Provider =
        new(Assembly.GetExecutingAssembly(), "wwwroot");

    public static IEndpointRouteBuilder MapWebUi(this IEndpointRouteBuilder routes)
    {
        var app = (IApplicationBuilder)routes;

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = Provider,
            // Vite fingerprints asset filenames, so their content can never
            // change under a given URL.
            OnPrepareResponse = ctx =>
            {
                var path = ctx.File.Name;
                ctx.Context.Response.Headers.CacheControl =
                    path.Equals("index.html", StringComparison.OrdinalIgnoreCase)
                        ? "no-cache"
                        : "public,max-age=31536000,immutable";
            },
        });

        // SPA fallback: anything that is not an API route or a real asset gets
        // index.html so client-side routing works on a hard refresh.
        routes.MapFallback(async context =>
        {
            var index = Provider.GetFileInfo("index.html");
            if (!index.Exists)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(index, context.RequestAborted).ConfigureAwait(false);
        });

        return routes;
    }

    /// <summary>True when a real build is embedded rather than the dev placeholder.</summary>
    public static bool IsEmbedded => Provider.GetFileInfo("index.html").Exists;
}
