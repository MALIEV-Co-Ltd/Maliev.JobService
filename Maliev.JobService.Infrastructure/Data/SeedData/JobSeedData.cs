using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Infrastructure.Data.SeedData;

public static class JobSeedData
{
    private static readonly Guid Order1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Order2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Order3Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OrderItem1Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OrderItem2Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OrderItem3Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Material1Id = Guid.Parse("aaaabbbb-cccc-dddd-eeee-aaaabbbbcccc");
    private static readonly Guid Material2Id = Guid.Parse("11112222-3333-4444-5555-111122223333");
    private static readonly Guid Material3Id = Guid.Parse("66667777-8888-9999-0000-666677778888");

    private static DateTime UtcNow => DateTime.UtcNow;

    public static IEnumerable<Job> GetAll()
    {
        var now = DateTime.UtcNow;

        return new List<Job>
        {
            new()
            {
                Id = Guid.Parse("10101010-1010-1010-1010-101010101010"),
                OrderId = Order1Id,
                OrderItemId = OrderItem1Id,
                MaterialId = Material1Id,
                Technology = "FDM",
                VolumeCm3 = 125.5m,
                EstimatedPrintTimeMinutes = 240,
                AssignedMachineId = "MAL-FDM-001",
                Priority = 1,
                Status = JobStatus.InProgress,
                Notes = "Production job - Enclosure parts",
                ScheduledStartTime = now.AddHours(-2),
                ScheduledEndTime = now.AddHours(2),
                SetupTimeMinutes = 30,
                QueuePosition = 1,
                StartedAt = now.AddHours(-2),
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now,
                IsOutsourced = false
            },
            new()
            {
                Id = Guid.Parse("20202020-2020-2020-2020-202020202020"),
                OrderId = Order1Id,
                OrderItemId = OrderItem2Id,
                MaterialId = Material2Id,
                Technology = "FDM",
                VolumeCm3 = 85.25m,
                EstimatedPrintTimeMinutes = 180,
                AssignedMachineId = "MAL-FDM-001",
                Priority = 2,
                Status = JobStatus.Queued,
                Notes = "Production job - Bracket components",
                ScheduledStartTime = now.AddHours(2),
                ScheduledEndTime = now.AddHours(5),
                SetupTimeMinutes = 20,
                QueuePosition = 2,
                StartedAt = null,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now,
                IsOutsourced = false
            },
            new()
            {
                Id = Guid.Parse("30303030-3030-3030-3030-303030303030"),
                OrderId = Order2Id,
                OrderItemId = OrderItem3Id,
                MaterialId = Material3Id,
                Technology = "SLA",
                VolumeCm3 = 45.0m,
                EstimatedPrintTimeMinutes = 120,
                AssignedMachineId = "MAL-SLA-001",
                Priority = 1,
                Status = JobStatus.Queued,
                Notes = "High-detail prototype",
                ScheduledStartTime = now.AddHours(1),
                ScheduledEndTime = now.AddHours(3),
                SetupTimeMinutes = 15,
                QueuePosition = 1,
                StartedAt = null,
                CreatedAt = now.AddHours(-12),
                UpdatedAt = now,
                IsOutsourced = false
            },
            new()
            {
                Id = Guid.Parse("40404040-4040-4040-4040-404040404040"),
                OrderId = Order2Id,
                OrderItemId = OrderItem1Id,
                MaterialId = Material1Id,
                Technology = "FDM",
                VolumeCm3 = 200.0m,
                EstimatedPrintTimeMinutes = 360,
                AssignedMachineId = "MAL-FDM-002",
                Priority = 3,
                Status = JobStatus.Queued,
                Notes = "Bulk production run",
                ScheduledStartTime = now.AddHours(5),
                ScheduledEndTime = now.AddHours(11),
                SetupTimeMinutes = 45,
                QueuePosition = 3,
                StartedAt = null,
                CreatedAt = now.AddHours(-6),
                UpdatedAt = now,
                IsOutsourced = false
            },
            new()
            {
                Id = Guid.Parse("50505050-5050-5050-5050-505050505050"),
                OrderId = Order3Id,
                OrderItemId = OrderItem2Id,
                MaterialId = Material2Id,
                Technology = "CNC",
                VolumeCm3 = 500.0m,
                EstimatedPrintTimeMinutes = 480,
                AssignedMachineId = "MAL-CNC-001",
                Priority = 1,
                Status = JobStatus.InProgress,
                Notes = "CNC machining - Aluminum fixture",
                ScheduledStartTime = now.AddHours(-4),
                ScheduledEndTime = now.AddHours(4),
                SetupTimeMinutes = 60,
                QueuePosition = 1,
                StartedAt = now.AddHours(-4),
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now,
                IsOutsourced = false
            },
            new()
            {
                Id = Guid.Parse("60606060-6060-6060-6060-606060606060"),
                OrderId = Order3Id,
                OrderItemId = OrderItem3Id,
                MaterialId = Material3Id,
                Technology = "SLA",
                VolumeCm3 = 30.0m,
                EstimatedPrintTimeMinutes = 90,
                AssignedMachineId = "MAL-SLA-001",
                Priority = 2,
                Status = JobStatus.Completed,
                Notes = "Completed - Dental model",
                ScheduledStartTime = now.AddDays(-1),
                ScheduledEndTime = now.AddDays(-1).AddHours(2),
                SetupTimeMinutes = 15,
                QueuePosition = 0,
                StartedAt = now.AddDays(-1),
                CompletedAt = now.AddDays(-1).AddHours(2),
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-1),
                IsOutsourced = false
            },
            new()
            {
                Id = Guid.Parse("70707070-7070-7070-7070-707070707070"),
                OrderId = Order1Id,
                OrderItemId = OrderItem1Id,
                MaterialId = Material1Id,
                Technology = "FDM",
                VolumeCm3 = 150.0m,
                EstimatedPrintTimeMinutes = 300,
                AssignedMachineId = null,
                Priority = 0,
                Status = JobStatus.Pending,
                Notes = "Pending - awaiting material",
                ScheduledStartTime = null,
                ScheduledEndTime = null,
                SetupTimeMinutes = 30,
                QueuePosition = 0,
                StartedAt = null,
                CompletedAt = null,
                CreatedAt = now.AddHours(-3),
                UpdatedAt = now,
                IsOutsourced = false
            }
        };
    }
}