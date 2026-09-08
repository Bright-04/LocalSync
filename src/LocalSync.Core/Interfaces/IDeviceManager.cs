using LocalSync.Core.Models;

namespace LocalSync.Core.Interfaces;

public interface IDeviceManager
{
    IReadOnlyCollection<Device> GetDevices();
    void AddOrUpdateDevice(Device device);
    void RemoveDevice(Guid id);
}
