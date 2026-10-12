namespace FCG.Payments.Application.Messaging;

public sealed record PaymentProcessedEvent
{
    public Guid EventId { get; init; }

    public Guid CorrelationId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public int Version { get; init; }

    public Guid PaymentId { get; init; }

    public Guid OrderId { get; init; }

    public Guid UserId { get; init; }

    public Guid GameId { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;
}
