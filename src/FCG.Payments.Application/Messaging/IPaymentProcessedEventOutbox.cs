namespace FCG.Payments.Application.Messaging;

public interface IPaymentProcessedEventOutbox
{
    void Enqueue(PaymentProcessedEvent message);
}
