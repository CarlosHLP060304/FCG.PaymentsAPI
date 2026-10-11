namespace FCG.Payments.Domain.Payments;

public sealed class Payment
{
    private readonly List<PaymentAttempt> _attempts = [];

    private Payment()
    {
        Currency = string.Empty;
    }

    private Payment(Guid orderId, Guid userId, Guid gameId, decimal amount, string currency, Guid? correlationId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        UserId = userId;
        GameId = gameId;
        Amount = amount;
        Currency = currency;
        CorrelationId = correlationId;
        Status = PaymentStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    // These identifiers belong to other services; no external database relationships are modeled.
    public Guid OrderId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid GameId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public Guid? CorrelationId { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<PaymentAttempt> Attempts => _attempts;

    public static Payment Create(
        Guid orderId,
        Guid userId,
        Guid gameId,
        decimal amount,
        string currency,
        DateTimeOffset? createdAt = null,
        Guid? correlationId = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId must not be empty.", nameof(orderId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        }

        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("GameId must not be empty.", nameof(gameId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3 || normalizedCurrency.Any(character => !char.IsAsciiLetter(character)))
        {
            throw new ArgumentException("Currency must be a three-letter ISO currency code.", nameof(currency));
        }

        return new Payment(orderId, userId, gameId, amount, normalizedCurrency, correlationId, createdAt ?? DateTimeOffset.UtcNow);
    }

    public void UpdateStatus(PaymentStatus status, DateTimeOffset? updatedAt = null)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException($"A payment in status {Status} cannot be changed.");
        }

        if (status is not (PaymentStatus.Approved or PaymentStatus.Rejected))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "A pending payment can only be approved or rejected.");
        }

        Status = status;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }

    public PaymentAttempt AddAttempt(PaymentStatus status, string? errorMessage = null, DateTimeOffset? createdAt = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        var attempt = PaymentAttempt.Create(Id, _attempts.Count + 1, status, errorMessage, createdAt);
        _attempts.Add(attempt);
        return attempt;
    }
}
