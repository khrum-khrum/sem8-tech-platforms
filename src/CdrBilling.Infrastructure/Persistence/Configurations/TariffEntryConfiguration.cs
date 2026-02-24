using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CdrBilling.Infrastructure.Persistence.Configurations;

public sealed class TariffEntryConfiguration : IEntityTypeConfiguration<TariffEntry>
{
    public void Configure(EntityTypeBuilder<TariffEntry> b)
    {
        b.ToTable("tariff_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityByDefaultColumn();

        b.Property(x => x.SessionId).IsRequired();
        b.Property(x => x.Prefix).HasMaxLength(20).IsRequired();
        b.Property(x => x.Destination).HasMaxLength(200).IsRequired();
        b.Property(x => x.RatePerMin).HasPrecision(10, 6).IsRequired();
        b.Property(x => x.ConnectionFee).HasPrecision(10, 6).IsRequired();
        b.Property(x => x.TimebandStart).IsRequired();
        b.Property(x => x.TimebandEnd).IsRequired();
        b.Property(x => x.WeekdayMask)
            .HasConversion<byte>()
            .IsRequired();
        b.Property(x => x.Priority).IsRequired();
        b.Property(x => x.EffectiveDate).IsRequired();
        b.Property(x => x.ExpiryDate);

        b.HasIndex(x => x.SessionId).HasDatabaseName("ix_te_session");
        b.HasIndex(x => new { x.SessionId, x.Prefix }).HasDatabaseName("ix_te_prefix");
    }
}
