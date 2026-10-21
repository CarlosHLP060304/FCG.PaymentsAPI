using FCG.Payments.Application;
using FCG.Payments.Api.Messaging;
using FCG.Payments.Api.Payments;
using FCG.Payments.Api.Startup;
using FCG.Payments.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var localDemonstrationEnabled = LocalDemonstrationDatabase.IsEnabled(
    builder.Configuration, builder.Environment.EnvironmentName);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddPaymentsApplication();
builder.Services.AddPaymentsInfrastructure(builder.Configuration);
builder.Services.AddPaymentsMessaging(builder.Configuration);

var app = builder.Build();

if (localDemonstrationEnabled)
{
    await LocalDemonstrationDatabase.MigrateAsync(
        app.Services, app.Logger, app.Lifetime.ApplicationStopping);
}

app.MapPaymentListingEndpoints();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    if (builder.Configuration.GetValue("RabbitMq:Enabled", true))
    {
        app.MapPaymentMessageTestEndpoints();
    }
}

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => !string.Equals(check.Name, "masstransit-bus", StringComparison.OrdinalIgnoreCase)
});
app.MapHealthChecks("/health/ready");

await app.RunAsync();

public partial class Program { }
