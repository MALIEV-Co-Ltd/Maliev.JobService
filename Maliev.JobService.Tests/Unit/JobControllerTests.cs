using Maliev.JobService.Api.Controllers;
using Maliev.JobService.Api.DTOs;
using Maliev.JobService.Application.Abstractions;
using Maliev.JobService.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Maliev.JobService.Tests.Unit;

public class JobControllerTests
{
    [Fact]
    public async Task GetKanban_CompletedJobs_AreExposedAsQualityReviewPending()
    {
        var completedJob = CreateTestJob(JobStatus.Completed);
        var service = new Mock<IJobService>();
        _ = service
            .Setup(jobService => jobService.GetKanbanJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([completedJob]);
        var controller = new JobController(service.Object, NullLogger<JobController>.Instance);

        ActionResult<KanbanResponse> result = await controller.GetKanban(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<KanbanResponse>(okResult.Value);
        KanbanJobDto qualityReviewJob = Assert.Single(response.QualityReviewPending);
        Assert.Equal(completedJob.Id, qualityReviewJob.JobId);
    }

    [Fact]
    public async Task GetJobsByOrder_ReturnsJobsForRequestedOrder()
    {
        var orderId = Guid.NewGuid();
        var job = CreateTestJob(JobStatus.Pending);
        job.OrderId = orderId;
        var service = new Mock<IJobService>();
        _ = service
            .Setup(jobService => jobService.GetJobsByOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([job]);
        var controller = new JobController(service.Object, NullLogger<JobController>.Instance);

        ActionResult<IReadOnlyList<JobDto>> result = await controller.GetJobsByOrder(orderId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var jobs = Assert.IsAssignableFrom<IReadOnlyList<JobDto>>(okResult.Value);
        var returnedJob = Assert.Single(jobs);
        Assert.Equal(job.Id, returnedJob.JobId);
        Assert.Equal(orderId, returnedJob.OrderId);
    }

    private static Job CreateTestJob(JobStatus status)
    {
        return new Job
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OrderItemId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            Technology = "FDM",
            VolumeCm3 = 100,
            EstimatedPrintTimeMinutes = 120,
            Priority = 5,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
