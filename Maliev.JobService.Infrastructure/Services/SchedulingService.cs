using Maliev.JobService.Application.Abstractions;
using Maliev.JobService.Domain.Entities;
using Maliev.JobService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.JobService.Infrastructure.Services;

/// <summary>
/// Computes and maintains time slots for manufacturing jobs on a machine queue.
/// </summary>
public class SchedulingService : ISchedulingService
{
    private readonly JobDbContext _db;
    private readonly ILogger<SchedulingService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SchedulingService"/>.
    /// </summary>
    public SchedulingService(JobDbContext db, ILogger<SchedulingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ComputeSlotAsync(Job job, string machineId, CancellationToken ct = default)
    {
        // Load all active jobs on this machine ordered by queue position
        var activeJobs = await _db.Jobs
            .Where(j => j.AssignedMachineId == machineId &&
                        (j.Status == JobStatus.Queued || j.Status == JobStatus.InProgress))
            .OrderBy(j => j.QueuePosition)
            .ToListAsync(ct);

        var maxPosition = activeJobs.Count > 0 ? activeJobs.Max(j => j.QueuePosition) : 0;
        job.QueuePosition = maxPosition + 1;

        // Find the earliest available start time (end of the last scheduled job or now)
        var slotStart = activeJobs
            .Where(j => j.ScheduledEndTime.HasValue)
            .Select(j => j.ScheduledEndTime!.Value)
            .DefaultIfEmpty(DateTime.UtcNow)
            .Max();

        if (slotStart < DateTime.UtcNow)
            slotStart = DateTime.UtcNow;

        var totalMinutes = job.SetupTimeMinutes + job.EstimatedPrintTimeMinutes;
        job.ScheduledStartTime = slotStart;
        job.ScheduledEndTime = slotStart.AddMinutes(totalMinutes);

        _logger.LogInformation(
            "Slot assigned for job {JobId} on {MachineId}: pos={Position} {Start:u} → {End:u}",
            job.Id, machineId, job.QueuePosition, job.ScheduledStartTime, job.ScheduledEndTime);
    }

    /// <inheritdoc />
    public async Task RescheduleQueueAsync(string machineId, CancellationToken ct = default)
    {
        var activeJobs = await _db.Jobs
            .Where(j => j.AssignedMachineId == machineId &&
                        (j.Status == JobStatus.Queued || j.Status == JobStatus.InProgress))
            .OrderBy(j => j.QueuePosition)
            .ToListAsync(ct);

        if (activeJobs.Count == 0) return;

        // The InProgress job (at most one) anchors the chain.
        // Queued jobs cascade from its scheduled end.
        var inProgress = activeJobs.FirstOrDefault(j => j.Status == JobStatus.InProgress);
        var cursor = inProgress?.ScheduledEndTime ?? DateTime.UtcNow;
        if (cursor < DateTime.UtcNow) cursor = DateTime.UtcNow;

        // Compact queue positions and cascade times for Queued jobs
        var position = inProgress != null ? inProgress.QueuePosition + 1 : 1;
        foreach (var j in activeJobs.Where(j => j.Status == JobStatus.Queued).OrderBy(j => j.QueuePosition))
        {
            j.QueuePosition = position++;
            j.ScheduledStartTime = cursor;
            j.ScheduledEndTime = cursor.AddMinutes(j.SetupTimeMinutes + j.EstimatedPrintTimeMinutes);
            cursor = j.ScheduledEndTime.Value;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Rescheduled {Count} queued jobs on machine {MachineId}", activeJobs.Count, machineId);
    }

    /// <inheritdoc />
    public async Task ReorderJobAsync(Guid jobId, int newPosition, string machineId, CancellationToken ct = default)
    {
        var queuedJobs = await _db.Jobs
            .Where(j => j.AssignedMachineId == machineId && j.Status == JobStatus.Queued)
            .OrderBy(j => j.QueuePosition)
            .ToListAsync(ct);

        var target = queuedJobs.FirstOrDefault(j => j.Id == jobId);
        if (target == null) return;

        queuedJobs.Remove(target);
        var clampedPos = Math.Clamp(newPosition - 1, 0, queuedJobs.Count);
        queuedJobs.Insert(clampedPos, target);

        // Reassign sequential positions (InProgress job holds position 0 conceptually)
        var nextPos = 1;
        foreach (var j in queuedJobs)
            j.QueuePosition = nextPos++;

        await _db.SaveChangesAsync(ct);
        await RescheduleQueueAsync(machineId, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Job>> GetMachineScheduleAsync(
        string machineId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        return _db.Jobs
            .AsNoTracking()
            .Where(j => j.AssignedMachineId == machineId &&
                        j.ScheduledStartTime.HasValue &&
                        j.ScheduledStartTime >= from &&
                        j.ScheduledStartTime <= to)
            .OrderBy(j => j.ScheduledStartTime)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<Job>)t.Result, ct);
    }
}
