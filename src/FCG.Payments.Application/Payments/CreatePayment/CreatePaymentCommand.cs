namespace FCG.Payments.Application.Payments.CreatePayment;

public sealed record CreatePaymentCommand(
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Amount,
    string Currency);
