using CdrBilling.Application.Abstractions;
using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;
using CdrBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CdrBilling.Infrastructure.Persistence.Repositories;

public sealed class BillingSessionRepository(AppDbContext db) : IBillingSessionRepository
{
    public async Task CreateAsync(BillingSession session, CancellationToken ct = default)
    {
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);
    }

    public Task<BillingSession?> GetAsync(Guid id, CancellationToken ct = default)
        => db.Sessions.FindAsync([id], ct).AsTask();

    public async Task UpdateAsync(BillingSession session, CancellationToken ct = default)
    {
        db.Sessions.Update(session);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(Guid id, SessionStatus status, CancellationToken ct = default)
    {
        await db.Sessions
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status), ct);
    }
}
