using Microsoft.EntityFrameworkCore;
using Maliev.JobService.Infrastructure.Persistence.Configurations;
using Maliev.JobService.Domain.Entities;
using MassTransit;

namespace Maliev.JobService.Infrastructure.Persistence;

/// <summary>
/// Database context for the Job Service.
/// </summary>
public class JobDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobDbContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    public JobDbContext(DbContextOptions<JobDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the database set for jobs.
    /// </summary>
    public DbSet<Job> Jobs => Set<Job>();

    /// <summary>
    /// Gets or sets the database set for job status transition audit records.
    /// </summary>
    public DbSet<JobStatusTransitionAudit> JobStatusTransitionAudits => Set<JobStatusTransitionAudit>();

    /// <summary>
    /// Gets or sets the database set for tentative production planning holds.
    /// </summary>
    public DbSet<ProductionPlanningHold> ProductionPlanningHolds => Set<ProductionPlanningHold>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new JobConfiguration());
        modelBuilder.ApplyConfiguration(new JobStatusTransitionAuditConfiguration());
        modelBuilder.ApplyConfiguration(new ProductionPlanningHoldConfiguration());

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
