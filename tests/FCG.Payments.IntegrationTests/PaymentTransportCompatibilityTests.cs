using System.Text.Json;
using System.Text.Json.Serialization;
using FCG.Payments.Api.Messaging;
using FCG.Payments.Application.Messaging;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FCG.Payments.IntegrationTests;

/// <summary>
/// Exercita o publicador de produção, a serialização JSON e um consumidor com
/// outro tipo CLR, que representa o contrato esperado pelo Notifications.
/// O transporte em memória não valida bindings ou confirmações do RabbitMQ.
/// </summary>
public sealed class PaymentTransportCompatibilityTests
{
    private const string NotificationsUrn =
        "urn:message:FCG.Notifications.Application.Messaging.Contracts:PaymentProcessedEvent";

    [Theory]
    [InlineData("Approved", 49.90, NotificationPaymentStatus.Approved)]
    [InlineData("Rejected", 149.90, NotificationPaymentStatus.Rejected)]
    public async Task Publisher_delivers_json_envelope_to_independent_notifications_contract(
        string status,
        decimal amount,
        NotificationPaymentStatus expectedStatus)
    {
        var services = new ServiceCollection();
        services.AddSingleton<NotificationReceiptProbe>();
        services.AddScoped<IPaymentProcessedEventPublisher, MassTransitPaymentProcessedEventPublisher>();
        services.AddMassTransitTestHarness(registration =>
        {
            registration.SetTestTimeouts(
                testTimeout: TimeSpan.FromSeconds(20),
                testInactivityTimeout: TimeSpan.FromSeconds(5));
            registration.AddConsumer<NotificationsContractConsumer>();
            registration.UsingInMemory((context, bus) =>
            {
                // Serialização real: o consumidor não recebe a instância CLR original.
                bus.UseJsonSerializer();
                bus.Message<PaymentProcessedIntegrationEvent>(m => m.SetEntityName("PaymentProcessedEvent"));
                bus.Message<NotificationPaymentProcessedContract>(m => m.SetEntityName("PaymentProcessedEvent"));
                bus.ConfigureEndpoints(context);
            });
        });

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var original = new PaymentProcessedEvent
            {
                EventId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.Parse("2026-10-18T15:30:00+00:00"),
                Version = 1,
                PaymentId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                GameId = Guid.NewGuid(),
                Amount = amount,
                Currency = "BRL",
                Status = status
            };

            await using (var scope = provider.CreateAsyncScope())
            {
                var publisher = scope.ServiceProvider.GetRequiredService<IPaymentProcessedEventPublisher>();
                await publisher.PublishAsync(original);
            }

            var receipt = await provider.GetRequiredService<NotificationReceiptProbe>()
                .Received.Task.WaitAsync(TimeSpan.FromSeconds(15));

            // O consumidor usa um enum e um tipo CLR diferentes do produtor.
            var actual = receipt.Message;
            Assert.Equal(original.EventId, actual.EventId);
            Assert.Equal(original.CorrelationId, actual.CorrelationId);
            Assert.Equal(original.OccurredAt, actual.OccurredAt);
            Assert.Equal(original.Version, actual.Version);
            Assert.Equal(original.PaymentId, actual.PaymentId);
            Assert.Equal(original.OrderId, actual.OrderId);
            Assert.Equal(original.UserId, actual.UserId);
            Assert.Equal(original.GameId, actual.GameId);
            Assert.Equal(original.Amount, actual.Amount);
            Assert.Equal(original.Currency, actual.Currency);
            Assert.Equal(expectedStatus, actual.Status);

            Assert.Equal(original.EventId, receipt.MessageId);
            Assert.Equal(original.CorrelationId, receipt.CorrelationId);
            Assert.Equal("application/vnd.masstransit+json", receipt.ContentType);
            Assert.Contains(NotificationsUrn, receipt.SupportedMessageTypes);

            // Verifica os bytes efetivamente recebidos: JSON direto ou URN antigo
            // não satisfazem este contrato mesmo que os campos isolados coincidam.
            using var envelope = JsonDocument.Parse(receipt.EnvelopeJson);
            var root = envelope.RootElement;
            Assert.Equal(original.EventId, root.GetProperty("messageId").GetGuid());
            Assert.Equal(original.CorrelationId, root.GetProperty("correlationId").GetGuid());
            var wireTypes = root.GetProperty("messageType").EnumerateArray()
                .Select(value => value.GetString()).ToArray();
            Assert.Contains(NotificationsUrn, wireTypes);
            Assert.DoesNotContain(
                "urn:message:FCG.Payments.Application.Messaging:PaymentProcessedEvent", wireTypes);
            var wireMessage = root.GetProperty("message");
            Assert.Equal(original.EventId, wireMessage.GetProperty("eventId").GetGuid());
            Assert.Equal(original.CorrelationId, wireMessage.GetProperty("correlationId").GetGuid());
            var wireAmount = wireMessage.GetProperty("amount");
            // MassTransit 8 pode representar decimal como string para preservar precisao.
            var amountText = wireAmount.ValueKind == JsonValueKind.String ? wireAmount.GetString()! : wireAmount.GetRawText();
            Assert.Equal(original.Amount, decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(status, wireMessage.GetProperty("status").GetString());
        }
        finally
        {
            await harness.Stop();
        }
    }

    // Fixture independente: reproduz o contrato externo de consumo sem referência
    // ao projeto Notifications e sem reutilizar o DTO de saída do Payments.
    [MessageUrn("FCG.Notifications.Application.Messaging.Contracts:PaymentProcessedEvent")]
    public sealed record NotificationPaymentProcessedContract(
        Guid EventId,
        Guid CorrelationId,
        DateTimeOffset OccurredAt,
        int Version,
        Guid PaymentId,
        Guid OrderId,
        Guid UserId,
        Guid GameId,
        decimal Amount,
        string Currency,
        NotificationPaymentStatus Status);

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NotificationPaymentStatus
    {
        Approved,
        Rejected
    }

    public sealed record NotificationReceipt(
        NotificationPaymentProcessedContract Message,
        Guid? MessageId,
        Guid? CorrelationId,
        string ContentType,
        string[] SupportedMessageTypes,
        string EnvelopeJson);

    public sealed class NotificationReceiptProbe
    {
        public TaskCompletionSource<NotificationReceipt> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class NotificationsContractConsumer(NotificationReceiptProbe probe)
        : IConsumer<NotificationPaymentProcessedContract>
    {
        public async Task Consume(ConsumeContext<NotificationPaymentProcessedContract> context)
        {
            using var reader = new StreamReader(context.ReceiveContext.GetBodyStream());
            var envelopeJson = await reader.ReadToEndAsync(context.CancellationToken);
            probe.Received.TrySetResult(new NotificationReceipt(
                context.Message,
                context.MessageId,
                context.CorrelationId,
                context.ReceiveContext.ContentType.MediaType ?? string.Empty,
                context.SupportedMessageTypes.ToArray(),
                envelopeJson));
        }
    }
}
