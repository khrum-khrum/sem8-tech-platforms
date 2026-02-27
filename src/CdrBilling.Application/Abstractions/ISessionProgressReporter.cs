namespace CdrBilling.Application.Abstractions;

public interface ISessionProgressReporter
{
    Task ReportAsync(Guid sessionId, int processed, int total, CancellationToken ct = default);
    Task ReportCompletedAsync(Guid sessionId, int processed, int total, CancellationToken ct = default);
    Task ReportFailedAsync(Guid sessionId, string error, CancellationToken ct = default);
}
