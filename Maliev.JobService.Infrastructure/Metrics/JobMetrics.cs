using System.Diagnostics.Metrics;
using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Infrastructure.Metrics;

/// <summary>
/// Collects Prometheus/OpenTelemetry metrics for job operations.
/// </summary>
public class JobMetrics
{
    private readonly Counter<long> _jobsCreatedCounter;
    private readonly Histogram<double> _transitionDurationHistogram;
    private readonly Counter<long> _jobsByStatusCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobMetrics"/> class.
    /// </summary>
    /// <param name="meterFactory">The meter factory.</param>
    public JobMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Maliev.JobService");

        _jobsCreatedCounter = meter.CreateCounter<long>("jobs_created_total", "count", "Total number of jobs created");
        _transitionDurationHistogram = meter.CreateHistogram<double>("job_transition_duration_seconds", "seconds", "Duration of job status transitions");
        _jobsByStatusCounter = meter.CreateCounter<long>("jobs_by_status", "count", "Jobs count by status");
    }

    /// <summary>
    /// Records job creation count.
    /// </summary>
    /// <param name="count">The number of created jobs.</param>
    public void RecordJobCreated(int count = 1)
    {
        _jobsCreatedCounter.Add(count);
    }

    /// <summary>
    /// Records a job state transition duration.
    /// </summary>
    /// <param name="fromStatus">The previous status.</param>
    /// <param name="toStatus">The new status.</param>
    /// <param name="duration">The transition duration.</param>
    public void RecordTransition(string fromStatus, string toStatus, TimeSpan duration)
    {
        _transitionDurationHistogram.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>("from_status", fromStatus),
            new KeyValuePair<string, object?>("to_status", toStatus));
    }

    /// <summary>
    /// Records observed job count for a status.
    /// </summary>
    /// <param name="status">The job status.</param>
    /// <param name="count">The count value.</param>
    public void RecordJobsByStatus(JobStatus status, int count)
    {
        _jobsByStatusCounter.Add(count, new KeyValuePair<string, object?>("status", status.ToString()));
    }
}
