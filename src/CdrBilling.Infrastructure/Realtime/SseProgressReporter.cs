using CdrBilling.Application.Abstractions;

namespace CdrBilling.Infrastructure.Realtime;

public sealed class SseProgressReporter(SseProgressHub hub) : ISessionProgressReporter
{
    public async Task ReportAsync(Guid sessionId, int processed, int total, CancellationToken ct = default)
        => await hub.WriteAsync(sessionId,
            new ProgressEvent(processed, total, "running"), ct);

    public async Task ReportCompletedAsync(Guid sessionId, CancellationToken ct = default)
        => await hub.WriteAsync(sessionId,
            new ProgressEvent(0, 0, "completed"), ct);

    public async Task ReportFailedAsync(Guid sessionId, string error, CancellationToken ct = default)
        => await hub.WriteAsync(sessionId,
            new ProgressEvent(0, 0, "failed", error), ct);
}
