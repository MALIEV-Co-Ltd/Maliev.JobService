using Maliev.JobService.Domain.Entities;
using Xunit;

namespace Maliev.JobService.Tests.Unit;

public class JobLogicTests
{
    [Fact]
    public void CompletedStatusDocumentation_RoutesProductionCompletionToQualityReview()
    {
        var source = File.ReadAllText(FindRepoFile("Maliev.JobService.Domain", "Entities", "JobStatus.cs"));

        Assert.Contains("quality review", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ready for shipping", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateJobsForPaidOrderAsync_RequiresLockedProductionSnapshots()
    {
        var source = File.ReadAllText(FindRepoFile("Maliev.JobService.Infrastructure", "Services", "JobService.cs"));
        var methodBody = ExtractMethodSource(source, "private async Task<int> CreateJobsForPaidOrderAsync(");

        Assert.Contains("ValidateLockedProductionSnapshots(item);", methodBody, StringComparison.Ordinal);
        Assert.Contains("MaterialSnapshotJson is required", source, StringComparison.Ordinal);
        Assert.Contains("ConfigurationSnapshotJson is required", source, StringComparison.Ordinal);
    }

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

        var daysRemaining = (deliveryDate.Value - DateTime.UtcNow).TotalDays;
        return Math.Max(0, (int)Math.Floor(daysRemaining));
    }

    private static string FindRepoFile(params string[] pathParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine([current.FullName, .. pathParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(pathParts));
    }

    private static string ExtractMethodSource(string source, string methodSignature)
    {
        var methodStart = source.IndexOf(methodSignature, StringComparison.Ordinal);
        Assert.True(methodStart >= 0, $"Could not find {methodSignature} source.");

        var openingBrace = source.IndexOf('{', methodStart);
        Assert.True(openingBrace > methodStart, $"Could not find opening brace for {methodSignature}.");

        var depth = 0;
        for (var index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[methodStart..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Could not isolate {methodSignature} source.");
    }
}
