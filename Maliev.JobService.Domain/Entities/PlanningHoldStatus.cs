namespace Maliev.JobService.Domain.Entities;

/// <summary>
/// Lifecycle status for a tentative production planning hold.
/// </summary>
public enum PlanningHoldStatus
{
    /// <summary>The hold reserves tentative capacity and has not expired.</summary>
    Active = 1,

    /// <summary>The hold was manually cancelled before conversion.</summary>
    Cancelled = 2,

    /// <summary>The hold expired automatically before conversion.</summary>
    Expired = 3,

    /// <summary>The hold was converted into a real manufacturing job.</summary>
    Converted = 4
}
