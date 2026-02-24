using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CdrBilling.Infrastructure.Realtime;

public sealed record ProgressEvent(int Processed, int Total, string Status, string? Error = null)
{
    public double Percent => Total > 0 ? Math.Round(Processed * 100.0 / Total, 1) : 0;
}

/// <summary>
/// Singleton in-memory relay: tariffication handler writes events here,
/// SSE endpoints subscribe to the channel for their session.
/// </summary>
public sealed class SseProgressHub
{
    private readonly ConcurrentDictionary<Guid, Channel<ProgressEvent>> _channels = new();

    public Channel<ProgressEvent> GetOrCreateChannel(Guid sessionId)
        => _channels.GetOrAdd(sessionId, _ =>
            Channel.CreateBounded<ProgressEvent>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            }));

    public void RemoveChannel(Guid sessionId)
        => _channels.TryRemove(sessionId, out _);

    public async ValueTask WriteAsync(Guid sessionId, ProgressEvent evt, CancellationToken ct = default)
    {
        var ch = GetOrCreateChannel(sessionId);
        await ch.Writer.WriteAsync(evt, ct);
    }
}
