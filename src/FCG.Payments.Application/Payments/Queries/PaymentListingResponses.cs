namespace FCG.Payments.Application.Payments.Queries;

/// <summary>Payment information returned by the listing API.</summary>
public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Amount,
    string Currency,
    Guid? CorrelationId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Payment attempt information returned by the listing API.</summary>
public sealed record PaymentAttemptResponse(
    Guid Id,
    Guid PaymentId,
    int AttemptNumber,
    string Status,
    DateTimeOffset CreatedAt);

/// <summary>Outbox message and delivery state returned by the listing API.</summary>
public sealed record PaymentOutboxMessageResponse(
    Guid Id,
    Guid EventId,
    Guid PaymentId,
    string EventType,
    string Payload,
    DateTimeOffset OccurredAt,
    DateTimeOffset? PublishedAt,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? LeaseExpiresAt);
