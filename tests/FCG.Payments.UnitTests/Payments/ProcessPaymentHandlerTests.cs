using FCG.Payments.Application.Abstractions.Repositories;
using FCG.Payments.Application.Messaging;
using FCG.Payments.Application.Payments.ProcessPayment;
using FCG.Payments.Domain.Payments;
using FCG.Payments.Infrastructure.Data.EF.Context;
using FCG.Payments.Infrastructure.Messaging;
using FCG.Payments.Infrastructure.Payments;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FCG.Payments.UnitTests.Payments;

public sealed class ProcessPaymentHandlerTests
{
    [Theory]
    [InlineData(49.99, PaymentStatus.Approved)]
    [InlineData(100.00, PaymentStatus.Approved)]
    [InlineData(100.01, PaymentStatus.Rejected)]
    public async Task HandleAsync_UsesDeterministicAmountLimit(decimal amount, PaymentStatus expectedStatus)
    {
        await using var database = await CreateDatabaseAsync();
        var payment = CreatePayment(amount);
        database.Payments.Add(payment);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var handler = CreateHandler(database);
        var result = await handler.HandleAsync(payment.Id);

        Assert.NotNull(result);
        Assert.True(result.WasProcessed);
        Assert.Equal(PaymentStatus.Pending, result.PreviousStatus);
        Assert.Equal(expectedStatus, result.CurrentStatus);
        Assert.Equal(expectedStatus, (await database.Payments.Include(item => item.Attempts).SingleAsync()).Status);
        var attempt = Assert.Single((await database.Payments.Include(item => item.Attempts).SingleAsync()).Attempts);
        Assert.Equal(expectedStatus, attempt.Status);
        Assert.Equal(1, attempt.AttemptNumber);
        var outboxMessage = await database.OutboxMessages.SingleAsync();
        var paymentEvent = outboxMessage.DeserializeEvent();
        Assert.NotEqual(Guid.Empty, paymentEvent.EventId);
        Assert.Equal(payment.CorrelationId, paymentEvent.CorrelationId);
        Assert.Equal(payment.Id, paymentEvent.PaymentId);
        Assert.Equal(payment.OrderId, paymentEvent.OrderId);
        Assert.Equal(payment.UserId, paymentEvent.UserId);
        Assert.Equal(payment.GameId, paymentEvent.GameId);
        Assert.Equal(payment.Amount, paymentEvent.Amount);
        Assert.Equal(payment.Currency, paymentEvent.Currency);
        Assert.Equal(expectedStatus.ToString(), paymentEvent.Status);
    }

    [Theory]
    [InlineData(PaymentStatus.Approved)]
    [InlineData(PaymentStatus.Rejected)]
    public async Task HandleAsync_TerminalPaymentDoesNotChangeOrCreateAnotherAttempt(PaymentStatus status)
    {
        await using var database = await CreateDatabaseAsync();
        var payment = CreatePayment(30m);
        payment.UpdateStatus(status);
        payment.AddAttempt(status);
        database.Payments.Add(payment);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var result = await CreateHandler(database).HandleAsync(payment.Id);

        Assert.NotNull(result);
        Assert.False(result.WasProcessed);
        Assert.Equal(status, result.PreviousStatus);
        Assert.Equal(status, result.CurrentStatus);
        Assert.Single((await database.Payments.Include(item => item.Attempts).SingleAsync()).Attempts);
        Assert.Empty(await database.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task HandleAsync_MissingPaymentReturnsNull()
    {
        await using var database = await CreateDatabaseAsync();

        var result = await CreateHandler(database).HandleAsync(Guid.NewGuid());

        Assert.Null(result);
        Assert.Empty(await database.Payments.ToListAsync());
    }

    [Fact]
    public async Task HandleAsync_ReprocessingApprovedPaymentIsIdempotent()
    {
        await using var database = await CreateDatabaseAsync();
        var payment = CreatePayment(90m);
        database.Payments.Add(payment);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var handler = CreateHandler(database);

        var first = await handler.HandleAsync(payment.Id);
        var second = await handler.HandleAsync(payment.Id);

        Assert.True(first!.WasProcessed);
        Assert.False(second!.WasProcessed);
        Assert.Equal(first.CurrentStatus, second.CurrentStatus);
        Assert.Single((await database.Payments.Include(item => item.Attempts).SingleAsync()).Attempts);
        Assert.Single(await database.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task HandleAsync_PersistsCorrelationAndEventDataAlongWithDecision()
    {
        await using var database = await CreateDatabaseAsync();
        var payment = Payment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 42m, "BRL", correlationId: Guid.NewGuid());
        database.Payments.Add(payment);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        await CreateHandler(database).HandleAsync(payment.Id);

        var persisted = await database.Payments.SingleAsync();
        Assert.Equal(payment.Id, persisted.Id);
        Assert.Equal(payment.OrderId, persisted.OrderId);
        Assert.Equal(payment.UserId, persisted.UserId);
        Assert.Equal(payment.GameId, persisted.GameId);
        Assert.Equal(42m, persisted.Amount);
        Assert.Equal("BRL", persisted.Currency);
        Assert.Equal(payment.CorrelationId, persisted.CorrelationId);
        Assert.True(persisted.UpdatedAt >= payment.CreatedAt);
    }

    private static ProcessPaymentHandler CreateHandler(PaymentsDbContext database) =>
        new(
            new PaymentRepositoryForTests(database),
            new AmountBasedPaymentSimulationStrategy(100m),
            new PaymentProcessedEventOutbox(database));

    private static Payment CreatePayment(decimal amount) =>
        Payment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), amount, "USD", correlationId: Guid.NewGuid());

    private static async Task<PaymentsDbContext> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseSqlite(connection)
            .Options;
        var database = new PaymentsDbContext(options);
        await database.Database.EnsureCreatedAsync();
        return database;
    }

    private sealed class PaymentRepositoryForTests(PaymentsDbContext database) : IPaymentRepository
    {
        public Task AddAsync(Payment payment, CancellationToken cancellationToken = default) =>
            database.Payments.AddAsync(payment, cancellationToken).AsTask();

        public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            database.Payments.Include(payment => payment.Attempts)
                .SingleOrDefaultAsync(payment => payment.Id == id, cancellationToken);

        public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
            database.Payments.Include(payment => payment.Attempts)
                .SingleOrDefaultAsync(payment => payment.OrderId == orderId, cancellationToken);

        public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
            database.Payments.AnyAsync(payment => payment.OrderId == orderId, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            database.SaveChangesAsync(cancellationToken);
    }
}
