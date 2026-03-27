using CdrBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CdrBilling.Infrastructure.Persistence.Configurations;

public sealed class CallRecordConfiguration : IEntityTypeConfiguration<CallRecord>
{
    public void Configure(EntityTypeBuilder<CallRecord> b)
    {
        b.ToTable("call_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityByDefaultColumn();

        b.Property(x => x.SessionId).IsRequired();
        b.Property(x => x.StartTime).IsRequired();
        b.Property(x => x.EndTime).IsRequired();
        b.Property(x => x.CallingParty).HasMaxLength(50).IsRequired();
        b.Property(x => x.CalledParty).HasMaxLength(50).IsRequired();
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(x => x.Disposition).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(x => x.DurationSec).IsRequired();
        b.Property(x => x.BillableSec).IsRequired();
        b.Property(x => x.OriginalCharge).HasPrecision(12, 4);
        b.Property(x => x.AccountCode).HasMaxLength(50);
        b.Property(x => x.CallId).HasMaxLength(100).IsRequired();
        b.Property(x => x.TrunkName).HasMaxLength(100);
        b.Property(x => x.ComputedCharge).HasPrecision(12, 4);
        b.Property(x => x.AppliedTariffId);

        b.HasOne(x => x.AppliedTariff)
            .WithMany()
            .HasForeignKey(x => x.AppliedTariffId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.SessionId).HasDatabaseName("ix_cr_session");
        b.HasIndex(x => new { x.SessionId, x.CallingParty }).HasDatabaseName("ix_cr_calling");
        b.HasIndex(x => new { x.SessionId, x.CalledParty }).HasDatabaseName("ix_cr_called");
        b.HasIndex(x => new { x.SessionId, x.Disposition, x.CallingParty }).HasDatabaseName("ix_cr_session_disposition_calling");
        b.HasIndex(x => new { x.SessionId, x.Disposition, x.CalledParty }).HasDatabaseName("ix_cr_session_disposition_called");
    }
}
