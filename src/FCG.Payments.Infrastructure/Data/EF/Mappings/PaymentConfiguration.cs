using FCG.Payments.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Payments.Infrastructure.Data.EF.Mappings;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount_Positive", "\"Amount\" > 0");
            table.HasCheckConstraint("CK_Payments_Status", "\"Status\" IN ('Pending', 'Approved', 'Rejected')");
        });

        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.OrderId).IsRequired();
        builder.HasIndex(payment => payment.OrderId).IsUnique().HasDatabaseName("UX_Payments_OrderId");
        builder.Property(payment => payment.UserId).IsRequired();
        builder.Property(payment => payment.GameId).IsRequired();
        builder.Property(payment => payment.CorrelationId);
        builder.Property(payment => payment.Amount).HasPrecision(18, 4).IsRequired();
        builder.Property(payment => payment.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(payment => payment.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(payment => payment.CreatedAt).IsRequired();
        builder.Property(payment => payment.UpdatedAt).IsRequired();

        builder.HasMany(payment => payment.Attempts)
            .WithOne()
            .HasForeignKey(attempt => attempt.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(payment => payment.Attempts)
            .HasField("_attempts")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
