using System.Net;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Configuration;

namespace LocalSync.Infrastructure.Discovery;

public class MdnsDiscoveryService : IDeviceDiscoveryService, IDisposable
{
    private readonly MulticastService _mdns;
    private readonly ServiceDiscovery _discovery;
    private readonly ILogger<MdnsDiscoveryService> _logger;
    private readonly string _serviceName = "_localsync._tcp";
    private readonly Guid _localDeviceId = Guid.NewGuid();
    private readonly int _localPort;
    private readonly string _instanceName;

    public event EventHandler<Device>? OnDeviceDiscovered;
    public event EventHandler<Device>? OnDeviceOffline;

    public MdnsDiscoveryService(ILogger<MdnsDiscoveryService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _mdns = new MulticastService();
        _discovery = new ServiceDiscovery(_mdns);

        var urls = configuration["urls"] ?? configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000";
        var uri = new Uri(urls.Split(';')[0].Replace("*", "localhost").Replace("+", "localhost"));
        _localPort = uri.Port;
        _instanceName = $"{Environment.MachineName}-{_localPort}";

        _mdns.NetworkInterfaceDiscovered += (s, e) => _mdns.SendQuery(_serviceName + ".local");
        _discovery.ServiceInstanceDiscovered += Discovery_ServiceInstanceDiscovered;
    }

    private void Discovery_ServiceInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e)
    {
        if (!e.ServiceInstanceName.ToString().Contains(_serviceName)) return;

        var txtRecord = e.Message.AdditionalRecords.OfType<TXTRecord>().FirstOrDefault() 
                     ?? e.Message.Answers.OfType<TXTRecord>().FirstOrDefault();
        var aRecord = e.Message.AdditionalRecords.OfType<ARecord>().FirstOrDefault() 
                   ?? e.Message.Answers.OfType<ARecord>().FirstOrDefault();
        var srvRecord = e.Message.AdditionalRecords.OfType<SRVRecord>().FirstOrDefault() 
                     ?? e.Message.Answers.OfType<SRVRecord>().FirstOrDefault();

        var idString = txtRecord?.Strings.FirstOrDefault(s => s.StartsWith("id="))?.Substring(3);
        var ip = aRecord?.Address.ToString() ?? "127.0.0.1";
        var port = srvRecord?.Port ?? 5000;

        _logger.LogInformation("Discovered instance: {Name} | IP: {IP}:{Port} | ID: {ID}", 
            e.ServiceInstanceName.Labels[0], ip, port, idString ?? "MISSING");

        if (port == _localPort) return; // Prevent self-discovery

        if (!Guid.TryParse(idString, out var deviceId))
        {
            var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"{ip}:{port}"));
            deviceId = new Guid(hash);
        }

        if (deviceId != _localDeviceId)
        {
            var device = new Device
            {
                Id = deviceId,
                Name = e.ServiceInstanceName.Labels[0],
                IpAddress = ip,
                Port = port,
                IsOnline = true
            };
            OnDeviceDiscovered?.Invoke(this, device);
        }
    }

    public Task StartDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        _mdns.Start();
        
        var profile = new ServiceProfile(_instanceName, _serviceName, (ushort)_localPort);
        profile.AddProperty("id", _localDeviceId.ToString());
        
        _discovery.Advertise(profile);
        _logger.LogInformation("Started mDNS advertising for {ServiceName}", _serviceName);

        return Task.CompletedTask;
    }

    public Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        _discovery.Unadvertise();
        _mdns.Stop();
        _logger.LogInformation("Stopped mDNS advertising");
        return Task.CompletedTask;
    }

    public Task<Device?> ConnectToIpAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manually connecting to IP {IpAddress}:{Port}", ipAddress, port);
        return Task.FromResult<Device?>(new Device
        {
            Id = Guid.NewGuid(),
            IpAddress = ipAddress,
            Port = port,
            Name = $"Manual_{ipAddress}",
            IsOnline = true
        });
    }

    public void Dispose()
    {
        _discovery.Dispose();
        _mdns.Dispose();
    }
}
