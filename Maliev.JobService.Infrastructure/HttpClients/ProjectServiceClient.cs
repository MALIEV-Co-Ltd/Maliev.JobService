using System.Net;
using System.Net.Http.Json;
using Maliev.JobService.Domain.Clients;
using Microsoft.Extensions.Logging;

namespace Maliev.JobService.Infrastructure.HttpClients;

/// <summary>
/// Implementation of the ProjectService client.
/// </summary>
public class ProjectServiceClient : IProjectServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProjectServiceClient> _logger;

    /// <summary>Initializes a new instance of the <see cref="ProjectServiceClient"/> class.</summary>
    public ProjectServiceClient(HttpClient httpClient, ILogger<ProjectServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LinkJobToPartAsync(Guid projectPartId, Guid jobId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/project/v1/projects/parts/{projectPartId:D}/job-link",
            new { jobId },
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "Project part {ProjectPartId} not found while linking Job {JobId}",
                projectPartId,
                jobId);
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}
