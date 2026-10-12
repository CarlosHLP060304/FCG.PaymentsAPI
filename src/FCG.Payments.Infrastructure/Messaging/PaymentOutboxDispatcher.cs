using System.Text.Json;
using FCG.Payments.Application.Messaging;
using FCG.Payments.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FCG.Payments.Infrastructure.Messaging;

public sealed class PaymentOutboxDispatcher(
    PaymentsDbContext dbContext,
    IPaymentProcessedEventPublisher publisher,
    TimeProvider timeProvider,
    ILogger<PaymentOutboxDispatcher> logger)
{
    private const string PaymentProcessedExchange = "payment-processed";
    private const int BatchSize = 20;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var dispatchedCount = 0;
        for (var index = 0; index < BatchSize; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var messageId = await dbContext.OutboxMessages
                .AsNoTracking()
                .Where(message => message.PublishedAt == null &&
                    message.NextAttemptAt <= now &&
                    (message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now))
                .OrderBy(message => message.NextAttemptAt)
                .ThenBy(message => message.Id)
                .Select(message => (Guid?)message.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (messageId is null)
            {
                break;
            }

            var leaseId = Guid.NewGuid();
            var attempt = await dbContext.OutboxMessages
                .Where(message => message.Id == messageId &&
                    message.PublishedAt == null &&
                    message.NextAttemptAt <= now &&
                    (message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now))
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(message => message.LeaseId, (Guid?)leaseId)
                    .SetProperty(message => message.LeaseExpiresAt, (DateTime?)(now + LeaseDuration))
                    .SetProperty(message => message.Attempts, message => message.Attempts + 1), cancellationToken);

            if (attempt == 0)
            {
                continue;
            }

            var outboxMessage = await dbContext.OutboxMessages.AsNoTracking()
                .SingleAsync(message => message.Id == messageId && message.LeaseId == leaseId, cancellationToken);

            PaymentProcessedEvent paymentEvent;
            try
            {
                paymentEvent = outboxMessage.DeserializeEvent();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await ScheduleRetryAsync(outboxMessage, leaseId, cancellationToken);
                logger.LogError(
                    "Could not deserialize PaymentProcessedEvent. EventId={EventId}, PaymentId={PaymentId}, Attempt={Attempt}, ErrorType={ErrorType}",
                    outboxMessage.EventId,
                    outboxMessage.PaymentId,
                    outboxMessage.Attempts,
                    exception.GetType().Name);
                continue;
            }

            logger.LogInformation(
                "Publishing PaymentProcessedEvent to RabbitMQ exchange {Exchange}. EventId={EventId}, CorrelationId={CorrelationId}, PaymentId={PaymentId}, OrderId={OrderId}, Status={Status}, Attempt={Attempt}, Payload={Payload}",
                PaymentProcessedExchange,
                paymentEvent.EventId,
                paymentEvent.CorrelationId,
                paymentEvent.PaymentId,
                paymentEvent.OrderId,
                paymentEvent.Status,
                outboxMessage.Attempts,
                JsonSerializer.Serialize(paymentEvent, JsonOptions));

            try
            {
                await publisher.PublishAsync(paymentEvent, cancellationToken);
                var publishedAt = timeProvider.GetUtcNow();
                var markedPublished = await dbContext.OutboxMessages
                    .Where(message => message.Id == messageId && message.LeaseId == leaseId && message.PublishedAt == null)
                    .ExecuteUpdateAsync(updates => updates
                        .SetProperty(message => message.PublishedAt, (DateTimeOffset?)publishedAt)
                        .SetProperty(message => message.LeaseId, (Guid?)null)
                        .SetProperty(message => message.LeaseExpiresAt, (DateTime?)null), cancellationToken);

                if (markedPublished == 1)
                {
                    dispatchedCount++;
                    logger.LogInformation(
                        "Published PaymentProcessedEvent. EventId={EventId}, CorrelationId={CorrelationId}, PaymentId={PaymentId}, OrderId={OrderId}, Status={Status}, Attempt={Attempt}",
                        paymentEvent.EventId,
                        paymentEvent.CorrelationId,
                        paymentEvent.PaymentId,
                        paymentEvent.OrderId,
                        paymentEvent.Status,
                        outboxMessage.Attempts);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await ScheduleRetryAsync(outboxMessage, leaseId, cancellationToken);
                logger.LogWarning(
                    exception,
                    "PaymentProcessedEvent publication failed to RabbitMQ exchange {Exchange}. EventId={EventId}, CorrelationId={CorrelationId}, PaymentId={PaymentId}, OrderId={OrderId}, Status={Status}, Attempt={Attempt}",
                    PaymentProcessedExchange,
                    paymentEvent.EventId,
                    paymentEvent.CorrelationId,
                    paymentEvent.PaymentId,
                    paymentEvent.OrderId,
                    paymentEvent.Status,
                    outboxMessage.Attempts);
            }
        }

        return dispatchedCount;
    }

    private Task<int> ScheduleRetryAsync(
        PaymentOutboxMessage message,
        Guid leaseId,
        CancellationToken cancellationToken)
    {
        var retrySeconds = Math.Min(60, Math.Pow(2, Math.Min(message.Attempts - 1, 6)));
        var nextAttemptAt = timeProvider.GetUtcNow().UtcDateTime.AddSeconds(retrySeconds);
        return dbContext.OutboxMessages
            .Where(candidate => candidate.Id == message.Id && candidate.LeaseId == leaseId && candidate.PublishedAt == null)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(candidate => candidate.NextAttemptAt, nextAttemptAt)
                .SetProperty(candidate => candidate.LeaseId, (Guid?)null)
                .SetProperty(candidate => candidate.LeaseExpiresAt, (DateTime?)null), cancellationToken);
    }
}
