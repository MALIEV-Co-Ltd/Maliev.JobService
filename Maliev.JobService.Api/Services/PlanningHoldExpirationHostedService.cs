using Maliev.JobService.Application.Abstractions;

namespace Maliev.JobService.Api.Services;

/// <summary>
/// Periodically expires tentative production planning holds.
/// </summary>
public sealed class PlanningHoldExpirationHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlanningHoldExpirationHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanningHoldExpirationHostedService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory.</param>
    /// <param name="logger">The logger.</param>
    public PlanningHoldExpirationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<PlanningHoldExpirationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireOnceAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to expire production planning holds.");
            }
        }
    }

    private async Task ExpireOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
        await jobService.ExpirePlanningHoldsAsync(DateTime.UtcNow, cancellationToken);
    }
}
