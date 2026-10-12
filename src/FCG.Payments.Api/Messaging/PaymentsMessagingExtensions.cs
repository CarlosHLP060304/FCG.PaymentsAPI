using FCG.Payments.Application.Messaging;
using MassTransit;

namespace FCG.Payments.Api.Messaging;

public static class PaymentsMessagingExtensions
{
    public static IServiceCollection AddPaymentsMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (!configuration.GetValue("RabbitMq:Enabled", true))
        {
            return services;
        }

        var rabbitMq = configuration.GetSection("RabbitMq");
        var hostAddress = rabbitMq["Host"] ?? "rabbitmq://localhost";
        var username = rabbitMq["Username"] ?? "kalo";
        var password = rabbitMq["Password"] ?? "kalo";

        services.AddMassTransit(registration =>
        {
            registration.AddConsumer<OrderPlacedConsumer>();
            registration.UsingRabbitMq((context, bus) =>
            {
                bus.Host(new Uri(hostAddress), host =>
                {
                    host.Username(username);
                    host.Password(password);
                });

                bus.Message<PaymentProcessedEvent>(message =>
                    message.SetEntityName("PaymentProcessedEvent"));

                bus.ReceiveEndpoint(PaymentMessageTestEndpoints.OrderPlacedQueue, endpoint =>
                {
                    endpoint.PrefetchCount = 16;
                    endpoint.UseMessageRetry(retry =>
                    {
                        retry.Ignore<InvalidOrderPlacedEventException>();
                        retry.Intervals(
                            TimeSpan.FromSeconds(1),
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromSeconds(15));
                    });
                    endpoint.ConfigureConsumer<OrderPlacedConsumer>(context);
                });
            });
        });

        services.AddScoped<IPaymentProcessedEventPublisher, MassTransitPaymentProcessedEventPublisher>();
        services.AddHostedService<PaymentOutboxBackgroundService>();

        return services;
    }
}
