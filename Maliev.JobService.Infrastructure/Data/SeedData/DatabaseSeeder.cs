using Maliev.JobService.Domain.Entities;
using Maliev.JobService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.JobService.Infrastructure.Data.SeedData;

public static class DatabaseSeeder
{
    public static async Task SeedJobsAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("JobDatabaseSeeder");

        try
        {
            if (await context.Jobs.AnyAsync())
            {
                logger.LogInformation("Jobs table already has data. Skipping seed.");
                return;
            }

            logger.LogInformation("Seeding manufacturing jobs...");

            var jobs = JobSeedData.GetAll().ToList();
            var now = DateTime.UtcNow;
            foreach (var job in jobs)
            {
                job.CreatedAt = now;
                job.UpdatedAt = now;
            }

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await context.Database.BeginTransactionAsync();

                foreach (var job in jobs)
                {
                    await context.Jobs.AddAsync(job);
                }

                await context.SaveChangesAsync();
                await tx.CommitAsync();
            });

            logger.LogInformation("Seeded {Count} jobs.", jobs.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding jobs.");
        }
    }
}