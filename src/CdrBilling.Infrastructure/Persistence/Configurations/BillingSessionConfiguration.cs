using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CdrBilling.Infrastructure.Persistence.Configurations;

public sealed class BillingSessionConfiguration : IEntityTypeConfiguration<BillingSession>
{
    public void Configure(EntityTypeBuilder<BillingSession> b)
    {
        b.ToTable("billing_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        b.Property(x => x.TotalRecords).HasDefaultValue(0);
        b.Property(x => x.ProcessedRecords).HasDefaultValue(0);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
    }
}
