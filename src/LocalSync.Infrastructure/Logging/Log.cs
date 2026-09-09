using System.Net;
using LocalSync.Core.Models;
using Microsoft.Extensions.Logging;

namespace LocalSync.Infrastructure.Logging;

/// <summary>
/// Source-generated log messages.
/// </summary>
/// <remarks>
/// The generator emits a cached delegate and a level check per message, so
/// arguments are never boxed or formatted when the level is disabled. It is
/// also the only logging shape that is fully trim- and AOT-safe.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Discovery listening on {Group}:{Port} across {InterfaceCount} interface(s)")]
    internal static partial void DiscoveryStarted(
        this ILogger logger, IPAddress group, int port, int interfaceCount);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "No multicast-capable network interface found; discovery is inactive")]
    internal static partial void DiscoveryInactive(this ILogger logger);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "Skipping interface {Address} for discovery")]
    internal static partial void DiscoveryInterfaceSkipped(
        this ILogger logger, Exception exception, IPAddress address);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Debug,
        Message = "Discovery {Operation} failed")]
    internal static partial void DiscoveryOperationFailed(
        this ILogger logger, Exception exception, string operation);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Debug,
        Message = "Datagram {Bytes}B from {Source}: parsed={Parsed} id={PeerId} self={IsSelf}")]
    internal static partial void DatagramReceived(
        this ILogger logger, int bytes, string source, bool parsed, string peerId, bool isSelf);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Discovered {Alias} at {Endpoint} via {Protocol}")]
    internal static partial void PeerDiscovered(
        this ILogger logger, string alias, string endpoint, PeerProtocol protocol);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Peer {Alias} at {Endpoint} timed out")]
    internal static partial void PeerExpired(this ILogger logger, string alias, string endpoint);

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Sent {FileName} ({Bytes} bytes) to {Alias}")]
    internal static partial void FileSent(
        this ILogger logger, string fileName, long bytes, string alias);

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "Generated device identity {DeviceId}")]
    internal static partial void IdentityGenerated(this ILogger logger, string deviceId);
}
