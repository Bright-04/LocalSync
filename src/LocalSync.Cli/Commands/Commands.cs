using System.CommandLine;
using LocalSync.Host;

namespace LocalSync.Cli.Commands;

internal static class SharedOptions
{
    internal const int DefaultPort = 53318;

    internal static Option<int> Port() =>
        new("--port", "-p") { Description = "Daemon HTTP port", DefaultValueFactory = _ => DefaultPort };
}

internal static class DaemonCommand
{
    internal static Command Create()
    {
        var port = SharedOptions.Port();
        var discoveryPort = new Option<int>("--discovery-port")
        {
            Description = "UDP multicast discovery port",
            DefaultValueFactory = _ => 53317,
        };
        var alias = new Option<string?>("--alias") { Description = "Name shown to other devices" };
        var receiveDir = new Option<string?>("--receive-dir") { Description = "Where received files are written" };
        var loopback = new Option<bool>("--loopback-only")
        {
            Description = "Bind HTTP to localhost only",
        };

        var command = new Command("daemon", "Run the LocalSync daemon")
        {
            port, discoveryPort, alias, receiveDir, loopback,
        };

        command.SetAction(async (result, ct) =>
        {
            var app = LocalSyncHost.Build(
                new DaemonOptions
                {
                    HttpPort = result.GetValue(port),
                    DiscoveryPort = result.GetValue(discoveryPort),
                    Alias = result.GetValue(alias),
                    ReceiveDirectory = result.GetValue(receiveDir),
                    LoopbackOnly = result.GetValue(loopback),
                },
                []);

            await app.RunAsync(ct).ConfigureAwait(false);
            return 0;
        });

        return command;
    }
}

internal static class PeersCommand
{
    internal static Command Create()
    {
        var port = SharedOptions.Port();
        var command = new Command("peers", "List devices discovered on the local network") { port };

        command.SetAction(async (result, ct) =>
        {
            using var client = new DaemonClient(result.GetValue(port));
            var peers = await client.GetPeersAsync(ct).ConfigureAwait(false);

            if (peers.Length == 0)
            {
                Console.WriteLine("No peers found.");
                return 0;
            }

            foreach (var peer in peers)
            {
                var trust = peer.Protocol == "LocalSync" ? "verified-pending" : "COMPAT";
                Console.WriteLine($"{peer.Alias,-24} {peer.Address}:{peer.Port,-6} [{trust}]");
                Console.WriteLine($"  id       {peer.DisplayId}");
                Console.WriteLine($"  mnemonic {peer.Mnemonic}");
            }

            return 0;
        });

        return command;
    }
}

internal static class SendCommand
{
    internal static Command Create()
    {
        var file = new Argument<string>("file") { Description = "Path to the file to send" };
        var peer = new Option<string>("--to", "-t") { Description = "Target device id", Required = true };
        var port = SharedOptions.Port();

        var command = new Command("send", "Send a file to a discovered peer") { file, peer, port };

        command.SetAction(async (result, ct) =>
        {
            var path = result.GetValue(file)!;
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"File not found: {path}");
                return 2;
            }

            using var client = new DaemonClient(result.GetValue(port));
            var (ok, detail) = await client.SendAsync(result.GetValue(peer)!, path, ct).ConfigureAwait(false);

            Console.WriteLine(detail);
            return ok ? 0 : 1;
        });

        return command;
    }
}

internal static class InfoCommand
{
    internal static Command Create()
    {
        var port = SharedOptions.Port();
        var command = new Command("info", "Show this device's identity") { port };

        command.SetAction(async (result, ct) =>
        {
            using var client = new DaemonClient(result.GetValue(port));
            var info = await client.GetInfoAsync(ct).ConfigureAwait(false);

            if (info is null)
            {
                Console.Error.WriteLine("Daemon did not respond.");
                return 1;
            }

            Console.WriteLine($"Alias      {info.Alias}");
            Console.WriteLine($"Device id  {info.DisplayId}");
            Console.WriteLine($"Mnemonic   {info.Mnemonic}");
            Console.WriteLine($"Receiving  {info.ReceiveDirectory}");
            return 0;
        });

        return command;
    }
}
