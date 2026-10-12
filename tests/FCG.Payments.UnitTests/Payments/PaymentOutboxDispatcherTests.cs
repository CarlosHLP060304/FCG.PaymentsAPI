using FCG.Payments.Application.Messaging;
using FCG.Payments.Infrastructure.Data.EF.Context;
using FCG.Payments.Infrastructure.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FCG.Payments.UnitTests.Payments;

public sealed class PaymentOutboxDispatcherTests
{
    [Fact]
    public async Task DispatchPendingAsync_RetriesUnavailableBrokerWithSameEventAndCorrelationIds()
    {
        await using var database = await CreateDatabaseAsync();
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-10-05T12:00:00Z"));
        var paymentEvent = new PaymentProcessedEvent
        {
            EventId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = clock.GetUtcNow(),
            Version = 1,
            PaymentId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            Amount = 57.25m,
            Currency = "BRL",
            Status = "Approved"
        };
        database.OutboxMessages.Add(PaymentOutboxMessage.Create(paymentEvent));
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var publisher = new FailingOncePublisher();
        var dispatcher = new PaymentOutboxDispatcher(
            database,
            publisher,
            clock,
            NullLogger<PaymentOutboxDispatcher>.Instance);

        Assert.Equal(0, await dispatcher.DispatchPendingAsync());
        var pending = await database.OutboxMessages.SingleAsync();
        Assert.Null(pending.PublishedAt);
        Assert.Equal(1, pending.Attempts);
        Assert.Equal(paymentEvent.EventId, publisher.Messages.Single().EventId);
        database.ChangeTracker.Clear();

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, await dispatcher.DispatchPendingAsync());

        var published = await database.OutboxMessages.SingleAsync();
        Assert.NotNull(published.PublishedAt);
        Assert.Equal(2, published.Attempts);
        Assert.Equal(2, publisher.Messages.Count);
        Assert.Equal(paymentEvent.EventId, publisher.Messages[1].EventId);
        Assert.Equal(paymentEvent.CorrelationId, publisher.Messages[1].CorrelationId);
        Assert.Equal(paymentEvent.PaymentId, publisher.Messages[1].PaymentId);
        Assert.Equal(paymentEvent.OrderId, publisher.Messages[1].OrderId);
        Assert.Equal(paymentEvent, publisher.Messages[1]);
    }

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

    private sealed class FailingOncePublisher : IPaymentProcessedEventPublisher
    {
        public List<PaymentProcessedEvent> Messages { get; } = [];

        public Task PublishAsync(PaymentProcessedEvent message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            if (Messages.Count == 1)
            {
                throw new InvalidOperationException("Simulated broker outage.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
