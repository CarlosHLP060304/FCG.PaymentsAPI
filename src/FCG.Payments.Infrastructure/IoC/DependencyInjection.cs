using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FCG.Payments.Application.Abstractions.Repositories;
using FCG.Payments.Infrastructure.Data.EF.Context;
using FCG.Payments.Infrastructure.Repositories;
using FCG.Payments.Application.Messaging;
using FCG.Payments.Infrastructure.Messaging;
using FCG.Payments.Application.Payments.ProcessPayment;
using FCG.Payments.Infrastructure.Payments;
using FCG.Payments.Application.Payments.Queries;

namespace FCG.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PaymentsDatabase") ?? string.Empty;

        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(connectionString, postgres =>
                postgres.MigrationsAssembly(typeof(PaymentsDbContext).Assembly.FullName)));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentListingService, PaymentListingService>();
        services.AddScoped<IOrderPlacedEventProcessor, OrderPlacedEventProcessor>();
        services.AddScoped<IPaymentProcessedEventOutbox, PaymentProcessedEventOutbox>();
        services.AddScoped<PaymentOutboxDispatcher>();
        var configuredApprovalLimit = configuration["PaymentSimulation:ApprovalLimit"];
        var approvalLimit = string.IsNullOrWhiteSpace(configuredApprovalLimit)
            ? 100m
            : decimal.TryParse(configuredApprovalLimit, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedLimit)
                ? parsedLimit
                : throw new InvalidOperationException("PaymentSimulation:ApprovalLimit must be a valid decimal number.");
        if (approvalLimit <= 0)
        {
            throw new InvalidOperationException("PaymentSimulation:ApprovalLimit must be greater than zero.");
        }

        services.AddSingleton<IPaymentSimulationStrategy>(
            new AmountBasedPaymentSimulationStrategy(approvalLimit));
        return services;
    }
}
