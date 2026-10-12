using FCG.Payments.Application.Abstractions.Repositories;
using FCG.Payments.Domain.Payments;

namespace FCG.Payments.Application.Payments.UpdatePaymentStatus;

public sealed class UpdatePaymentStatusHandler(IPaymentRepository paymentRepository)
{
    public async Task<Payment> HandleAsync(
        UpdatePaymentStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payment = await paymentRepository.GetByIdAsync(command.PaymentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment '{command.PaymentId}' was not found.");

        payment.UpdateStatus(command.Status);
        await paymentRepository.SaveChangesAsync(cancellationToken);
        return payment;
    }
}
