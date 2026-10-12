using FCG.Payments.Application.Messaging;
using FCG.Payments.Infrastructure.Data.EF.Context;

namespace FCG.Payments.Infrastructure.Messaging;

public sealed class PaymentProcessedEventOutbox(PaymentsDbContext dbContext) : IPaymentProcessedEventOutbox
{
    public void Enqueue(PaymentProcessedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);
        dbContext.OutboxMessages.Add(PaymentOutboxMessage.Create(message));
    }
}
