using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.JobService.Infrastructure.Persistence;

/// <summary>
/// Factory for creating <see cref="JobDbContext"/> instances at design time (e.g., for EF Core migrations).
/// </summary>
public class JobDbContextFactory : IDesignTimeDbContextFactory<JobDbContext>
{
    /// <inheritdoc />
    public JobDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<JobDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=jobservice;Username=postgres;Password=postgres");
        return new JobDbContext(optionsBuilder.Options);
    }
}
