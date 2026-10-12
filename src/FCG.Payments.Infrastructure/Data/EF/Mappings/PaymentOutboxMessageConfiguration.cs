using FCG.Payments.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Payments.Infrastructure.Data.EF.Mappings;

public sealed class PaymentOutboxMessageConfiguration : IEntityTypeConfiguration<PaymentOutboxMessage>
{
    public void Configure(EntityTypeBuilder<PaymentOutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", table =>
        {
            table.HasCheckConstraint("CK_OutboxMessages_Attempts_NonNegative", "\"Attempts\" >= 0");
        });

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.EventId).IsRequired();
        builder.HasIndex(message => message.EventId).IsUnique()
            .HasDatabaseName("UX_OutboxMessages_EventId");
        builder.Property(message => message.PaymentId).IsRequired();
        builder.HasIndex(message => message.PaymentId).IsUnique()
            .HasDatabaseName("UX_OutboxMessages_PaymentId");
        builder.Property(message => message.EventType).HasMaxLength(300).IsRequired();
        builder.Property(message => message.Payload).IsRequired();
        builder.Property(message => message.OccurredAt).IsRequired();
        builder.Property(message => message.PublishedAt);
        builder.Property(message => message.Attempts).IsRequired();
        builder.Property(message => message.NextAttemptAt).IsRequired();
        builder.Property(message => message.LeaseId);
        builder.Property(message => message.LeaseExpiresAt);
        builder.HasIndex(message => new { message.PublishedAt, message.NextAttemptAt, message.LeaseExpiresAt })
            .HasDatabaseName("IX_OutboxMessages_Pending");
    }
}
