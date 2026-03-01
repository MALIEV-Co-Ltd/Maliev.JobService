using Maliev.JobService.Domain.Entities;
using Xunit;

namespace Maliev.JobService.Tests.Unit;

public class JobLogicTests
{
    [Theory]
    [InlineData(JobStatus.Pending, "queue", true)]
    [InlineData(JobStatus.Pending, "start", true)]
    [InlineData(JobStatus.InProgress, "finish", true)]
    [InlineData(JobStatus.Finishing, "complete", true)]
    [InlineData(JobStatus.Completed, "start", false)]
    [InlineData(JobStatus.InProgress, "queue", false)]
    public void ValidateTransition_ShouldReturnExpectedResult(JobStatus current, string action, bool expected)
    {
        // Arrange
        var validTransitions = new Dictionary<JobStatus, HashSet<string>>
        {
            [JobStatus.Pending] = new() { "queue", "start", "cancel" },
            [JobStatus.Queued] = new() { "start", "reassign", "cancel" },
            [JobStatus.InProgress] = new() { "finish", "cancel" },
            [JobStatus.Finishing] = new() { "complete", "cancel" },
            [JobStatus.Completed] = new() { "cancel" },
            [JobStatus.Cancelled] = new()
        };

        // Act
        var result = validTransitions.TryGetValue(current, out var allowedActions) && allowedActions.Contains(action);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculatePriority_ShouldReturnCorrectDays()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(5).AddMinutes(1);
        
        // Act
        var result = CalculatePriority(futureDate);

        // Assert
        Assert.Equal(5, result);
    }

    [Fact]
    public void CalculatePriority_ShouldClampToZeroForPastDate()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddDays(-5);
        
        // Act
        var result = CalculatePriority(pastDate);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculatePriority_ShouldReturn999ForNull()
    {
        // Act
        var result = CalculatePriority(null);

        // Assert
        Assert.Equal(999, result);
    }

    private static int CalculatePriority(DateTime? deliveryDate)
    {
        if (!deliveryDate.HasValue)
            return 999;
            
        var daysRemaining = (deliveryDate.Value - DateTime.UtcNow).Days;
        return Math.Max(0, daysRemaining);
    }
}
