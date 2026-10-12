using FCG.Payments.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Payments.Infrastructure.Data.EF.Mappings;

public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("PaymentAttempts", table =>
        {
            table.HasCheckConstraint("CK_PaymentAttempts_AttemptNumber_Positive", "\"AttemptNumber\" > 0");
            table.HasCheckConstraint("CK_PaymentAttempts_Status", "\"Status\" IN ('Pending', 'Approved', 'Rejected')");
        });

        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).ValueGeneratedNever();
        builder.Property(attempt => attempt.PaymentId).IsRequired();
        builder.Property(attempt => attempt.AttemptNumber).IsRequired();
        builder.HasIndex(attempt => new { attempt.PaymentId, attempt.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("UX_PaymentAttempts_PaymentId_AttemptNumber");
        builder.Property(attempt => attempt.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(attempt => attempt.CreatedAt).IsRequired();
        builder.Property(attempt => attempt.ErrorMessage).HasMaxLength(1024);
    }
}
