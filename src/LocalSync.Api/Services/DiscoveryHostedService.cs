using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;

namespace LocalSync.Api.Services;

public class DiscoveryHostedService : IHostedService
{
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly IDeviceManager _deviceManager;

    public DiscoveryHostedService(IDeviceDiscoveryService discoveryService, IDeviceManager deviceManager)
    {
        _discoveryService = discoveryService;
        _deviceManager = deviceManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _discoveryService.OnDeviceDiscovered += OnDeviceDiscovered;
        _discoveryService.OnDeviceOffline += OnDeviceOffline;

        return _discoveryService.StartDiscoveryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _discoveryService.OnDeviceDiscovered -= OnDeviceDiscovered;
        _discoveryService.OnDeviceOffline -= OnDeviceOffline;

        return _discoveryService.StopDiscoveryAsync(cancellationToken);
    }

    private void OnDeviceDiscovered(object? sender, Device device)
    {
        _deviceManager.AddOrUpdateDevice(device);
    }

    private void OnDeviceOffline(object? sender, Device device)
    {
        _deviceManager.RemoveDevice(device.Id);
    }
}
