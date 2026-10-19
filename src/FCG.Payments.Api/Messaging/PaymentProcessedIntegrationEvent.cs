using FCG.Payments.Application.Messaging;
using MassTransit;

namespace FCG.Payments.Api.Messaging;

// O contrato de transporte possui identidade publica estavel, independente do namespace interno.
// Mantem MassTransit fora da Application e preserva o formato consumido por Catalog/Notifications.
[MessageUrn("FCG.Notifications.Application.Messaging.Contracts:PaymentProcessedEvent")]
public sealed record PaymentProcessedIntegrationEvent
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

    public static PaymentProcessedIntegrationEvent From(PaymentProcessedEvent message) => new()
    {
        EventId = message.EventId,
        CorrelationId = message.CorrelationId,
        OccurredAt = message.OccurredAt,
        Version = message.Version,
        PaymentId = message.PaymentId,
        OrderId = message.OrderId,
        UserId = message.UserId,
        GameId = message.GameId,
        Amount = message.Amount,
        Currency = message.Currency,
        Status = message.Status
    };
}
