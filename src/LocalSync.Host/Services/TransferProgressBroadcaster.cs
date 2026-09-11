using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using Microsoft.Extensions.Hosting;

namespace LocalSync.Host.Services;

/// <summary>
/// Publishes progress on a fixed tick instead of per chunk.
/// </summary>
/// <remarks>
/// At 1 MiB chunks and gigabit line rate the per-chunk design produced roughly
/// 125 broadcasts a second per transfer, each serialising JSON to every
/// connected client, to drive a UI that repaints 60 times a second behind a
/// 500 ms CSS transition. 4 Hz is below the threshold where a progress bar
/// looks anything but smooth, and it makes throughput independent of how many
/// clients are watching.
/// </remarks>
public sealed class TransferProgressBroadcaster : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    private readonly ITransferManager _transfers;
    private readonly ITransferEventSink _sink;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<Guid, long> _lastPublished = [];
    private readonly HashSet<Guid> _settled = [];

    public TransferProgressBroadcaster(
        ITransferManager transfers, ITransferEventSink sink, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(transfers);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _transfers = transfers;
        _sink = sink;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                var batch = CollectChanged();
                if (batch.Count > 0)
                {
                    await _sink.ProgressBatchAsync(batch, stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    /// <summary>Only transfers whose byte count moved since the last tick.</summary>
    internal List<TransferProgress> CollectChanged()
    {
        var batch = new List<TransferProgress>();

        foreach (var session in _transfers.GetSessions())
        {
            if (_settled.Contains(session.Id))
            {
                continue;
            }

            var transferred = session.TransferredSize;

            if (_lastPublished.TryGetValue(session.Id, out var previous) && previous == transferred)
            {
                continue;
            }

            _lastPublished[session.Id] = transferred;
            batch.Add(new TransferProgress(session.Id, session.State, transferred, session.TotalSize));

            // Publish a terminal transfer once, then stop considering it.
            // Dropping it from _lastPublished instead would make the next tick
            // see no previous value and republish it, forever.
            if (session.IsTerminal)
            {
                _settled.Add(session.Id);
                _lastPublished.Remove(session.Id);
            }
        }

        return batch;
    }
}
