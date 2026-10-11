using Microsoft.Extensions.DependencyInjection;
using FCG.Payments.Application.Payments.CreatePayment;
using FCG.Payments.Application.Payments.UpdatePaymentStatus;
using FCG.Payments.Application.Payments.ProcessPayment;

namespace FCG.Payments.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsApplication(this IServiceCollection services)
    {
        services.AddScoped<CreatePaymentHandler>();
        services.AddScoped<UpdatePaymentStatusHandler>();
        services.AddScoped<ProcessPaymentHandler>();
        return services;
    }
}
