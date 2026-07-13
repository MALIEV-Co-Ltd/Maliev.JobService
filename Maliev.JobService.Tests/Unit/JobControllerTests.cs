using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.JobService.Api.Controllers;
using Maliev.JobService.Api.DTOs;
using Maliev.JobService.Application.Abstractions;
using Maliev.JobService.Application.Authorization;
using Maliev.JobService.Application.Models;
using Maliev.JobService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Maliev.JobService.Tests.Unit;

public class JobControllerTests
{
    [Fact]
    public async Task CreatePlanningHold_IntranetServiceIdentity_UsesTrustedDelegatedEmployeeActor()
    {
        var hold = CreateTestPlanningHold();
        string? recordedActor = null;
        var service = new Mock<IJobService>();
        _ = service
            .Setup(jobService => jobService.CreatePlanningHoldAsync(
                It.IsAny<CreatePlanningHoldCommand>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreatePlanningHoldCommand, string, CancellationToken>((_, actor, _) => recordedActor = actor)
            .ReturnsAsync(PlanningHoldOperationResult.Success(hold));
        var controller = CreateController(
            service.Object,
            [
                new Claim("sub", "system:service:intranetbff"),
                new Claim("service_name", "IntranetBff"),
                new Claim("user_type", "service")
            ],
            delegatedActor: "employee-123");

        _ = await controller.CreatePlanningHold(new CreateProductionPlanningHoldRequest(), CancellationToken.None);

        Assert.Equal("employee-123", recordedActor);
    }

    [Fact]
    public async Task CreatePlanningHold_EndUserCannotSpoofDelegatedActorHeader()
    {
        var hold = CreateTestPlanningHold();
        string? recordedActor = null;
        var service = new Mock<IJobService>();
        _ = service
            .Setup(jobService => jobService.CreatePlanningHoldAsync(
                It.IsAny<CreatePlanningHoldCommand>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreatePlanningHoldCommand, string, CancellationToken>((_, actor, _) => recordedActor = actor)
            .ReturnsAsync(PlanningHoldOperationResult.Success(hold));
        var controller = CreateController(
            service.Object,
            [new Claim(ClaimTypes.NameIdentifier, "employee-real")],
            delegatedActor: "employee-spoofed");

        _ = await controller.CreatePlanningHold(new CreateProductionPlanningHoldRequest(), CancellationToken.None);

        Assert.Equal("employee-real", recordedActor);
    }

    [Fact]
    public void GetPlanningHold_DeclaresExactVersionedResourceRoute()
    {
        var action = typeof(JobController).GetMethod("GetPlanningHold");

        Assert.NotNull(action);
        var route = Assert.Single(action.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false));
        Assert.Equal("planning-holds/{id:guid}", Assert.IsType<HttpGetAttribute>(route).Template);
        var permission = Assert.Single(action.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: false));
        Assert.Equal(JobPermissions.JobsRead, Assert.IsType<RequirePermissionAttribute>(permission).Permission);
    }

    [Fact]
    public void JobServiceContract_DeclaresExactPlanningHoldLookup()
    {
        var method = typeof(IJobService).GetMethod("GetPlanningHoldAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<ProductionPlanningHold?>), method.ReturnType);
        Assert.Collection(
            method.GetParameters(),
            parameter => Assert.Equal(typeof(Guid), parameter.ParameterType),
            parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));
    }

    [Fact]
    public async Task GetPlanningHold_ExistingHold_ReturnsExistingDtoWithProjectOwnership()
    {
        var hold = CreateTestPlanningHold();
        var service = new Mock<IJobService>();
        _ = service
            .Setup(jobService => jobService.GetPlanningHoldAsync(hold.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);
        var controller = new JobController(service.Object, NullLogger<JobController>.Instance);

        ActionResult<ProductionPlanningHoldDto> result = await controller.GetPlanningHold(
            hold.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ProductionPlanningHoldDto>(ok.Value);
        Assert.Equal(hold.Id, dto.Id);
        Assert.Equal(hold.ProjectId, dto.ProjectId);
        Assert.Equal(hold.ProjectPartId, dto.ProjectPartId);
    }

    [Fact]
    public async Task GetPlanningHold_UnknownHold_ReturnsNotFound()
    {
        var holdId = Guid.NewGuid();
        var service = new Mock<IJobService>();
        _ = service
            .Setup(jobService => jobService.GetPlanningHoldAsync(holdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionPlanningHold?)null);
        var controller = new JobController(service.Object, NullLogger<JobController>.Instance);

        ActionResult<ProductionPlanningHoldDto> result = await controller.GetPlanningHold(
            holdId,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

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

    private static ProductionPlanningHold CreateTestPlanningHold()
    {
        return new ProductionPlanningHold
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ProjectPartId = Guid.NewGuid(),
            Technology = "FDM",
            MachineId = "FDM-01",
            ScheduledStartTime = DateTime.UtcNow.AddHours(1),
            ScheduledEndTime = DateTime.UtcNow.AddHours(2),
            SetupTimeMinutes = 15,
            ProductionTimeMinutes = 45,
            Quantity = 1,
            Status = PlanningHoldStatus.Active,
            CreatedBy = "planner",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(72)
        };
    }

    private static JobController CreateController(
        IJobService service,
        IReadOnlyCollection<Claim> claims,
        string delegatedActor)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"))
        };
        context.Request.Headers["X-Maliev-Delegated-Actor-Id"] = delegatedActor;
        return new JobController(service, NullLogger<JobController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }
}
