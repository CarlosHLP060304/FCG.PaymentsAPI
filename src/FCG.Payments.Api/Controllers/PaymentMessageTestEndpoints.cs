using FCG.Payments.Application.Messaging;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Payments.Api.Messaging;

/// <summary>Registers development-only endpoints for testing payment message queues.</summary>
public static class PaymentMessageTestEndpoints
{
    public const string OrderPlacedQueue = "payments-order-placed";
    public const string OrderPlacedErrorQueue = "payments-order-placed_error";

    /// <summary>Maps endpoints that send test OrderPlacedEvent messages to the payment queues.</summary>
    public static WebApplication MapPaymentMessageTestEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dev/messages")
            .WithTags("Payment message testing");

        group.MapPost("/order-placed", (
                OrderPlacedEvent message,
                ISendEndpointProvider sendEndpointProvider,
                CancellationToken cancellationToken) =>
                SendAsync(message, OrderPlacedQueue, sendEndpointProvider, cancellationToken))
            .WithName("SendOrderPlacedTestMessage")
            .WithSummary("Send an OrderPlacedEvent to the payment consumer queue.")
            .Produces<PaymentTestMessageSentResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPost("/order-placed-error", (
                OrderPlacedEvent message,
                ISendEndpointProvider sendEndpointProvider,
                CancellationToken cancellationToken) =>
                SendAsync(message, OrderPlacedErrorQueue, sendEndpointProvider, cancellationToken))
            .WithName("SendOrderPlacedErrorTestMessage")
            .WithSummary("Send an OrderPlacedEvent directly to the MassTransit error queue.")
            .Produces<PaymentTestMessageSentResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<Results<Accepted<PaymentTestMessageSentResponse>, BadRequest<ProblemDetails>>> SendAsync(
        OrderPlacedEvent message,
        string queueName,
        ISendEndpointProvider sendEndpointProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            OrderPlacedEventValidator.Validate(message);
        }
        catch (InvalidOrderPlacedEventException exception)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid OrderPlacedEvent",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }

        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri($"queue:{queueName}"));
        await endpoint.Send(message, cancellationToken);

        return TypedResults.Accepted<PaymentTestMessageSentResponse>(
            uri: (string?)null,
            value: new PaymentTestMessageSentResponse(
                queueName,
                message.EventId,
                message.CorrelationId,
                message.OrderId));
    }
}

/// <summary>Confirms the queue and identifiers used for a sent test event.</summary>
public sealed record PaymentTestMessageSentResponse(
    string Queue,
    Guid EventId,
    Guid CorrelationId,
    Guid OrderId);
