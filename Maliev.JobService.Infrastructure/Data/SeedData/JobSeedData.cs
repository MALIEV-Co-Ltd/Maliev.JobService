using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Infrastructure.Data.SeedData;

public static class JobSeedData
{
    // Fixed order / material GUIDs for referential stability
    private static readonly Guid[] OrderIds = Enumerable.Range(1, 12)
        .Select(i => Guid.Parse($"{i:D8}-{i:D4}-{i:D4}-{i:D4}-{i:D12}"))
        .ToArray();

    private static readonly Guid[] ItemIds = Enumerable.Range(1, 12)
        .Select(i => Guid.Parse($"aaaa{i:D4}-bbbb-cccc-dddd-eeee{i:D8}"))
        .ToArray();

    private static readonly Guid MatPla     = Guid.Parse("aa000001-0000-0000-0000-000000000001");
    private static readonly Guid MatAbs     = Guid.Parse("aa000002-0000-0000-0000-000000000002");
    private static readonly Guid MatPetg    = Guid.Parse("aa000003-0000-0000-0000-000000000003");
    private static readonly Guid MatResin   = Guid.Parse("aa000004-0000-0000-0000-000000000004");

    public static IEnumerable<Job> GetAll()
    {
        var now = DateTime.UtcNow;

        // ── MAL-FDM-001 (Bambulab X1C #1) ───────────────────────────────────
        //   Cascading queue: InProgress → 4 Queued → 1 Completed
        //   Job durations: setup=15 min + print varies
        //
        //   J01 InProgress :  now-3h   → now+2h    (print 285 min ≈ 300 min total)
        //   J02 Queued[2]  :  now+2h   → now+7h    (print 270 min + 30 min setup)
        //   J03 Queued[3]  :  now+7h   → now+13h   (print 345 min + 15 min setup)
        //   J04 Queued[4]  :  now+13h  → now+21h   (print 465 min + 15 min setup)
        //   J05 Queued[5]  :  now+21h  → now+29h   (print 465 min + 15 min setup)
        //   J06 Completed  :  2 days ago (4h job)

        var j01End = now.AddHours(2);
        var j02End = j01End.AddMinutes(15 + 285); // ~5h
        var j03End = j02End.AddMinutes(15 + 345); // ~6h
        var j04End = j03End.AddMinutes(15 + 465); // ~8h
        var j05End = j04End.AddMinutes(15 + 465); // ~8h

        // ── MAL-FDM-002 (Bambulab X1C #2) ───────────────────────────────────
        //   Lighter queue so new orders preferentially land here
        //   J07 InProgress :  now-1h   → now+3h
        //   J08 Queued[2]  :  now+3h   → now+9h
        //   J09 Queued[3]  :  now+9h   → now+15h
        //   J10 Completed  :  3 days ago
        //   J11 Cancelled  :  last week

        var j07End = now.AddHours(3);
        var j08End = j07End.AddMinutes(15 + 345);  // ~6h
        var j09End = j08End.AddMinutes(15 + 345);  // ~6h

        // ── MAL-SLA-001 (Phrozen Mighty 4K) ─────────────────────────────────
        //   J12 InProgress :  now-30min → now+2.5h
        //   J13 Queued[2]  :  now+2.5h  → now+6h
        //   J14 Queued[3]  :  now+6h    → now+9h

        var j12End = now.AddMinutes(-30).AddMinutes(30 + 150);  // start -30min, setup 30, print 150
        var j13End = j12End.AddMinutes(30 + 180);
        var j14End = j13End.AddMinutes(30 + 150);

        return new List<Job>
        {
            // ── FDM-001: InProgress ───────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("01000000-0000-0000-0000-000000000001"),
                OrderId = OrderIds[0], OrderItemId = ItemIds[0], MaterialId = MatPla,
                Technology = "FDM", VolumeCm3 = 23.8m,
                EstimatedPrintTimeMinutes = 285, SetupTimeMinutes = 15,
                AssignedMachineId = "MAL-FDM-001", Priority = 1,
                Status = JobStatus.InProgress, QueuePosition = 1,
                Notes = "Enclosure front panel — FDM",
                ScheduledStartTime = now.AddHours(-3),
                ScheduledEndTime = j01End,
                StartedAt = now.AddHours(-3),
                CreatedAt = now.AddDays(-2), UpdatedAt = now, IsOutsourced = false
            },
            // ── FDM-001: Queued [2] ──────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("01000000-0000-0000-0000-000000000002"),
                OrderId = OrderIds[1], OrderItemId = ItemIds[1], MaterialId = MatAbs,
                Technology = "FDM", VolumeCm3 = 23.8m,
                EstimatedPrintTimeMinutes = 285, SetupTimeMinutes = 15,
                AssignedMachineId = "MAL-FDM-001", Priority = 2,
                Status = JobStatus.Queued, QueuePosition = 2,
                Notes = "Bracket assembly × 3",
                ScheduledStartTime = j01End,
                ScheduledEndTime = j02End,
                CreatedAt = now.AddDays(-1), UpdatedAt = now, IsOutsourced = false
            },
            // ── FDM-001: Queued [3] ──────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("01000000-0000-0000-0000-000000000003"),
                OrderId = OrderIds[2], OrderItemId = ItemIds[2], MaterialId = MatPetg,
                Technology = "FDM", VolumeCm3 = 28.8m,
                EstimatedPrintTimeMinutes = 345, SetupTimeMinutes = 15,
                AssignedMachineId = "MAL-FDM-001", Priority = 2,
                Status = JobStatus.Queued, QueuePosition = 3,
                Notes = "Housing base plate — PETG",
                ScheduledStartTime = j02End,
                ScheduledEndTime = j03End,
                CreatedAt = now.AddHours(-18), UpdatedAt = now, IsOutsourced = false
            },
            // ── FDM-001: Queued [4] ──────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("01000000-0000-0000-0000-000000000004"),
                OrderId = OrderIds[3], OrderItemId = ItemIds[3], MaterialId = MatPla,
                Technology = "FDM", VolumeCm3 = 38.8m,
                EstimatedPrintTimeMinutes = 465, SetupTimeMinutes = 15,
                AssignedMachineId = "MAL-FDM-001", Priority = 3,
                Status = JobStatus.Queued, QueuePosition = 4,
                Notes = "Large structural part",
                ScheduledStartTime = j03End,
                ScheduledEndTime = j04End,
                CreatedAt = now.AddHours(-6), UpdatedAt = now, IsOutsourced = false
            },
            // ── FDM-001: Queued [5] ──────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("01000000-0000-0000-0000-000000000005"),
                OrderId = OrderIds[4], OrderItemId = ItemIds[4], MaterialId = MatAbs,
                Technology = "FDM", VolumeCm3 = 38.8m,
                EstimatedPrintTimeMinutes = 465, SetupTimeMinutes = 15,
                AssignedMachineId = "MAL-FDM-001", Priority = 3,
                Status = JobStatus.Queued, QueuePosition = 5,
                Notes = "Replacement manifold — ABS",
                ScheduledStartTime = j04End,
                ScheduledEndTime = j05End,
                CreatedAt = now.AddHours(-4), UpdatedAt = now, IsOutsourced = false
            },
            // ── FDM-001: Completed (2 days ago) ─────────────────────────────
            new()
            {
                Id = Guid.Parse("01000000-0000-0000-0000-000000000006"),
                OrderId = OrderIds[5], OrderItemId = ItemIds[5], MaterialId = MatPla,
                Technology = "FDM", VolumeCm3 = 15.0m,
                EstimatedPrintTimeMinutes = 180, SetupTimeMinutes = 15,
                AssignedMachineId = "MAL-FDM-001", Priority = 1,
                Status = JobStatus.Completed, QueuePosition = 0,
                Notes = "Completed — cable clip set",
                ScheduledStartTime = now.AddDays(-2).AddHours(8),
                ScheduledEndTime   = now.AddDays(-2).AddHours(11),
                StartedAt   = now.AddDays(-2).AddHours(8),
                CompletedAt = now.AddDays(-2).AddHours(11),
                CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-2), IsOutsourced = false
            },

            // ── FDM-002: InProgress ───────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("02000000-0000-0000-0000-000000000001"),
                OrderId = OrderIds[6], OrderItemId = ItemIds[6], MaterialId = MatPetg,
                Technology = "FDM", VolumeCm3 = 20.0m,
                EstimatedPrintTimeMinutes = 240, SetupTimeMinutes = 15,
                AssignedMachineId = "MAL-FDM-002", Priority = 1,
                Status = JobStatus.InProgress, QueuePosition = 1,
                Notes = "Sensor mount — PETG, qty 2",
                ScheduledStartTime = now.AddHours(-1),
                ScheduledEndTime = j07End,
                StartedAt = now.AddHours(-1),
                CreatedAt = now.AddDays(-1), UpdatedAt = now, IsOutsourced = false
            },
            // ── FDM-002: Queued [2] ──────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("02000000-0000-0000-0000-000000000002"),
                OrderId = OrderIds[7], OrderItemId = ItemIds[7], MaterialId = MatPla,
                Technology = "FDM", VolumeCm3 = 28.8m,
                EstimatedPrintTimeMinutes = 345, SetupTimeMinutes = 15,
                AssignedMachineId = "MAL-FDM-002", Priority = 2,
                Status = JobStatus.Queued, QueuePosition = 2,
                Notes = "Display bezel × 5",
                ScheduledStartTime = j07End,
                ScheduledEndTime = j08End,
                CreatedAt = now.AddHours(-12), UpdatedAt = now, IsOutsourced = false
            },
            // ── FDM-002: Queued [3] ──────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("02000000-0000-0000-0000-000000000003"),
                OrderId = OrderIds[8], OrderItemId = ItemIds[8], MaterialId = MatAbs,
                Technology = "FDM", VolumeCm3 = 28.8m,
                EstimatedPrintTimeMinutes = 345, SetupTimeMinutes = 15,
                AssignedMachineId = "MAL-FDM-002", Priority = 2,
                Status = JobStatus.Queued, QueuePosition = 3,
                Notes = "Cover plate — high-temp ABS",
                ScheduledStartTime = j08End,
                ScheduledEndTime = j09End,
                CreatedAt = now.AddHours(-8), UpdatedAt = now, IsOutsourced = false
            },
            // ── FDM-002: Completed (3 days ago) ─────────────────────────────
            new()
            {
                Id = Guid.Parse("02000000-0000-0000-0000-000000000004"),
                OrderId = OrderIds[9], OrderItemId = ItemIds[9], MaterialId = MatPla,
                Technology = "FDM", VolumeCm3 = 10.0m,
                EstimatedPrintTimeMinutes = 120, SetupTimeMinutes = 15,
                AssignedMachineId = "MAL-FDM-002", Priority = 1,
                Status = JobStatus.Completed, QueuePosition = 0,
                Notes = "Completed — door latch prototype",
                ScheduledStartTime = now.AddDays(-3).AddHours(9),
                ScheduledEndTime   = now.AddDays(-3).AddHours(11),
                StartedAt   = now.AddDays(-3).AddHours(9),
                CompletedAt = now.AddDays(-3).AddHours(11),
                CreatedAt = now.AddDays(-4), UpdatedAt = now.AddDays(-3), IsOutsourced = false
            },
            // ── FDM-002: Cancelled ───────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("02000000-0000-0000-0000-000000000005"),
                OrderId = OrderIds[10], OrderItemId = ItemIds[10], MaterialId = MatPetg,
                Technology = "FDM", VolumeCm3 = 18.0m,
                EstimatedPrintTimeMinutes = 216, SetupTimeMinutes = 15,
                AssignedMachineId = "MAL-FDM-002", Priority = 2,
                Status = JobStatus.Cancelled, QueuePosition = 0,
                Notes = "Cancelled — design revision required",
                ScheduledStartTime = now.AddDays(-1).AddHours(10),
                ScheduledEndTime   = now.AddDays(-1).AddHours(14),
                CreatedAt = now.AddDays(-5), UpdatedAt = now.AddDays(-1), IsOutsourced = false
            },

            // ── SLA-001: InProgress ───────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("03000000-0000-0000-0000-000000000001"),
                OrderId = OrderIds[11], OrderItemId = ItemIds[11], MaterialId = MatResin,
                Technology = "SLA", VolumeCm3 = 18.8m,
                EstimatedPrintTimeMinutes = 150, SetupTimeMinutes = 30,
                AssignedMachineId = "MAL-SLA-001", Priority = 1,
                Status = JobStatus.InProgress, QueuePosition = 1,
                Notes = "Dental model — high precision",
                ScheduledStartTime = now.AddMinutes(-30),
                ScheduledEndTime = j12End,
                StartedAt = now.AddMinutes(-30),
                CreatedAt = now.AddDays(-1), UpdatedAt = now, IsOutsourced = false
            },
            // ── SLA-001: Queued [2] ──────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("03000000-0000-0000-0000-000000000002"),
                OrderId = Guid.Parse("aa000001-1111-1111-1111-000000000001"),
                OrderItemId = Guid.Parse("bb000001-1111-1111-1111-000000000001"),
                MaterialId = MatResin,
                Technology = "SLA", VolumeCm3 = 22.5m,
                EstimatedPrintTimeMinutes = 180, SetupTimeMinutes = 30,
                AssignedMachineId = "MAL-SLA-001", Priority = 2,
                Status = JobStatus.Queued, QueuePosition = 2,
                Notes = "Jewelry wax casting model",
                ScheduledStartTime = j12End,
                ScheduledEndTime = j13End,
                CreatedAt = now.AddHours(-20), UpdatedAt = now, IsOutsourced = false
            },
            // ── SLA-001: Queued [3] ──────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("03000000-0000-0000-0000-000000000003"),
                OrderId = Guid.Parse("aa000002-2222-2222-2222-000000000002"),
                OrderItemId = Guid.Parse("bb000002-2222-2222-2222-000000000002"),
                MaterialId = MatResin,
                Technology = "SLA", VolumeCm3 = 18.8m,
                EstimatedPrintTimeMinutes = 150, SetupTimeMinutes = 30,
                AssignedMachineId = "MAL-SLA-001", Priority = 3,
                Status = JobStatus.Queued, QueuePosition = 3,
                Notes = "Hearing aid shell prototype",
                ScheduledStartTime = j13End,
                ScheduledEndTime = j14End,
                CreatedAt = now.AddHours(-16), UpdatedAt = now, IsOutsourced = false
            },

            // ── Pending (unassigned) ─────────────────────────────────────────
            new()
            {
                Id = Guid.Parse("04000000-0000-0000-0000-000000000001"),
                OrderId = Guid.Parse("aa000003-3333-3333-3333-000000000003"),
                OrderItemId = Guid.Parse("bb000003-3333-3333-3333-000000000003"),
                MaterialId = MatPla,
                Technology = "FDM", VolumeCm3 = 30.0m,
                EstimatedPrintTimeMinutes = 360, SetupTimeMinutes = 15,
                AssignedMachineId = null, Priority = 2,
                Status = JobStatus.Pending, QueuePosition = 0,
                Notes = "Pending review — client approval needed",
                ScheduledStartTime = null, ScheduledEndTime = null,
                CreatedAt = now.AddHours(-5), UpdatedAt = now, IsOutsourced = false
            },
            new()
            {
                Id = Guid.Parse("04000000-0000-0000-0000-000000000002"),
                OrderId = Guid.Parse("aa000004-4444-4444-4444-000000000004"),
                OrderItemId = Guid.Parse("bb000004-4444-4444-4444-000000000004"),
                MaterialId = MatResin,
                Technology = "SLA", VolumeCm3 = 12.0m,
                EstimatedPrintTimeMinutes = 96, SetupTimeMinutes = 30,
                AssignedMachineId = null, Priority = 1,
                Status = JobStatus.Pending, QueuePosition = 0,
                Notes = "Pending — awaiting material restock",
                ScheduledStartTime = null, ScheduledEndTime = null,
                CreatedAt = now.AddHours(-3), UpdatedAt = now, IsOutsourced = false
            },
        };
    }
}
