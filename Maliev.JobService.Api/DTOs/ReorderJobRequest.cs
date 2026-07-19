namespace Maliev.JobService.Api.DTOs;

/// <summary>Request to change the queue position of a job.</summary>
public record ReorderJobRequest(int NewPosition);
