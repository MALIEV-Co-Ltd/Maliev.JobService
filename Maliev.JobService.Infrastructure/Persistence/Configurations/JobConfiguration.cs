using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for the <see cref="Job"/> entity.
/// </summary>
public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.HasKey(j => j.Id);
        
        builder.Property(j => j.Technology)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(j => j.AssignedMachineId)
            .HasMaxLength(100);
            
        builder.Property(j => j.Notes)
            .HasMaxLength(2000);
            
        builder.Property(j => j.VolumeCm3)
            .HasPrecision(10, 3);
            
        builder.HasIndex(j => j.OrderId)
            .HasDatabaseName("IX_Jobs_OrderId");
            
        builder.HasIndex(j => j.Status)
            .HasDatabaseName("IX_Jobs_Status");
            
        builder.HasIndex(j => j.AssignedMachineId)
            .HasDatabaseName("IX_Jobs_AssignedMachineId");
            
        builder.HasIndex(j => j.Priority)
            .HasDatabaseName("IX_Jobs_Priority");
        
        builder.HasIndex(j => new { j.OrderId, j.OrderItemId })
            .IsUnique()
            .HasDatabaseName("IX_Jobs_OrderId_OrderItemId");

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
