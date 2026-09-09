using System.Security.Cryptography;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using LocalSync.Core.Security;

namespace LocalSync.Host.Services;

public sealed record PublishResult(bool Ok, string? Path, string? Error);

/// <summary>
/// Moves a completed transfer from staging into the receive directory.
/// </summary>
/// <remarks>
/// The peer-supplied name is applied here and nowhere earlier: bytes are always
/// written to a server-generated ordinal name, so a hostile name never reaches
/// a write path even if validation were bypassed.
/// </remarks>
public sealed class ReceivedFilePublisher
{
    private const int MaxCollisionAttempts = 999;

    private readonly IAppPaths _paths;

    public ReceivedFilePublisher(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public async Task<PublishResult> PublishAsync(TransferSession session, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrEmpty(session.StagingPath) || !File.Exists(session.StagingPath))
        {
            return new PublishResult(false, null, "Staging file is missing.");
        }

        if (!SafeFileName.TryValidate(session.FileName, out var safeName, out var rejection))
        {
            return new PublishResult(false, null, rejection);
        }

        if (session.Sha256 is { Length: > 0 } expected)
        {
            var actual = await ComputeSha256Async(session.StagingPath, ct).ConfigureAwait(false);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                // Never publish a file that failed verification. Leave the
                // partial in staging so a later resume can reuse it.
                return new PublishResult(false, null, "Checksum mismatch.");
            }
        }

        Directory.CreateDirectory(_paths.ReceiveDirectory);

        if (!SafeFileName.TryResolveWithin(_paths.ReceiveDirectory, safeName, out var destination))
        {
            return new PublishResult(false, null, "Resolved path escapes the receive directory.");
        }

        try
        {
            var final = MoveWithoutOverwriting(session.StagingPath, destination);
            return new PublishResult(true, final, null);
        }
        catch (IOException ex)
        {
            return new PublishResult(false, null, ex.Message);
        }
    }

    /// <summary>
    /// Renames into place, adding " (2)", " (3)" and so on when the name is taken.
    /// </summary>
    /// <remarks>
    /// Driven by catching the failure rather than checking File.Exists first:
    /// the check-then-move version is a time-of-check race against any other
    /// process writing to the same directory.
    /// </remarks>
    private static string MoveWithoutOverwriting(string source, string destination)
    {
        var directory = Path.GetDirectoryName(destination)!;
        var stem = Path.GetFileNameWithoutExtension(destination);
        var extension = Path.GetExtension(destination);

        for (var attempt = 1; attempt <= MaxCollisionAttempts; attempt++)
        {
            var candidate = attempt == 1
                ? destination
                : Path.Combine(directory, $"{stem} ({attempt}){extension}");

            try
            {
                File.Move(source, candidate, overwrite: false);
                return candidate;
            }
            catch (IOException) when (File.Exists(candidate))
            {
                // Taken between our attempt and now; try the next number.
            }
        }

        throw new IOException($"Could not find a free name for '{destination}'.");
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
        await using var scope = stream.ConfigureAwait(false);

        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false));
    }
}
