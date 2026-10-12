namespace FCG.Payments.Application.Messaging;

public static class OrderPlacedEventValidator
{
    public static void Validate(OrderPlacedEvent? message)
    {
        if (message is null ||
            message.EventId == Guid.Empty ||
            message.CorrelationId == Guid.Empty ||
            message.OccurredAt == default ||
            message.Version <= 0 ||
            message.OrderId == Guid.Empty ||
            message.UserId == Guid.Empty ||
            message.GameId == Guid.Empty ||
            message.Price <= 0 ||
            !IsCurrencyCode(message.Currency))
        {
            throw new InvalidOrderPlacedEventException(
                "OrderPlacedEvent is missing required fields or contains invalid financial data.");
        }
    }

    private static bool IsCurrencyCode(string? currency) =>
        currency is { Length: 3 } && currency.All(char.IsAsciiLetter);
}

public sealed class InvalidOrderPlacedEventException(string message) : Exception(message);
