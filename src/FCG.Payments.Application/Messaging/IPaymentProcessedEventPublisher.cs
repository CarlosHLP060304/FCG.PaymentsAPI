namespace FCG.Payments.Application.Messaging;

public interface IPaymentProcessedEventPublisher
{
    Task PublishAsync(PaymentProcessedEvent message, CancellationToken cancellationToken = default);
}
