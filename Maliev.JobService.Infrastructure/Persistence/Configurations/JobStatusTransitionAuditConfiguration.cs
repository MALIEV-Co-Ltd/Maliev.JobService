using Maliev.JobService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.JobService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for <see cref="JobStatusTransitionAudit"/>.
/// </summary>
public class JobStatusTransitionAuditConfiguration : IEntityTypeConfiguration<JobStatusTransitionAudit>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<JobStatusTransitionAudit> builder)
    {
        builder.ToTable("job_status_transition_audits", "public");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(audit => audit.JobId)
            .HasColumnName("job_id")
            .IsRequired();

        builder.Property(audit => audit.PreviousStatus)
            .HasColumnName("previous_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(audit => audit.NewStatus)
            .HasColumnName("new_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(audit => audit.ChangedBy)
            .HasColumnName("changed_by")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(audit => audit.ChangedAtUtc)
            .HasColumnName("changed_at_utc")
            .IsRequired();

        builder.Property(audit => audit.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.HasOne(audit => audit.Job)
            .WithMany()
            .HasForeignKey(audit => audit.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(audit => audit.JobId)
            .HasDatabaseName("ix_job_status_transition_audits_job_id");

        builder.HasIndex(audit => audit.ChangedAtUtc)
            .HasDatabaseName("ix_job_status_transition_audits_changed_at_utc");
    }
}
