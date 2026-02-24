using CdrBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CdrBilling.Infrastructure.Persistence.Configurations;

public sealed class SubscriberConfiguration : IEntityTypeConfiguration<Subscriber>
{
    public void Configure(EntityTypeBuilder<Subscriber> b)
    {
        b.ToTable("subscribers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityByDefaultColumn();

        b.Property(x => x.SessionId).IsRequired();
        b.Property(x => x.PhoneNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.ClientName).HasMaxLength(200).IsRequired();

        b.HasIndex(x => x.SessionId).HasDatabaseName("ix_sub_session");
        b.HasIndex(x => new { x.SessionId, x.PhoneNumber }).HasDatabaseName("ix_sub_phone");
    }
}
