using LocalSync.Core.Security;

namespace LocalSync.Core.Tests.Security;

public class SafeFileNameTests
{
    [Theory]
    // Relative traversal, both separator flavours. The backslash cases are the
    // ones Path.GetFileName does not catch on Unix, where '\' is a legal
    // filename character.
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\Windows\\System32\\config\\SAM")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("subdir/file.txt")]
    [InlineData("subdir\\file.txt")]
    // Absolute paths: Path.Combine with an absolute second argument silently
    // discards the root entirely.
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("\\\\server\\share\\file.txt")]
    // NTFS alternate data stream
    [InlineData("report.pdf:evil")]
    // Windows reserved device names, with and without an extension
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("LPT1.dat")]
    [InlineData("aux")]
    // Trailing dot/space, which Windows silently strips into a collision
    [InlineData("evil.txt.")]
    [InlineData("evil.txt ")]
    // Right-to-left override renders "photo\u202Egnp.exe" as "photoexe.png"
    [InlineData("photo\u202Egnp.exe")]
    [InlineData("a\u200Eb.txt")]
    // Control characters, including NUL
    [InlineData("bad\0name.txt")]
    [InlineData("bad\nname.txt")]
    // Empty and whitespace
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryValidate_RejectsUnsafeNames(string? candidate)
    {
        var ok = SafeFileName.TryValidate(candidate, out var safeName, out var rejection);

        Assert.False(ok);
        Assert.Equal(string.Empty, safeName);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
    }

    [Theory]
    [InlineData("report.pdf")]
    [InlineData("holiday photo.jpeg")]
    [InlineData("archive.tar.gz")]
    [InlineData("no-extension")]
    [InlineData("Ünïcödé filename.txt")]
    [InlineData("日本語.txt")]
    [InlineData("console.log")]
    [InlineData("contact.csv")]
    public void TryValidate_AcceptsOrdinaryNames(string candidate)
    {
        var ok = SafeFileName.TryValidate(candidate, out var safeName, out var rejection);

        Assert.True(ok, rejection);
        Assert.Equal(candidate, safeName);
        Assert.Null(rejection);
    }

    [Fact]
    public void TryValidate_RejectsNamesOver255Bytes()
    {
        var tooLong = new string('a', 256);

        Assert.False(SafeFileName.TryValidate(tooLong, out _, out _));
        Assert.True(SafeFileName.TryValidate(new string('a', 255), out _, out _));
    }

    [Fact]
    public void TryValidate_CountsBytesNotChars()
    {
        // Each of these is 3 UTF-8 bytes, so 90 chars is 270 bytes.
        var multibyte = string.Concat(Enumerable.Repeat("あ", 90));

        Assert.False(SafeFileName.TryValidate(multibyte, out _, out _));
    }

    [Fact]
    public void TryResolveWithin_ProducesPathUnderRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "localsync-test-root");

        var ok = SafeFileName.TryResolveWithin(root, "report.pdf", out var fullPath);

        Assert.True(ok);
        Assert.StartsWith(Path.GetFullPath(root), fullPath, StringComparison.Ordinal);
        Assert.Equal("report.pdf", Path.GetFileName(fullPath));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("..\\escape.txt")]
    [InlineData("/etc/passwd")]
    public void TryResolveWithin_RefusesToEscapeRoot(string candidate)
    {
        var root = Path.Combine(Path.GetTempPath(), "localsync-test-root");

        var ok = SafeFileName.TryResolveWithin(root, candidate, out var fullPath);

        Assert.False(ok);
        Assert.Equal(string.Empty, fullPath);
    }

    [Fact]
    public void TryResolveWithin_HandlesRootWithTrailingSeparator()
    {
        var root = Path.Combine(Path.GetTempPath(), "localsync-test-root") + Path.DirectorySeparatorChar;

        Assert.True(SafeFileName.TryResolveWithin(root, "report.pdf", out var fullPath));
        Assert.Equal("report.pdf", Path.GetFileName(fullPath));
    }

    /// <summary>Joins segments with the platform separator so these cases hold
    /// on Windows as well as Unix.</summary>
    private static string P(params string[] segments) =>
        string.Join(Path.DirectorySeparatorChar, segments);

    [Theory]
    // A naive StartsWith(root) without a trailing separator accepts every one
    // of these. Directory transfers validate multi-segment paths, so this is
    // reachable there even though TryValidate blocks it for leaf names.
    [InlineData("localsync", "localsync-evil", "x.txt")]
    [InlineData("localsync", "localsync.backup", "x.txt")]
    [InlineData("data", "database", "dump.sql")]
    public void IsWithinRoot_RejectsSiblingSharingANamePrefix(
        string rootLeaf, string siblingLeaf, string file)
    {
        var root = P("tmp", rootLeaf);
        var candidate = P("tmp", siblingLeaf, file);

        Assert.False(SafeFileName.IsWithinRoot(root, candidate));
    }

    [Fact]
    public void IsWithinRoot_RejectsSiblingWithNoSeparatorAtAll()
    {
        Assert.False(SafeFileName.IsWithinRoot(P("tmp", "localsync"), P("tmp", "localsyncsecrets")));
    }

    [Fact]
    public void IsWithinRoot_AcceptsPathsInsideRoot()
    {
        var root = P("tmp", "localsync");

        Assert.True(SafeFileName.IsWithinRoot(root, P("tmp", "localsync", "x.txt")));
        Assert.True(SafeFileName.IsWithinRoot(root, P("tmp", "localsync", "nested", "deep", "x.txt")));
        Assert.True(SafeFileName.IsWithinRoot(
            root + Path.DirectorySeparatorChar, P("tmp", "localsync", "x.txt")));
    }

    [Fact]
    public void IsWithinRoot_RejectsRootItself()
    {
        // The root directory is not a valid destination for a file.
        var root = P("tmp", "localsync");

        Assert.False(SafeFileName.IsWithinRoot(root, root));
    }
}
