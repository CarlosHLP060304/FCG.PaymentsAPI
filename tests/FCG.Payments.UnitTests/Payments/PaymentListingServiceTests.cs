using FCG.Payments.Application.Messaging;
using FCG.Payments.Application.Payments.Queries;
using FCG.Payments.Domain.Payments;
using FCG.Payments.Infrastructure.Data.EF.Context;
using FCG.Payments.Infrastructure.Messaging;
using FCG.Payments.Infrastructure.Payments;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FCG.Payments.UnitTests.Payments;

public sealed class PaymentListingServiceTests
{
    [Fact]
    public async Task ListingMethods_ReturnPaymentAttemptAndOutboxData()
    {
        await using var database = await CreateDatabaseAsync();
        var payment = Payment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 25.50m, "USD", correlationId: Guid.NewGuid());
        payment.UpdateStatus(PaymentStatus.Approved);
        payment.AddAttempt(PaymentStatus.Approved);
        database.Payments.Add(payment);

        var paymentEvent = new PaymentProcessedEvent
        {
            EventId = Guid.NewGuid(),
            CorrelationId = payment.CorrelationId!.Value,
            OccurredAt = payment.UpdatedAt,
            Version = 1,
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            UserId = payment.UserId,
            GameId = payment.GameId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status.ToString()
        };
        database.OutboxMessages.Add(PaymentOutboxMessage.Create(paymentEvent));
        await database.SaveChangesAsync();

        var service = new PaymentListingService(database);
        var payments = await service.ListPaymentsAsync();
        var attempts = await service.ListPaymentAttemptsAsync();
        var outboxMessages = await service.ListOutboxMessagesAsync();

        var listedPayment = Assert.Single(payments);
        Assert.Equal(payment.Id, listedPayment.Id);
        Assert.Equal(payment.OrderId, listedPayment.OrderId);
        Assert.Equal("Approved", listedPayment.Status);
        Assert.Equal(25.50m, listedPayment.Amount);

        var listedAttempt = Assert.Single(attempts);
        Assert.Equal(payment.Id, listedAttempt.PaymentId);
        Assert.Equal(1, listedAttempt.AttemptNumber);
        Assert.Equal("Approved", listedAttempt.Status);

        var listedOutbox = Assert.Single(outboxMessages);
        Assert.Equal(paymentEvent.EventId, listedOutbox.EventId);
        Assert.Equal(payment.Id, listedOutbox.PaymentId);
        Assert.Contains(paymentEvent.EventId.ToString(), listedOutbox.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Null(listedOutbox.PublishedAt);
        Assert.Equal(0, listedOutbox.Attempts);
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
}
