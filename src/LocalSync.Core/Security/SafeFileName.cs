namespace LocalSync.Core.Security;

/// <summary>
/// Validates a peer-supplied file name before it is allowed anywhere near a
/// filesystem path.
/// </summary>
/// <remarks>
/// Rejects rather than rewrites. A silently rewritten name lands the file
/// somewhere the sender did not ask for, which is harder to diagnose than a
/// refusal and can still collide with an existing file.
/// </remarks>
public static class SafeFileName
{
    private static readonly char[] AlwaysInvalid = ['<', '>', ':', '"', '|', '?', '*'];

    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>
    /// Returns true when <paramref name="candidate"/> is a single, safe path
    /// segment usable as a leaf file name on every supported platform.
    /// </summary>
    public static bool TryValidate(string? candidate, out string safeName, out string? rejection)
    {
        safeName = string.Empty;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            rejection = "File name is empty.";
            return false;
        }

        // Normalise both separators before any leaf extraction. On Unix, '\' is
        // a legal filename character, so Path.GetFileName leaves a
        // Windows-style "..\..\etc\passwd" fully intact.
        if (candidate.Contains('/', StringComparison.Ordinal) ||
            candidate.Contains('\\', StringComparison.Ordinal))
        {
            rejection = "File name must not contain a path separator.";
            return false;
        }

        if (candidate is "." or "..")
        {
            rejection = "File name must not be a relative path segment.";
            return false;
        }

        foreach (var c in candidate)
        {
            if (char.IsControl(c))
            {
                rejection = "File name must not contain control characters.";
                return false;
            }

            if (Array.IndexOf(AlwaysInvalid, c) >= 0)
            {
                rejection = $"File name must not contain '{c}'.";
                return false;
            }

            // Unicode Format (Cf) covers every bidi and zero-width control,
            // including ones an explicit list would miss. Without this,
            // "photo\u202Egnp.exe" renders to a reader as "photoexe.png".
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                == System.Globalization.UnicodeCategory.Format)
            {
                rejection = "File name must not contain bidirectional or zero-width controls.";
                return false;
            }
        }

        // Windows silently strips these, so "evil.txt." and "evil.txt" collide.
        if (candidate.EndsWith('.') || candidate.EndsWith(' '))
        {
            rejection = "File name must not end with a dot or space.";
            return false;
        }

        // Reserved with OR without an extension: "nul.txt" is still NUL.
        var stem = candidate;
        var dot = stem.IndexOf('.', StringComparison.Ordinal);
        if (dot >= 0)
        {
            stem = stem[..dot];
        }

        foreach (var reserved in ReservedDeviceNames)
        {
            if (string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase))
            {
                rejection = $"File name '{stem}' is a reserved device name.";
                return false;
            }
        }

        if (System.Text.Encoding.UTF8.GetByteCount(candidate) > 255)
        {
            rejection = "File name exceeds 255 bytes.";
            return false;
        }

        safeName = candidate;
        rejection = null;
        return true;
    }

    /// <summary>
    /// Resolves <paramref name="leafName"/> under <paramref name="root"/> and
    /// confirms the result is still contained by it.
    /// </summary>
    /// <remarks>
    /// Belt and braces behind <see cref="TryValidate"/>: sanitisers get
    /// bypassed, and Path.Combine with an absolute second argument silently
    /// discards the first.
    /// </remarks>
    public static bool TryResolveWithin(string root, string leafName, out string fullPath)
    {
        fullPath = string.Empty;

        if (!TryValidate(leafName, out var safeName, out _))
        {
            return false;
        }

        var fullRoot = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, safeName));

        if (!IsWithinRoot(fullRoot, candidate))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    /// <summary>
    /// Returns true when <paramref name="candidateFullPath"/> lies strictly
    /// inside <paramref name="root"/>.
    /// </summary>
    /// <remarks>
    /// Public and separately testable because <see cref="TryValidate"/> makes
    /// it unreachable for single leaf names, but directory transfers validate
    /// multi-segment relative paths where escape is genuinely possible.
    /// Both arguments must already be absolute.
    /// </remarks>
    public static bool IsWithinRoot(string root, string candidateFullPath)
    {
        // macOS and Windows are case-insensitive by default, so an Ordinal
        // comparison there could be sidestepped by case variation.
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        // The trailing separator is load-bearing: without it, root
        // "/tmp/localsync" would accept "/tmp/localsync-evil/x".
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return candidateFullPath.StartsWith(prefix, comparison);
    }
}
