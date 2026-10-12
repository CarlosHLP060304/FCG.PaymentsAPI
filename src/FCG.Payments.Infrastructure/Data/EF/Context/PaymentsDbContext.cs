using FCG.Payments.Domain.Payments;
using FCG.Payments.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace FCG.Payments.Infrastructure.Data.EF.Context;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    public DbSet<PaymentInboxMessage> InboxMessages => Set<PaymentInboxMessage>();

    public DbSet<PaymentOutboxMessage> OutboxMessages => Set<PaymentOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
