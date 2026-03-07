using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

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

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("JobDbContext")
            ?? "Host=localhost;Database=jobservice;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);
        return new JobDbContext(optionsBuilder.Options);
    }
}
