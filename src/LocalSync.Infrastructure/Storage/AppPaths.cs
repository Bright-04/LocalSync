using LocalSync.Core.Interfaces;

namespace LocalSync.Infrastructure.Storage;

/// <summary>Desktop per-OS locations for LocalSync's durable state.</summary>
public sealed class AppPaths : IAppPaths
{
    private const string AppFolder = "LocalSync";

    public AppPaths(string? receiveRootOverride = null)
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            ConfigDirectory = Path.Combine(appData, AppFolder);
            StateDirectory = Path.Combine(localAppData, AppFolder);
        }
        else if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var support = Path.Combine(home, "Library", "Application Support", AppFolder);
            ConfigDirectory = support;
            StateDirectory = support;
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            ConfigDirectory = Path.Combine(
                Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(home, ".config"),
                "localsync");
            // XDG state, not cache: this survives reboots and is not disposable.
            StateDirectory = Path.Combine(
                Environment.GetEnvironmentVariable("XDG_STATE_HOME") ?? Path.Combine(home, ".local", "state"),
                "localsync");
        }

        ReceiveDirectory = receiveRootOverride ?? DefaultReceiveDirectory();

        // Inside the receive root on purpose: a cross-filesystem File.Move
        // degrades from an atomic rename into a full copy, which would destroy
        // both the atomicity and the speed of publishing a finished file.
        StagingDirectory = Path.Combine(ReceiveDirectory, ".localsync-incomplete");
    }

    public string ConfigDirectory { get; }

    public string StateDirectory { get; }

    public string ReceiveDirectory { get; }

    public string StagingDirectory { get; }

    private static string DefaultReceiveDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(home, "Downloads");

        return Directory.Exists(downloads)
            ? Path.Combine(downloads, AppFolder)
            : Path.Combine(home, AppFolder);
    }

    public void EnsureCreated()
    {
        CreateOwnerOnly(ConfigDirectory);
        CreateOwnerOnly(StateDirectory);
        Directory.CreateDirectory(ReceiveDirectory);
        CreateOwnerOnly(StagingDirectory);
    }

    private static void CreateOwnerOnly(string path)
    {
        Directory.CreateDirectory(path);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
