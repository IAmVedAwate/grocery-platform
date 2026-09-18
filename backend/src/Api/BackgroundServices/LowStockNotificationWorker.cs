using Application.Notifications;

namespace Api.BackgroundServices;

/// <summary>
/// Periodic sweep (docs/ROADMAP.md Phase 3). Resolves a fresh DI scope per
/// iteration rather than holding one scoped GroceryDbContext for the
/// worker's lifetime — a long-lived DbContext accumulates tracked entities
/// and never sees data committed by other requests via a second-level
/// cache, neither of which is wanted for a job that's supposed to see the
/// current state of every tenant's inventory on every run.
/// </summary>
public sealed class LowStockNotificationWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<LowStockNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Read once at startup, not per-iteration — unlike the auth rate
        // limiter's per-request partitioner, nothing here needs to observe
        // a WebApplicationFactory test override made after the host is
        // already running; tests instead call ILowStockNotificationGenerator
        // directly rather than waiting on this loop's timer.
        var intervalSeconds = int.TryParse(configuration["LOW_STOCK_CHECK_INTERVAL_SECONDS"], out var seconds) ? seconds : 300;
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var generator = scope.ServiceProvider.GetRequiredService<ILowStockNotificationGenerator>();
                var created = await generator.GenerateAsync(stoppingToken);
                if (created > 0)
                    logger.LogInformation("Low-stock notification sweep created {Count} notification(s).", created);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A single failed sweep (e.g. a transient DB blip) must not
                // kill the worker for the rest of the process lifetime.
                logger.LogError(ex, "Low-stock notification sweep failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }
}
