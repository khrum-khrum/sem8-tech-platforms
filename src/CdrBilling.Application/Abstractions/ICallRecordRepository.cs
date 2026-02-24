using CdrBilling.Application.DTOs;
using CdrBilling.Domain.Entities;

namespace CdrBilling.Application.Abstractions;

public interface ICallRecordRepository
{
    Task BulkInsertAsync(IAsyncEnumerable<CallRecord> records, CancellationToken ct = default);
    IAsyncEnumerable<CallRecord> GetUnratedAsync(Guid sessionId, CancellationToken ct = default);
    Task<int> CountAsync(Guid sessionId, CancellationToken ct = default);
    Task BulkUpdateChargesAsync(
        IEnumerable<(long Id, decimal Charge, long TariffId)> updates,
        CancellationToken ct = default);
    Task<IReadOnlyList<SubscriberSummaryDto>> GetSummaryAsync(Guid sessionId, CancellationToken ct = default);
    Task<PagedResult<CallRecordDetailDto>> GetDetailAsync(
        Guid sessionId,
        string? phoneNumber,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
