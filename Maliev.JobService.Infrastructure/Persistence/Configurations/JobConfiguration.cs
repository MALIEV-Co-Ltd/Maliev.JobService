using Maliev.JobService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.JobService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for <see cref="Job"/>.
/// </summary>
public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs", "public");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(j => j.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(j => j.OrderItemId)
            .HasColumnName("order_item_id")
            .IsRequired();

        builder.Property(j => j.MaterialId)
            .HasColumnName("material_id")
            .IsRequired();

        builder.Property(j => j.Technology)
            .HasColumnName("technology")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(j => j.VolumeCm3)
            .HasColumnName("volume_cm3")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(j => j.EstimatedPrintTimeMinutes)
            .HasColumnName("estimated_print_time_minutes")
            .IsRequired();

        builder.Property(j => j.AssignedMachineId)
            .HasColumnName("assigned_machine_id")
            .HasMaxLength(100);

        builder.Property(j => j.Priority)
            .HasColumnName("priority")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(j => j.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(j => j.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.Property(j => j.ScheduledStartTime)
            .HasColumnName("scheduled_start_time");

        builder.Property(j => j.ScheduledEndTime)
            .HasColumnName("scheduled_end_time");

        builder.Property(j => j.SetupTimeMinutes)
            .HasColumnName("setup_time_minutes")
            .IsRequired();

        builder.Property(j => j.QueuePosition)
            .HasColumnName("queue_position")
            .IsRequired();

        builder.Property(j => j.StartedAt)
            .HasColumnName("started_at");

        builder.Property(j => j.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(j => j.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(j => j.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // xmin concurrency token
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.HasIndex(j => j.Status)
            .HasDatabaseName("ix_jobs_status");

        builder.HasIndex(j => j.OrderId)
            .HasDatabaseName("ix_jobs_order_id");

        builder.HasIndex(j => j.AssignedMachineId)
            .HasDatabaseName("ix_jobs_assigned_machine_id");
    }
}
