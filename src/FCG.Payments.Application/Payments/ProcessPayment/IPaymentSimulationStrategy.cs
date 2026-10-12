using FCG.Payments.Domain.Payments;

namespace FCG.Payments.Application.Payments.ProcessPayment;

public interface IPaymentSimulationStrategy
{
    PaymentStatus Decide(Payment payment);
}
