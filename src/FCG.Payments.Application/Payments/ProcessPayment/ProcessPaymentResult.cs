using FCG.Payments.Domain.Payments;

namespace FCG.Payments.Application.Payments.ProcessPayment;

public sealed record ProcessPaymentResult(
    bool WasProcessed,
    Guid PaymentId,
    Guid OrderId,
    Guid? CorrelationId,
    PaymentStatus? PreviousStatus,
    PaymentStatus CurrentStatus);
