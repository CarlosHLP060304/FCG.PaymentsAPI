using FCG.Payments.Application.Payments.Queries;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FCG.Payments.Api.Payments;

public static class PaymentListingEndpoints
{
    public static WebApplication MapPaymentListingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api")
            .WithTags("Payment listings");

        group.MapGet("/payments", async Task<Ok<IReadOnlyList<PaymentResponse>>> (
                IPaymentListingService listingService,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await listingService.ListPaymentsAsync(cancellationToken)))
            .WithName("ListPayments")
            .WithSummary("List recorded payments")
            .WithDescription("Returns payments ordered from newest to oldest.")
            .Produces<IReadOnlyList<PaymentResponse>>(StatusCodes.Status200OK);

        group.MapGet("/payment-attempts", async Task<Ok<IReadOnlyList<PaymentAttemptResponse>>> (
                IPaymentListingService listingService,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await listingService.ListPaymentAttemptsAsync(cancellationToken)))
            .WithName("ListPaymentAttempts")
            .WithSummary("List payment processing attempts")
            .WithDescription("Returns payment attempts ordered from newest to oldest.")
            .Produces<IReadOnlyList<PaymentAttemptResponse>>(StatusCodes.Status200OK);

        group.MapGet("/payment-outbox", async Task<Ok<IReadOnlyList<PaymentOutboxMessageResponse>>> (
                IPaymentListingService listingService,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await listingService.ListOutboxMessagesAsync(cancellationToken)))
            .WithName("ListPaymentOutboxMessages")
            .WithSummary("List payment outbox messages")
            .WithDescription("Returns payment publication events and their delivery state, newest first.")
            .Produces<IReadOnlyList<PaymentOutboxMessageResponse>>(StatusCodes.Status200OK);

        return app;
    }
}
