using System.CommandLine;
using LocalSync.Cli.Commands;

var root = new RootCommand("LocalSync - LAN-first peer-to-peer file transfer")
{
    DaemonCommand.Create(),
    PeersCommand.Create(),
    SendCommand.Create(),
    InfoCommand.Create(),
};

return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
