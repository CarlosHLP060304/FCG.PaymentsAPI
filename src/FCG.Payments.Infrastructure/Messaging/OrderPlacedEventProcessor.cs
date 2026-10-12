using FCG.Payments.Application.Messaging;
using FCG.Payments.Domain.Payments;
using FCG.Payments.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;

namespace FCG.Payments.Infrastructure.Messaging;

public sealed class OrderPlacedEventProcessor(PaymentsDbContext dbContext) : IOrderPlacedEventProcessor
{
    public const string ConsumerName = "Payments.OrderPlacedConsumer";

    public async Task<OrderPlacedProcessingResult> ProcessAsync(
        OrderPlacedEvent message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        OrderPlacedEventValidator.Validate(message);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var inboxEntryExists = await dbContext.InboxMessages.AnyAsync(
                entry => entry.ConsumerName == ConsumerName && entry.EventId == message.EventId,
                cancellationToken);
            if (inboxEntryExists)
            {
                var duplicatePaymentId = await dbContext.Payments
                    .Where(payment => payment.OrderId == message.OrderId)
                    .Select(payment => (Guid?)payment.Id)
                    .SingleOrDefaultAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new OrderPlacedProcessingResult(OrderPlacedProcessingOutcome.DuplicateEvent, duplicatePaymentId);
            }

            var payment = await dbContext.Payments
                .SingleOrDefaultAsync(candidate => candidate.OrderId == message.OrderId, cancellationToken);
            var outcome = payment is null
                ? OrderPlacedProcessingOutcome.Created
                : OrderPlacedProcessingOutcome.DuplicateOrder;

            if (payment is null)
            {
                payment = Payment.Create(
                    message.OrderId,
                    message.UserId,
                    message.GameId,
                    message.Price,
                    message.Currency,
                    correlationId: message.CorrelationId);
                await dbContext.Payments.AddAsync(payment, cancellationToken);
            }

            await dbContext.InboxMessages.AddAsync(
                new PaymentInboxMessage(message.EventId, ConsumerName, DateTimeOffset.UtcNow),
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new OrderPlacedProcessingResult(outcome, payment.Id);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            finally
            {
                dbContext.ChangeTracker.Clear();
            }

            throw;
        }
    }
}
