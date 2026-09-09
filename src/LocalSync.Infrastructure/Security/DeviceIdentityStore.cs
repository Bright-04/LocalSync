using System.Security.Cryptography;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Security;
using LocalSync.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace LocalSync.Infrastructure.Security;

/// <summary>
/// Loads or creates this device's persistent identity.
/// </summary>
/// <remarks>
/// The previous implementation generated a fresh GUID per process, so a device
/// presented a new identity on every restart. Persisting it is what makes
/// pinning possible at all.
/// <para>
/// Phase 2a replaces the stored random bytes with an ECDSA P-256 key pair and
/// derives the id as SHA-256 of its SubjectPublicKeyInfo. The file name and
/// permissions below already anticipate holding key material.
/// </para>
/// </remarks>
public sealed class DeviceIdentityStore
{
    private const string IdentityFileName = "identity.key";

    private readonly IAppPaths _paths;
    private readonly ILogger<DeviceIdentityStore> _logger;

    public DeviceIdentityStore(IAppPaths paths, ILogger<DeviceIdentityStore> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(logger);

        _paths = paths;
        _logger = logger;
    }

    public DeviceId LoadOrCreate()
    {
        _paths.EnsureCreated();
        var path = Path.Combine(_paths.ConfigDirectory, IdentityFileName);

        if (File.Exists(path))
        {
            EnsureNotWorldReadable(path);

            var existing = File.ReadAllBytes(path);
            if (existing.Length == DeviceId.SizeInBytes)
            {
                return DeviceId.FromBytes(existing);
            }

            // Refusing is safer than silently regenerating: a new identity would
            // be presented to every already-paired peer as a key change, which
            // is exactly the signal that should mean "attack".
            throw new InvalidOperationException(
                $"Identity file '{path}' is corrupt ({existing.Length} bytes, expected {DeviceId.SizeInBytes}). " +
                "Delete it to generate a new identity, understanding that peers must re-pair.");
        }

        var material = RandomNumberGenerator.GetBytes(DeviceId.SizeInBytes);
        WriteOwnerOnly(path, material);

        var id = DeviceId.FromBytes(material);

        // Guarded because ToDisplayString allocates and computes check
        // characters, which CA1873 correctly flags as wasted when the level is
        // disabled.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.IdentityGenerated(id.ToDisplayString());
        }

        return id;
    }

    private static void WriteOwnerOnly(string path, byte[] contents)
    {
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllBytes(path, contents);
            return;
        }

        using (var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            }))
        {
            stream.Write(contents);
        }
    }

    /// <summary>
    /// Fails closed on loose permissions, the way OpenSSH does for a private
    /// key, rather than quietly using material another local user can read.
    /// </summary>
    private static void EnsureNotWorldReadable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        const UnixFileMode Forbidden =
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

        if ((mode & Forbidden) != 0)
        {
            throw new InvalidOperationException(
                $"Identity file '{path}' is accessible to other users (mode {mode}). " +
                $"Run: chmod 600 \"{path}\"");
        }
    }
}
