using System.Text.Json;
using FCG.Payments.Application.Messaging;

namespace FCG.Payments.Infrastructure.Messaging;

public sealed class PaymentOutboxMessage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private PaymentOutboxMessage()
    {
        EventType = string.Empty;
        Payload = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public Guid PaymentId { get; private set; }

    public string EventType { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public int Attempts { get; private set; }

    public DateTime NextAttemptAt { get; private set; }

    public Guid? LeaseId { get; private set; }

    public DateTime? LeaseExpiresAt { get; private set; }

    public static PaymentOutboxMessage Create(PaymentProcessedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new PaymentOutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = message.EventId,
            PaymentId = message.PaymentId,
            EventType = typeof(PaymentProcessedEvent).FullName!,
            Payload = JsonSerializer.Serialize(message, JsonOptions),
            OccurredAt = message.OccurredAt,
            NextAttemptAt = message.OccurredAt.UtcDateTime
        };
    }

    public PaymentProcessedEvent DeserializeEvent() =>
        JsonSerializer.Deserialize<PaymentProcessedEvent>(Payload, JsonOptions)
        ?? throw new InvalidOperationException("The PaymentProcessedEvent outbox payload is empty.");
}
