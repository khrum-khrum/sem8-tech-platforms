using CdrBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CdrBilling.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BillingSession> Sessions => Set<BillingSession>();
    public DbSet<CallRecord>     CallRecords => Set<CallRecord>();
    public DbSet<TariffEntry>    TariffEntries => Set<TariffEntry>();
    public DbSet<Subscriber>     Subscribers => Set<Subscriber>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
