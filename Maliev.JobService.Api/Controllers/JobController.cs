using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.JobService.Application.Authorization;
using Maliev.JobService.Api.DTOs;
using Maliev.JobService.Application.Abstractions;
using Maliev.JobService.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maliev.JobService.Api.Controllers;

/// <summary>
/// Controller for managing manufacturing jobs.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("job/v{version:apiVersion}/jobs")]
[RequirePermission(JobPermissions.JobsRead)]
public class JobController : ControllerBase
{
    private readonly IJobService _jobService;
    private readonly ILogger<JobController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobController"/> class.
    /// </summary>
    /// <param name="jobService">The application job service.</param>
    /// <param name="logger">The logger.</param>
    public JobController(IJobService jobService, ILogger<JobController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a specific job by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the job.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The job DTO if found; otherwise, NotFound.</returns>
    [HttpGet("{id}")]
    [RequirePermission(JobPermissions.JobsRead)]
    public async Task<ActionResult<JobDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetByIdAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(JobDto.FromEntity(job));
    }

    /// <summary>
    /// Retrieves a paged list of jobs with optional filtering.
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="technology">Optional technology filter.</param>
    /// <param name="assignedMachineId">Optional machine ID filter.</param>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A paged result of job DTOs.</returns>
    [HttpGet]
    [RequirePermission(JobPermissions.JobsRead)]
    public async Task<ActionResult<PagedResult<JobDto>>> GetJobs(
        [FromQuery] JobStatus? status,
        [FromQuery] string? technology,
        [FromQuery] string? assignedMachineId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _jobService.GetJobsAsync(status, technology, assignedMachineId, page, pageSize, cancellationToken);
        var items = result.Items.Select(JobDto.FromEntity).ToList();

        _logger.LogInformation("Retrieved {Count} jobs (page {Page} of {TotalPages})", items.Count, result.Page, result.TotalPages);

        return Ok(new PagedResult<JobDto>
        {
            Items = items,
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
        });
    }

    /// <summary>
    /// Retrieves the jobs in a Kanban board format.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The Kanban board data.</returns>
    [HttpGet("kanban")]
    [RequirePermission(JobPermissions.JobsRead)]
    public async Task<ActionResult<KanbanResponse>> GetKanban(CancellationToken cancellationToken)
    {
        var jobs = await _jobService.GetKanbanJobsAsync(cancellationToken);

        var response = new KanbanResponse
        {
            Pending = jobs.Where(j => j.Status == JobStatus.Pending).Select(KanbanJobDto.FromEntity).ToList(),
            Queued = jobs.Where(j => j.Status == JobStatus.Queued).Select(KanbanJobDto.FromEntity).ToList(),
            InProgress = jobs.Where(j => j.Status == JobStatus.InProgress).Select(KanbanJobDto.FromEntity).ToList(),
            Finishing = jobs.Where(j => j.Status == JobStatus.Finishing).Select(KanbanJobDto.FromEntity).ToList(),
            Completed = jobs.Where(j => j.Status == JobStatus.Completed).Select(KanbanJobDto.FromEntity).ToList(),
            Cancelled = jobs.Where(j => j.Status == JobStatus.Cancelled).Select(KanbanJobDto.FromEntity).ToList(),
        };

        _logger.LogInformation("Retrieved Kanban view with {Total} total jobs", jobs.Count);
        return Ok(response);
    }

    /// <summary>
    /// Gets the queue depth (number of active jobs) by technology.
    /// </summary>
    /// <param name="technology">Optional technology filter (e.g., FDM, SLA, CNC).</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A dictionary of technology to active job count.</returns>
    [HttpGet("queue-depth")]
    [RequirePermission(JobPermissions.JobsRead)]
    public async Task<ActionResult<Dictionary<string, int>>> GetQueueDepth(
        [FromQuery] string? technology,
        CancellationToken cancellationToken)
    {
        var result = await _jobService.GetQueueDepthByTechnologyAsync(technology, cancellationToken);
        _logger.LogInformation("Retrieved queue depth for {Technology}: {Count}", technology ?? "all technologies", result.Count);
        return Ok(result);
    }

    /// <summary>
    /// Queues a job on a specific machine.
    /// </summary>
    /// <param name="id">The job ID.</param>
    /// <param name="request">The queue request containing machine ID.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The updated job DTO.</returns>
    [HttpPost("{id}/queue")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Queue(Guid id, [FromBody] QueueJobRequest request, CancellationToken cancellationToken)
    {
        var result = await _jobService.QueueAsync(id, request.MachineId, GetCurrentUserId(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Starts production for a job.
    /// </summary>
    /// <param name="id">The job ID.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The updated job DTO.</returns>
    [HttpPost("{id}/start")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Start(Guid id, CancellationToken cancellationToken)
    {
        var result = await _jobService.StartAsync(id, GetCurrentUserId(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Moves a job to the finishing stage.
    /// </summary>
    /// <param name="id">The job ID.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The updated job DTO.</returns>
    [HttpPost("{id}/finish")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Finish(Guid id, CancellationToken cancellationToken)
    {
        var result = await _jobService.FinishAsync(id, GetCurrentUserId(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Completes a job.
    /// </summary>
    /// <param name="id">The job ID.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The updated job DTO.</returns>
    [HttpPost("{id}/complete")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _jobService.CompleteAsync(id, GetCurrentUserId(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Cancels a job.
    /// </summary>
    /// <param name="id">The job ID.</param>
    /// <param name="request">The cancellation request containing the reason.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The updated job DTO.</returns>
    [HttpPost("{id}/cancel")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Cancel(Guid id, [FromBody] CancelJobRequest request, CancellationToken cancellationToken)
    {
        var result = await _jobService.CancelAsync(id, request.Reason, GetCurrentUserId(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Reassigns a queued job to a different machine.
    /// </summary>
    /// <param name="id">The job ID.</param>
    /// <param name="request">The reassignment request containing the new machine ID.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The updated job DTO.</returns>
    [HttpPatch("{id}/reassign")]
    [RequirePermission(JobPermissions.JobsWrite)]
    public async Task<ActionResult<JobDto>> Reassign(Guid id, [FromBody] ReassignJobRequest request, CancellationToken cancellationToken)
    {
        var result = await _jobService.ReassignAsync(id, request.MachineId, cancellationToken);
        return ToActionResult(result);
    }

    private ActionResult<JobDto> ToActionResult(Maliev.JobService.Application.Models.JobOperationResult result)
    {
        if (result.IsNotFound)
        {
            return NotFound();
        }

        if (!result.IsSuccess)
        {
            return Conflict(new { error = result.Error ?? "Operation failed" });
        }

        return Ok(JobDto.FromEntity(result.Job!));
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("user_id")?.Value
            ?? "system";
    }
}
