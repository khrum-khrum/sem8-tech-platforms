using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;

namespace CdrBilling.Application.Abstractions;

public interface IBillingSessionRepository
{
    Task CreateAsync(BillingSession session, CancellationToken ct = default);
    Task<BillingSession?> GetAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(BillingSession session, CancellationToken ct = default);
    Task SetStatusAsync(Guid id, SessionStatus status, CancellationToken ct = default);
}
