using Maliev.JobService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.JobService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for <see cref="ProductionPlanningHold"/>.
/// </summary>
public sealed class ProductionPlanningHoldConfiguration : IEntityTypeConfiguration<ProductionPlanningHold>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProductionPlanningHold> builder)
    {
        builder.ToTable("production_planning_holds", "public");

        builder.HasKey(hold => hold.Id);

        builder.Property(hold => hold.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(hold => hold.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(hold => hold.ProjectPartId)
            .HasColumnName("project_part_id")
            .IsRequired();

        builder.Property(hold => hold.Technology)
            .HasColumnName("technology")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(hold => hold.MachineId)
            .HasColumnName("machine_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(hold => hold.MachineName)
            .HasColumnName("machine_name")
            .HasMaxLength(200);

        builder.Property(hold => hold.QueuePosition)
            .HasColumnName("queue_position")
            .IsRequired();

        builder.Property(hold => hold.ScheduledStartTime)
            .HasColumnName("scheduled_start_time")
            .IsRequired();

        builder.Property(hold => hold.ScheduledEndTime)
            .HasColumnName("scheduled_end_time")
            .IsRequired();

        builder.Property(hold => hold.SetupTimeMinutes)
            .HasColumnName("setup_time_minutes")
            .IsRequired();

        builder.Property(hold => hold.ProductionTimeMinutes)
            .HasColumnName("production_time_minutes")
            .IsRequired();

        builder.Property(hold => hold.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.Property(hold => hold.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(hold => hold.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.Property(hold => hold.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(hold => hold.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(hold => hold.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(hold => hold.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(hold => hold.ConvertedJobId)
            .HasColumnName("converted_job_id");

        builder.HasIndex(hold => new { hold.ProjectId, hold.ProjectPartId })
            .HasDatabaseName("ix_production_planning_holds_project_part");

        builder.HasIndex(hold => new { hold.MachineId, hold.Status, hold.ScheduledStartTime })
            .HasDatabaseName("ix_production_planning_holds_machine_status_start");

        builder.HasIndex(hold => new { hold.Technology, hold.Status })
            .HasDatabaseName("ix_production_planning_holds_technology_status");

        builder.HasIndex(hold => hold.ExpiresAt)
            .HasDatabaseName("ix_production_planning_holds_expires_at");

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
    }
}
