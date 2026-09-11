using System.Text.Json;
using System.Threading.Channels;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using LocalSync.Protocol;
using LocalSync.Protocol.Transfer;

namespace LocalSync.Host.Services;

/// <summary>One connected browser or CLI client.</summary>
public sealed class EventSubscriber
{
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(256)
        {
            // A stalled reader must never block the transfer that is producing
            // events, and the newest state is the state worth keeping.
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public ChannelReader<string> Reader => _channel.Reader;

    public void Post(string payload) => _channel.Writer.TryWrite(payload);

    public void Complete() => _channel.Writer.TryComplete();
}

/// <summary>
/// Fans transfer and peer events out to connected clients over Server-Sent
/// Events.
/// </summary>
/// <remarks>
/// SSE rather than SignalR: SignalR is only partially AOT-supported, whereas
/// SSE is plain HTTP and needs no client library at all, which matters for the
/// zero-install browser page. The channel is one-way, which is all the UI needs.
/// </remarks>
public sealed class ServerSentEventPublisher : ITransferEventSink
{
    private readonly List<EventSubscriber> _subscribers = [];
    private readonly Lock _gate = new();

    public EventSubscriber Subscribe()
    {
        var subscriber = new EventSubscriber();
        lock (_gate)
        {
            _subscribers.Add(subscriber);
        }

        return subscriber;
    }

    public void Unsubscribe(EventSubscriber subscriber)
    {
        lock (_gate)
        {
            _subscribers.Remove(subscriber);
        }

        subscriber.Complete();
    }

    public int SubscriberCount
    {
        get
        {
            lock (_gate)
            {
                return _subscribers.Count;
            }
        }
    }

    public ValueTask TransferCreatedAsync(TransferSession session, CancellationToken ct = default)
    {
        Broadcast("transfer-created", Serialize(ToView(session)));
        return ValueTask.CompletedTask;
    }

    public ValueTask StateChangedAsync(
        Guid transferId, TransferState previous, TransferState current, CancellationToken ct = default)
    {
        Broadcast(
            "transfer-state",
            $$"""{"id":"{{transferId}}","from":"{{previous}}","to":"{{current}}"}""");
        return ValueTask.CompletedTask;
    }

    public ValueTask ProgressBatchAsync(
        IReadOnlyList<TransferProgress> batch, CancellationToken ct = default)
    {
        if (batch.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        var items = batch.Select(p =>
            $$"""{"id":"{{p.TransferId}}","state":"{{p.State}}","transferredSize":{{p.TransferredSize}},"totalSize":{{p.TotalSize}}}""");

        Broadcast("transfer-progress", $"[{string.Join(',', items)}]");
        return ValueTask.CompletedTask;
    }

    public ValueTask PeerAppearedAsync(Peer peer, CancellationToken ct = default)
    {
        Broadcast("peer-appeared", Serialize(ToView(peer)));
        return ValueTask.CompletedTask;
    }

    public ValueTask PeerDisappearedAsync(Peer peer, CancellationToken ct = default)
    {
        Broadcast("peer-disappeared", $$"""{"id":"{{peer.Id}}"}""");
        return ValueTask.CompletedTask;
    }

    private void Broadcast(string eventName, string json)
    {
        // SSE frames data line by line, so an embedded newline would split one
        // event into two malformed ones.
        var payload = $"event: {eventName}\ndata: {json.ReplaceLineEndings(string.Empty)}\n\n";

        lock (_gate)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Post(payload);
            }
        }
    }

    private static string Serialize(TransferView view) =>
        JsonSerializer.Serialize(view, LocalSyncJsonContext.Default.TransferView);

    private static string Serialize(PeerView view) =>
        JsonSerializer.Serialize(view, LocalSyncJsonContext.Default.PeerView);

    public static TransferView ToView(TransferSession session) => new()
    {
        Id = session.Id.ToString(),
        FileName = session.FileName,
        TotalSize = session.TotalSize,
        TransferredSize = session.TransferredSize,
        State = session.State.ToString(),
        TargetDeviceId = session.PeerId,
        CreatedAt = session.CreatedAt,
        CompletedAt = session.CompletedAt,
        ErrorMessage = session.ErrorMessage,
    };

    public static PeerView ToView(Peer peer) => new()
    {
        Id = peer.Id.ToString(),
        Alias = peer.Alias,
        DisplayId = peer.Id.ToDisplayString(),
        Mnemonic = peer.Id.ToMnemonic(),
        Address = peer.Address,
        Port = peer.Port,
        DeviceType = peer.DeviceType,
        Protocol = peer.Protocol.ToString(),
        LastSeen = peer.LastSeen,
    };
}
