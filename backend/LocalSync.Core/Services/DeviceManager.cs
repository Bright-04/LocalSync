using System.Collections.Concurrent;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;

namespace LocalSync.Core.Services;

public class DeviceManager : IDeviceManager
{
    private readonly ConcurrentDictionary<Guid, Device> _devices = new();

    public IReadOnlyCollection<Device> GetDevices() => _devices.Values.ToList();

    public void AddOrUpdateDevice(Device device)
    {
        device.LastSeen = DateTime.UtcNow;
        _devices.AddOrUpdate(device.Id, device, (_, existing) => 
        {
            existing.IpAddress = device.IpAddress;
            existing.Port = device.Port;
            existing.IsOnline = true;
            existing.LastSeen = DateTime.UtcNow;
            return existing;
        });
    }

    public void RemoveDevice(Guid id)
    {
        _devices.TryRemove(id, out _);
    }
}
