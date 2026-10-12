using FCG.Payments.Application.Payments.ProcessPayment;
using FCG.Payments.Domain.Payments;

namespace FCG.Payments.Infrastructure.Payments;

public sealed class AmountBasedPaymentSimulationStrategy(decimal approvalLimit) : IPaymentSimulationStrategy
{
    public PaymentStatus Decide(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return payment.Amount <= approvalLimit
            ? PaymentStatus.Approved
            : PaymentStatus.Rejected;
    }
}
