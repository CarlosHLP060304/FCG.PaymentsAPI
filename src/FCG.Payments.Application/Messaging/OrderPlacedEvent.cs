namespace FCG.Payments.Application.Messaging;

public sealed record OrderPlacedEvent
{
    public Guid EventId { get; init; }

    public Guid CorrelationId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public int Version { get; init; }

    public Guid OrderId { get; init; }

    public Guid UserId { get; init; }

    public Guid GameId { get; init; }

    public decimal Price { get; init; }

    public string Currency { get; init; } = string.Empty;
}
