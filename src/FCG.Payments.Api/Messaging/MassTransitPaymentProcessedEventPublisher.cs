using FCG.Payments.Application.Messaging;
using MassTransit;

namespace FCG.Payments.Api.Messaging;

public sealed class MassTransitPaymentProcessedEventPublisher(IPublishEndpoint publishEndpoint)
    : IPaymentProcessedEventPublisher
{
    public Task PublishAsync(PaymentProcessedEvent message, CancellationToken cancellationToken = default) =>
        publishEndpoint.Publish(message, cancellationToken);
}
