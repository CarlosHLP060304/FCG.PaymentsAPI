namespace FCG.Payments.Application.Payments.Queries;

public interface IPaymentListingService
{
    Task<IReadOnlyList<PaymentResponse>> ListPaymentsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentAttemptResponse>> ListPaymentAttemptsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentOutboxMessageResponse>> ListOutboxMessagesAsync(CancellationToken cancellationToken = default);
}
