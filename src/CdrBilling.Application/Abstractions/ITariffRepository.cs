using CdrBilling.Domain.Entities;

namespace CdrBilling.Application.Abstractions;

public interface ITariffRepository
{
    Task BulkInsertAsync(IEnumerable<TariffEntry> entries, CancellationToken ct = default);
    Task<IReadOnlyList<TariffEntry>> GetAllForSessionAsync(Guid sessionId, CancellationToken ct = default);
}
