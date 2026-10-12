using FCG.Payments.Domain.Payments;

namespace FCG.Payments.Application.Payments.UpdatePaymentStatus;

public sealed record UpdatePaymentStatusCommand(Guid PaymentId, PaymentStatus Status);
