using Microsoft.AspNetCore.Mvc;
using Maliev.JobService.Api.Authorization;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.MessagingContracts.Contracts.Jobs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Maliev.JobService.Data;
using Maliev.JobService.Data.Entities;
using Maliev.JobService.Api.DTOs;
using Maliev.JobService.Api.Metrics;
using System.Security.Claims;

namespace Maliev.JobService.Api.Controllers;

/// <summary>
/// Controller for managing manufacturing jobs.
/// </summary>
[ApiController]
[Route("job/v1/jobs")]
[RequirePermission(JobPermissions.JobsRead)]
public class JobController : ControllerBase
{
    private readonly JobDbContext _dbContext;
    private readonly JobMetrics _metrics;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<JobController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="metrics">The job metrics.</param>
    /// <param name="publishEndpoint">The message bus publish endpoint.</param>
    /// <param name="logger">The logger.</param>
    public JobController(
        JobDbContext dbContext,
        JobMetrics metrics,
        IPublishEndpoint publishEndpoint,
        ILogger<JobController> logger)
    {
        _dbContext = dbContext;
        _metrics = metrics;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Gets a specific job by its ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<JobDto>> GetById(Guid id)
    {
        var job = await _dbContext.Jobs.FindAsync(id);
        if (job == null)
            return NotFound();
            
        return Ok(JobDto.FromEntity(job));
    }

    /// <summary>
    /// Retrieves a paged list of jobs with optional filtering.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<JobDto>>> GetJobs(
        [FromQuery] JobStatus? status,
        [FromQuery] string? technology,
        [FromQuery] string? assignedMachineId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _dbContext.Jobs.AsNoTracking();
        
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);
            
        if (!string.IsNullOrEmpty(technology))
            query = query.Where(j => j.Technology == technology);
            
        if (!string.IsNullOrEmpty(assignedMachineId))
            query = query.Where(j => j.AssignedMachineId == assignedMachineId);
        
        var total = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)Math.Clamp(pageSize, 1, 100));
        
        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(j => JobDto.FromEntity(j))
            .ToListAsync();
        
        _logger.LogInformation("Retrieved {Count} jobs (page {Page} of {TotalPages})", items.Count, page, totalPages);
        
        return Ok(new PagedResult<JobDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    /// <summary>
    /// Retrieves the jobs in a Kanban board format.
    /// </summary>
    [HttpGet("kanban")]
    public async Task<ActionResult<KanbanResponse>> GetKanban()
    {
        var jobs = await _dbContext.Jobs
            .AsNoTracking()
            .OrderBy(j => j.Priority)
            .ToListAsync();
        
        var response = new KanbanResponse
        {
            Pending = jobs.Where(j => j.Status == JobStatus.Pending).Select(KanbanJobDto.FromEntity).ToList(),
            Queued = jobs.Where(j => j.Status == JobStatus.Queued).Select(KanbanJobDto.FromEntity).ToList(),
            InProgress = jobs.Where(j => j.Status == JobStatus.InProgress).Select(KanbanJobDto.FromEntity).ToList(),
            Finishing = jobs.Where(j => j.Status == JobStatus.Finishing).Select(KanbanJobDto.FromEntity).ToList(),
            Completed = jobs.Where(j => j.Status == JobStatus.Completed).Select(KanbanJobDto.FromEntity).ToList(),
            Cancelled = jobs.Where(j => j.Status == JobStatus.Cancelled).Select(KanbanJobDto.FromEntity).ToList()
        };
        
        _logger.LogInformation("Retrieved Kanban view with {Total} total jobs", jobs.Count);
        
        return Ok(response);
    }

    /// <summary>
    /// Queues a job on a specific machine.
    /// </summary>
    [HttpPost("{id}/queue")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Queue(Guid id, [FromBody] QueueJobRequest request)
    {
        var job = await _dbContext.Jobs.FindAsync(id);
        if (job == null)
            return NotFound();
        
        var (valid, error) = ValidateTransition(job.Status, "queue");
        if (!valid)
            return Conflict(new { error });
        
        var previousStatus = job.Status;
        var transitionStart = job.UpdatedAt;
        
        job.Status = JobStatus.Queued;
        job.AssignedMachineId = request.MachineId;
        job.UpdatedAt = DateTime.UtcNow;
        
        await PublishJobStatusChangedAsync(job, previousStatus);
        await _dbContext.SaveChangesAsync();
        
        _metrics.RecordTransition(previousStatus.ToString(), job.Status.ToString(), DateTime.UtcNow - transitionStart);
        _logger.LogInformation("Job {JobId} queued on machine {MachineId}", job.Id, request.MachineId);
        
        return Ok(JobDto.FromEntity(job));
    }

    /// <summary>
    /// Starts production for a job.
    /// </summary>
    [HttpPost("{id}/start")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Start(Guid id)
    {
        var job = await _dbContext.Jobs.FindAsync(id);
        if (job == null)
            return NotFound();
        
        var (valid, error) = ValidateTransition(job.Status, "start");
        if (!valid)
            return Conflict(new { error });
        
        var previousStatus = job.Status;
        var transitionStart = job.UpdatedAt;
        
        job.Status = JobStatus.InProgress;
        job.StartedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        
        await _publishEndpoint.Publish(new JobStartedEvent
        {
            JobId = job.Id,
            OrderId = job.OrderId,
            MaterialId = job.MaterialId,
            VolumeCm3 = job.VolumeCm3,
            Technology = job.Technology,
            AssignedMachineId = job.AssignedMachineId ?? string.Empty,
            StartedAt = job.StartedAt.Value
        });
        await PublishJobStatusChangedAsync(job, previousStatus);
        await _dbContext.SaveChangesAsync();
        
        _metrics.RecordTransition(previousStatus.ToString(), job.Status.ToString(), DateTime.UtcNow - transitionStart);
        _logger.LogInformation("Job {JobId} started at {StartedAt}", job.Id, job.StartedAt);
        
        return Ok(JobDto.FromEntity(job));
    }

    /// <summary>
    /// Moves a job to the finishing stage.
    /// </summary>
    [HttpPost("{id}/finish")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Finish(Guid id)
    {
        var job = await _dbContext.Jobs.FindAsync(id);
        if (job == null)
            return NotFound();
        
        var (valid, error) = ValidateTransition(job.Status, "finish");
        if (!valid)
            return Conflict(new { error });
        
        var previousStatus = job.Status;
        var transitionStart = job.UpdatedAt;
        
        job.Status = JobStatus.Finishing;
        job.UpdatedAt = DateTime.UtcNow;
        
        await PublishJobStatusChangedAsync(job, previousStatus);
        await _dbContext.SaveChangesAsync();
        
        _metrics.RecordTransition(previousStatus.ToString(), job.Status.ToString(), DateTime.UtcNow - transitionStart);
        _logger.LogInformation("Job {JobId} moved to finishing", job.Id);
        
        return Ok(JobDto.FromEntity(job));
    }

    /// <summary>
    /// Completes a job.
    /// </summary>
    [HttpPost("{id}/complete")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Complete(Guid id)
    {
        var job = await _dbContext.Jobs.FindAsync(id);
        if (job == null)
            return NotFound();
        
        var (valid, error) = ValidateTransition(job.Status, "complete");
        if (!valid)
            return Conflict(new { error });
        
        var previousStatus = job.Status;
        var transitionStart = job.UpdatedAt;
        
        job.Status = JobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        
        await PublishJobStatusChangedAsync(job, previousStatus);
        await _dbContext.SaveChangesAsync();
        
        _metrics.RecordTransition(previousStatus.ToString(), job.Status.ToString(), DateTime.UtcNow - transitionStart);
        _logger.LogInformation("Job {JobId} completed at {CompletedAt}", job.Id, job.CompletedAt);
        
        return Ok(JobDto.FromEntity(job));
    }

    /// <summary>
    /// Cancels a job.
    /// </summary>
    [HttpPost("{id}/cancel")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Cancel(Guid id, [FromBody] CancelJobRequest request)
    {
        var job = await _dbContext.Jobs.FindAsync(id);
        if (job == null)
            return NotFound();
        
        var previousStatus = job.Status;
        var transitionStart = job.UpdatedAt;
        
        job.Status = JobStatus.Cancelled;
        job.Notes = request.Reason;
        job.UpdatedAt = DateTime.UtcNow;
        
        await PublishJobStatusChangedAsync(job, previousStatus);
        await _dbContext.SaveChangesAsync();
        
        _metrics.RecordTransition(previousStatus.ToString(), job.Status.ToString(), DateTime.UtcNow - transitionStart);
        _logger.LogInformation("Job {JobId} cancelled with reason: {Reason}", job.Id, request.Reason);
        
        return Ok(JobDto.FromEntity(job));
    }

    /// <summary>
    /// Reassigns a queued job to a different machine.
    /// </summary>
    [HttpPatch("{id}/reassign")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Reassign(Guid id, [FromBody] ReassignJobRequest request)
    {
        var job = await _dbContext.Jobs.FindAsync(id);
        if (job == null)
            return NotFound();
        
        if (job.Status != JobStatus.Queued)
            return Conflict(new { error = "Can only reassign jobs in Queued status" });
        
        job.AssignedMachineId = request.MachineId;
        job.UpdatedAt = DateTime.UtcNow;
        
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation("Job {JobId} reassigned to machine {MachineId}", job.Id, request.MachineId);
        
        return Ok(JobDto.FromEntity(job));
    }

    private static (bool Valid, string Error) ValidateTransition(JobStatus currentStatus, string action)
    {
        var validTransitions = new Dictionary<JobStatus, HashSet<string>>
        {
            [JobStatus.Pending] = new() { "queue", "start", "cancel" },
            [JobStatus.Queued] = new() { "start", "reassign", "cancel" },
            [JobStatus.InProgress] = new() { "finish", "cancel" },
            [JobStatus.Finishing] = new() { "complete", "cancel" },
            [JobStatus.Completed] = new() { "cancel" },
            [JobStatus.Cancelled] = new()
        };
        
        if (validTransitions.TryGetValue(currentStatus, out var allowedActions) && allowedActions.Contains(action))
            return (true, string.Empty);
        
        return (false, $"Cannot {action} a job in {currentStatus} status");
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("user_id")?.Value
            ?? "system";
    }

    private async Task PublishJobStatusChangedAsync(Job job, JobStatus previousStatus)
    {
        await _publishEndpoint.Publish(new JobStatusChangedEvent
        {
            JobId = job.Id,
            OrderId = job.OrderId,
            PreviousStatus = previousStatus.ToString(),
            NewStatus = job.Status.ToString(),
            Technology = job.Technology,
            AssignedMachineId = job.AssignedMachineId,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = GetCurrentUserId()
        });
    }
}
