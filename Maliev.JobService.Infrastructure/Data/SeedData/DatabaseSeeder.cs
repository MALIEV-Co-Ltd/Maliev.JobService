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
            var jobs = JobSeedData.GetAll().ToList();
            var existingIds = await context.Jobs
                .AsNoTracking()
                .Select(job => job.Id)
                .ToListAsync();
            var missingJobs = jobs
                .Where(job => !existingIds.Contains(job.Id))
                .ToList();

            if (missingJobs.Count == 0)
            {
                logger.LogInformation("All seeded manufacturing jobs already exist. Skipping seed.");
                return;
            }

            logger.LogInformation("Seeding manufacturing jobs...");

            var now = DateTime.UtcNow;
            foreach (var job in missingJobs)
            {
                job.CreatedAt = now;
                job.UpdatedAt = now;
            }

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await context.Database.BeginTransactionAsync();

                foreach (var job in missingJobs)
                {
                    await context.Jobs.AddAsync(job);
                }

                await context.SaveChangesAsync();
                await tx.CommitAsync();
            });

            logger.LogInformation("Seeded {Count} jobs.", missingJobs.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding jobs.");
        }
    }
}
