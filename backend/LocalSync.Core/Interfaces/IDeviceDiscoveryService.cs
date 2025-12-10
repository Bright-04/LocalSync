using LocalSync.Core.Models;

namespace LocalSync.Core.Interfaces;

public interface IDeviceDiscoveryService
{
    /// <summary>
    /// Starts the device discovery process (e.g. mDNS broadcasting and listening)
    /// </summary>
    Task StartDiscoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the discovery process
    /// </summary>
    Task StopDiscoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually connects to a fallback IP if mDNS fails
    /// </summary>
    Task<Device?> ConnectToIpAsync(string ipAddress, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event triggered when a new device is discovered or goes offline
    /// </summary>
    event EventHandler<Device> OnDeviceDiscovered;
    event EventHandler<Device> OnDeviceOffline;
}
