using FCG.Payments.Application.Payments.Queries;
using FCG.Payments.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;

namespace FCG.Payments.Infrastructure.Payments;

public sealed class PaymentListingService(PaymentsDbContext dbContext) : IPaymentListingService
{
    public async Task<IReadOnlyList<PaymentResponse>> ListPaymentsAsync(
        CancellationToken cancellationToken = default)
    {
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Select(payment => new PaymentResponse(
                payment.Id,
                payment.OrderId,
                payment.UserId,
                payment.GameId,
                payment.Amount,
                payment.Currency,
                payment.CorrelationId,
                payment.Status.ToString(),
                payment.CreatedAt,
                payment.UpdatedAt))
            .ToListAsync(cancellationToken);

        return payments.OrderByDescending(payment => payment.CreatedAt).ToArray();
    }

    public async Task<IReadOnlyList<PaymentAttemptResponse>> ListPaymentAttemptsAsync(
        CancellationToken cancellationToken = default)
    {
        var attempts = await dbContext.PaymentAttempts
            .AsNoTracking()
            .Select(attempt => new PaymentAttemptResponse(
                attempt.Id,
                attempt.PaymentId,
                attempt.AttemptNumber,
                attempt.Status.ToString(),
                attempt.CreatedAt))
            .ToListAsync(cancellationToken);

        return attempts.OrderByDescending(attempt => attempt.CreatedAt)
            .ThenBy(attempt => attempt.AttemptNumber)
            .ToArray();
    }

    public async Task<IReadOnlyList<PaymentOutboxMessageResponse>> ListOutboxMessagesAsync(
        CancellationToken cancellationToken = default)
    {
        var messages = await dbContext.OutboxMessages
            .AsNoTracking()
            .Select(message => new
            {
                message.Id,
                message.EventId,
                message.PaymentId,
                message.EventType,
                message.Payload,
                message.OccurredAt,
                message.PublishedAt,
                message.Attempts,
                message.NextAttemptAt,
                message.LeaseExpiresAt
            })
            .ToListAsync(cancellationToken);

        return messages.Select(message => new PaymentOutboxMessageResponse(
            message.Id,
            message.EventId,
            message.PaymentId,
            message.EventType,
            message.Payload,
            message.OccurredAt,
            message.PublishedAt,
            message.Attempts,
            new DateTimeOffset(DateTime.SpecifyKind(message.NextAttemptAt, DateTimeKind.Utc)),
            message.LeaseExpiresAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(message.LeaseExpiresAt.Value, DateTimeKind.Utc))))
            .OrderByDescending(message => message.OccurredAt)
            .ToArray();
    }
}
