namespace Maliev.JobService.Domain.Clients;

/// <summary>
/// Client for updating source project state after production job creation.
/// </summary>
public interface IProjectServiceClient
{
    /// <summary>Links the specified job to the source project part.</summary>
    Task LinkJobToPartAsync(Guid projectPartId, Guid jobId, CancellationToken cancellationToken = default);
}
