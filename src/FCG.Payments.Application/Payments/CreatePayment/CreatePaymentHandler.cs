using FCG.Payments.Application.Abstractions.Repositories;
using FCG.Payments.Domain.Payments;

namespace FCG.Payments.Application.Payments.CreatePayment;

public sealed class CreatePaymentHandler(IPaymentRepository paymentRepository)
{
    public async Task<Payment> HandleAsync(
        CreatePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existingPayment = await paymentRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);
        if (existingPayment is not null)
        {
            return existingPayment;
        }

        var payment = Payment.Create(
            command.OrderId,
            command.UserId,
            command.GameId,
            command.Amount,
            command.Currency);

        await paymentRepository.AddAsync(payment, cancellationToken);
        await paymentRepository.SaveChangesAsync(cancellationToken);
        return payment;
    }
}
