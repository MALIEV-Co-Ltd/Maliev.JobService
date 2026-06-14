using Xunit;

using JobServiceImplementation = Maliev.JobService.Infrastructure.Services.JobService;

namespace Maliev.JobService.Tests.Unit.Services;

public class JobServiceValidationTests
{
    [Fact]
    public async Task QueueAsync_WithBlankMachineId_ReturnsFailureBeforePersistence()
    {
        var service = CreateServiceWithoutPersistence();

        var result = await service.QueueAsync(Guid.NewGuid(), "   ", "testuser");

        Assert.False(result.IsSuccess);
        Assert.Contains("MachineId", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReassignAsync_WithBlankMachineId_ReturnsFailureBeforePersistence()
    {
        var service = CreateServiceWithoutPersistence();

        var result = await service.ReassignAsync(Guid.NewGuid(), "   ");

        Assert.False(result.IsSuccess);
        Assert.Contains("MachineId", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelAsync_WithBlankReason_ReturnsFailureBeforePersistence()
    {
        var service = CreateServiceWithoutPersistence();

        var result = await service.CancelAsync(Guid.NewGuid(), "   ", "testuser");

        Assert.False(result.IsSuccess);
        Assert.Contains("reason", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static JobServiceImplementation CreateServiceWithoutPersistence() => new(
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        null!);
}
