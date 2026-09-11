namespace LocalSync.Core.Interfaces;

/// <summary>
/// Per-platform locations for durable state.
/// </summary>
/// <remarks>
/// An interface rather than a static helper because Environment.SpecialFolder
/// is wrong on Android and iOS, where MAUI supplies its own app-private roots.
/// </remarks>
public interface IAppPaths
{
    /// <summary>Configuration and identity material. Created with owner-only permissions.</summary>
    string ConfigDirectory { get; }

    /// <summary>Durable state such as the transfer database.</summary>
    string StateDirectory { get; }

    /// <summary>Where completed transfers are published.</summary>
    string ReceiveDirectory { get; }

    /// <summary>
    /// Partial transfers, deliberately inside <see cref="ReceiveDirectory"/>:
    /// a cross-filesystem move degrades from an atomic rename to a full copy.
    /// </summary>
    string StagingDirectory { get; }

    /// <summary>Ensures the directories exist with appropriate permissions.</summary>
    void EnsureCreated();
}
