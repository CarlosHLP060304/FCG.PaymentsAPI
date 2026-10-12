using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Payments.Infrastructure.Messaging;

public sealed class PaymentInboxMessageConfiguration : IEntityTypeConfiguration<PaymentInboxMessage>
{
    public void Configure(EntityTypeBuilder<PaymentInboxMessage> builder)
    {
        builder.ToTable("InboxMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.EventId).IsRequired();
        builder.Property(message => message.ConsumerName).HasMaxLength(200).IsRequired();
        builder.Property(message => message.ProcessedAt).IsRequired();
        builder.HasIndex(message => new { message.ConsumerName, message.EventId })
            .IsUnique()
            .HasDatabaseName("UX_InboxMessages_ConsumerName_EventId");
    }
}
