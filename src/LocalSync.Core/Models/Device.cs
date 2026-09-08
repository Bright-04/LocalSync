namespace LocalSync.Core.Models;

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}
