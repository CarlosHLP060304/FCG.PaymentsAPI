using FCG.Payments.Application.Abstractions.Repositories;
using FCG.Payments.Application.Payments.CreatePayment;
using FCG.Payments.Domain.Payments;
using FCG.Payments.Infrastructure.Data.EF.Context;
using FCG.Payments.Infrastructure.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FCG.Payments.UnitTests.Payments;

public sealed class PaymentTests
{
    [Fact]
    public void Create_StartsPending_AndKeepsExternalOrderAndContractedAmount()
    {
        var orderId = Guid.NewGuid();
        var payment = Payment.Create(orderId, Guid.NewGuid(), Guid.NewGuid(), 19.95m, "usd");

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(orderId, payment.OrderId);
        Assert.Equal(19.95m, payment.Amount);
        Assert.Equal("USD", payment.Currency);
        Assert.Equal(payment.CreatedAt, payment.UpdatedAt);
    }

    [Theory]
    [InlineData(PaymentStatus.Approved)]
    [InlineData(PaymentStatus.Rejected)]
    public void UpdateStatus_AllowsTerminalDecisionFromPending(PaymentStatus status)
    {
        var payment = CreatePayment();

        payment.UpdateStatus(status);

        Assert.Equal(status, payment.Status);
        Assert.True(payment.UpdatedAt >= payment.CreatedAt);
    }

    [Fact]
    public void UpdateStatus_RejectsTransitionFromTerminalState()
    {
        var payment = CreatePayment();
        payment.UpdateStatus(PaymentStatus.Approved);

        Assert.Throws<InvalidOperationException>(() => payment.UpdateStatus(PaymentStatus.Rejected));
    }

    [Fact]
    public async Task Sqlite_PersistsPaymentValuesAndEnforcesUniqueOrderId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();

        var orderId = Guid.NewGuid();
        var payment = Payment.Create(orderId, Guid.NewGuid(), Guid.NewGuid(), 124.5678m, "BRL");
        payment.AddAttempt(PaymentStatus.Pending, createdAt: payment.CreatedAt);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();
        var persisted = await dbContext.Payments.Include(item => item.Attempts).SingleAsync();
        Assert.Equal(orderId, persisted.OrderId);
        Assert.Equal(124.5678m, persisted.Amount);
        Assert.Equal("BRL", persisted.Currency);
        Assert.Equal(PaymentStatus.Pending, persisted.Status);
        Assert.Equal(1, Assert.Single(persisted.Attempts).AttemptNumber);

        persisted.UpdateStatus(PaymentStatus.Approved);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        Assert.Equal(PaymentStatus.Approved, (await dbContext.Payments.SingleAsync()).Status);

        dbContext.Payments.Add(Payment.Create(orderId, Guid.NewGuid(), Guid.NewGuid(), 1.2345m, "USD"));
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public void DbContext_ContainsOnlyFinancialEntitiesAndInternalAttemptRelationship()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var dbContext = CreateDbContext(connection);

        var entityTypes = dbContext.Model.GetEntityTypes().ToArray();
        Assert.Equal(4, entityTypes.Length);
        Assert.Contains(entityTypes, entity => entity.ClrType == typeof(Payment));
        Assert.Contains(entityTypes, entity => entity.ClrType == typeof(PaymentAttempt));
        Assert.Contains(entityTypes, entity => entity.ClrType == typeof(PaymentInboxMessage));
        Assert.Contains(entityTypes, entity => entity.ClrType == typeof(PaymentOutboxMessage));
        Assert.All(entityTypes.SelectMany(entity => entity.GetForeignKeys()), foreignKey =>
            Assert.Equal(typeof(Payment), foreignKey.PrincipalEntityType.ClrType));
        Assert.Contains(entityTypes.Single(entity => entity.ClrType == typeof(Payment))
            .GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Payment.OrderId));
        Assert.Contains(entityTypes.Single(entity => entity.ClrType == typeof(PaymentInboxMessage))
            .GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PaymentInboxMessage.ConsumerName), nameof(PaymentInboxMessage.EventId)]));
    }

    [Fact]
    public async Task CreatePaymentHandler_IsIdempotentByOrderId()
    {
        var repository = new InMemoryPaymentRepository();
        var handler = new CreatePaymentHandler(repository);
        var command = new CreatePaymentCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 32m, "USD");

        var first = await handler.HandleAsync(command);
        var second = await handler.HandleAsync(command with { Amount = 99m });

        Assert.Same(first, second);
        Assert.Equal(32m, second.Amount);
        Assert.Single(repository.Payments);
    }

    private static Payment CreatePayment() =>
        Payment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, "USD");

    private static PaymentsDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseSqlite(connection)
            .Options;
        return new PaymentsDbContext(options);
    }

    private sealed class InMemoryPaymentRepository : IPaymentRepository
    {
        public List<Payment> Payments { get; } = [];

        public Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            Payments.Add(payment);
            return Task.CompletedTask;
        }

        public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Payments.SingleOrDefault(payment => payment.Id == id));

        public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Payments.SingleOrDefault(payment => payment.OrderId == orderId));

        public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Payments.Any(payment => payment.OrderId == orderId));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
