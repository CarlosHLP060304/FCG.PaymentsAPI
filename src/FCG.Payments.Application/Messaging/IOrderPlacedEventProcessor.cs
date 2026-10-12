namespace FCG.Payments.Application.Messaging;

public interface IOrderPlacedEventProcessor
{
    Task<OrderPlacedProcessingResult> ProcessAsync(
        OrderPlacedEvent message,
        CancellationToken cancellationToken = default);
}

public enum OrderPlacedProcessingOutcome
{
    Created,
    DuplicateEvent,
    DuplicateOrder
}

public sealed record OrderPlacedProcessingResult(OrderPlacedProcessingOutcome Outcome, Guid? PaymentId);
