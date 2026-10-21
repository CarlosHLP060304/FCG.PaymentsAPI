using FCG.Payments.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;

namespace FCG.Payments.Api.Startup;

/// <summary>
/// Prepares an existing PostgreSQL database only for an explicitly enabled local demonstration.
/// Database/user creation belongs to the infrastructure repository.
/// </summary>
public static class LocalDemonstrationDatabase
{
    private const long MigrationLockId = 4670276501;

    public static bool IsEnabled(IConfiguration configuration, string environmentName)
    {
        var configuredValue = configuration["DemonstracaoLocal:Habilitada"];
        if (configuredValue is null)
        {
            return false;
        }

        if (!bool.TryParse(configuredValue, out var enabled))
        {
            throw new InvalidOperationException(
                "DemonstracaoLocal:Habilitada must be true or false.");
        }

        if (enabled && !string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Automatic Payments migrations are allowed only in Development with DemonstracaoLocal:Habilitada=true.");
        }

        return enabled;
    }

    public static async Task MigrateAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        await context.Database.OpenConnectionAsync(cancellationToken);
        var lockAcquired = false;

        try
        {
            // A session lock also serializes separate replicas sharing this Payments database.
            // Keep the connection open while EF Core manages its own migration transactions.
            await using var acquire = context.Database.GetDbConnection().CreateCommand();
            acquire.CommandText = $"SELECT pg_advisory_lock({MigrationLockId})";
            await acquire.ExecuteNonQueryAsync(cancellationToken);
            lockAcquired = true;

            logger.LogInformation("Applying pending Payments migrations for the local demonstration.");
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Payments database is ready for the local demonstration.");
        }
        finally
        {
            try
            {
                if (lockAcquired)
                {
                    await using var release = context.Database.GetDbConnection().CreateCommand();
                    release.CommandText = $"SELECT pg_advisory_unlock({MigrationLockId})";
                    await release.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }
}
