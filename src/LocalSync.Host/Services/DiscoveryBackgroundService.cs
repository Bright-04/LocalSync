using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using LocalSync.Infrastructure.Discovery;
using Microsoft.Extensions.Hosting;

namespace LocalSync.Host.Services;

/// <summary>Runs multicast discovery and relays peer changes to clients.</summary>
public sealed class DiscoveryBackgroundService : BackgroundService
{
    private readonly MulticastResponder _responder;
    private readonly IPeerRegistry _registry;
    private readonly ITransferEventSink _events;

    public DiscoveryBackgroundService(
        MulticastResponder responder, IPeerRegistry registry, ITransferEventSink events)
    {
        ArgumentNullException.ThrowIfNull(responder);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(events);

        _responder = responder;
        _registry = registry;
        _events = events;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _registry.PeerAppeared += OnPeerAppeared;
        _registry.PeerDisappeared += OnPeerDisappeared;
        _responder.Start();

        stoppingToken.Register(() =>
        {
            _registry.PeerAppeared -= OnPeerAppeared;
            _registry.PeerDisappeared -= OnPeerDisappeared;
        });

        return Task.CompletedTask;
    }

    private void OnPeerAppeared(object? sender, Peer peer) =>
        _ = _events.PeerAppearedAsync(peer).AsTask();

    private void OnPeerDisappeared(object? sender, Peer peer) =>
        _ = _events.PeerDisappearedAsync(peer).AsTask();

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await _responder.DisposeAsync().ConfigureAwait(false);
    }
}
