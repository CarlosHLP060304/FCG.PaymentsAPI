using FCG.Payments.Infrastructure.Messaging;

namespace FCG.Payments.Api.Messaging;

public sealed class PaymentOutboxBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentOutboxBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<PaymentOutboxDispatcher>();
                await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Payment outbox polling failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
