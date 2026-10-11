namespace FCG.Payments.Domain.Payments;

public sealed class PaymentAttempt
{
    private PaymentAttempt()
    {
    }

    private PaymentAttempt(Guid paymentId, int attemptNumber, PaymentStatus status, string? errorMessage, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        PaymentId = paymentId;
        AttemptNumber = attemptNumber;
        Status = status;
        ErrorMessage = errorMessage;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid PaymentId { get; private set; }

    public int AttemptNumber { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    internal static PaymentAttempt Create(
        Guid paymentId,
        int attemptNumber,
        PaymentStatus status,
        string? errorMessage,
        DateTimeOffset? createdAt = null)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("PaymentId must not be empty.", nameof(paymentId));
        }

        if (attemptNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new PaymentAttempt(paymentId, attemptNumber, status, errorMessage, createdAt ?? DateTimeOffset.UtcNow);
    }
}
