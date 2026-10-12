namespace FCG.Payments.Infrastructure.Messaging;

public sealed class PaymentInboxMessage
{
    private PaymentInboxMessage()
    {
        ConsumerName = string.Empty;
    }

    public PaymentInboxMessage(Guid eventId, string consumerName, DateTimeOffset processedAt)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        ConsumerName = consumerName;
        ProcessedAt = processedAt;
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string ConsumerName { get; private set; }

    public DateTimeOffset ProcessedAt { get; private set; }
}
