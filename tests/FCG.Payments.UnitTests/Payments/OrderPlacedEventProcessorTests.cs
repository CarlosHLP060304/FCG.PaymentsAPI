using FCG.Payments.Application.Messaging;
using FCG.Payments.Domain.Payments;
using FCG.Payments.Infrastructure.Data.EF.Context;
using FCG.Payments.Infrastructure.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FCG.Payments.UnitTests.Payments;

public sealed class OrderPlacedEventProcessorTests
{
    [Fact]
    public async Task ProcessAsync_CreatesPendingPaymentAndInboxInOneTransaction()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var processor = new OrderPlacedEventProcessor(dbContext);
        var message = CreateEvent();

        var result = await processor.ProcessAsync(message);

        var payment = await dbContext.Payments.SingleAsync();
        var inboxEntry = await dbContext.InboxMessages.SingleAsync();
        Assert.Equal(OrderPlacedProcessingOutcome.Created, result.Outcome);
        Assert.Equal(payment.Id, result.PaymentId);
        Assert.Equal(message.OrderId, payment.OrderId);
        Assert.Equal(message.UserId, payment.UserId);
        Assert.Equal(message.GameId, payment.GameId);
        Assert.Equal(message.Price, payment.Amount);
        Assert.Equal(message.Currency.ToUpperInvariant(), payment.Currency);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(message.CorrelationId, payment.CorrelationId);
        Assert.Equal(message.EventId, inboxEntry.EventId);
        Assert.Equal(OrderPlacedEventProcessor.ConsumerName, inboxEntry.ConsumerName);
    }

    [Fact]
    public async Task ProcessAsync_RedeliveredEventDoesNotRepeatEffect()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var processor = new OrderPlacedEventProcessor(dbContext);
        var message = CreateEvent();

        var first = await processor.ProcessAsync(message);
        var duplicate = await processor.ProcessAsync(message);

        Assert.Equal(OrderPlacedProcessingOutcome.Created, first.Outcome);
        Assert.Equal(OrderPlacedProcessingOutcome.DuplicateEvent, duplicate.Outcome);
        Assert.Equal(first.PaymentId, duplicate.PaymentId);
        Assert.Equal(1, await dbContext.Payments.CountAsync());
        Assert.Equal(1, await dbContext.InboxMessages.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_DifferentEventForSameOrderDoesNotCreateAnotherPayment()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var processor = new OrderPlacedEventProcessor(dbContext);
        var firstMessage = CreateEvent();
        var otherEvent = firstMessage with
        {
            EventId = Guid.NewGuid(),
            Price = firstMessage.Price + 50m,
            CorrelationId = Guid.NewGuid()
        };

        var first = await processor.ProcessAsync(firstMessage);
        var duplicateOrder = await processor.ProcessAsync(otherEvent);

        Assert.Equal(OrderPlacedProcessingOutcome.DuplicateOrder, duplicateOrder.Outcome);
        Assert.Equal(first.PaymentId, duplicateOrder.PaymentId);
        Assert.Equal(1, await dbContext.Payments.CountAsync());
        Assert.Equal(2, await dbContext.InboxMessages.CountAsync());
        Assert.Equal(firstMessage.Price, (await dbContext.Payments.SingleAsync()).Amount);
    }

    [Fact]
    public async Task ProcessAsync_InvalidMessageIsRejectedBeforePersistence()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var invalidMessage = CreateEvent() with { Currency = "US" };

        await Assert.ThrowsAsync<InvalidOrderPlacedEventException>(
            () => new OrderPlacedEventProcessor(dbContext).ProcessAsync(invalidMessage));

        Assert.Empty(await dbContext.Payments.ToListAsync());
        Assert.Empty(await dbContext.InboxMessages.ToListAsync());
    }

    [Fact]
    public async Task ProcessAsync_FailedInboxWriteRollsBackPaymentAndRedeliverySucceeds()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER FailInboxInsert BEFORE INSERT ON InboxMessages " +
            "BEGIN SELECT RAISE(ABORT, 'simulated inbox failure'); END;");
        var processor = new OrderPlacedEventProcessor(dbContext);
        var message = CreateEvent();

        await Assert.ThrowsAsync<DbUpdateException>(() => processor.ProcessAsync(message));
        Assert.Empty(await dbContext.Payments.ToListAsync());
        Assert.Empty(await dbContext.InboxMessages.ToListAsync());

        await dbContext.Database.ExecuteSqlRawAsync("DROP TRIGGER FailInboxInsert;");
        var redelivery = await processor.ProcessAsync(message);

        Assert.Equal(OrderPlacedProcessingOutcome.Created, redelivery.Outcome);
        Assert.Single(await dbContext.Payments.ToListAsync());
        Assert.Single(await dbContext.InboxMessages.ToListAsync());
    }

    private static OrderPlacedEvent CreateEvent() => new()
    {
        EventId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        OccurredAt = DateTimeOffset.UtcNow,
        Version = 1,
        OrderId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        GameId = Guid.NewGuid(),
        Price = 25.99m,
        Currency = "brl"
    };

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static PaymentsDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseSqlite(connection)
            .Options;
        return new PaymentsDbContext(options);
    }
}
