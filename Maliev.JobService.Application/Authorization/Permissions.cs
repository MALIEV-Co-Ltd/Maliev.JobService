namespace Maliev.JobService.Application.Authorization;

/// <summary>
/// Defines the permissions for the Job Service.
/// </summary>
public static class JobPermissions
{
    /// <summary>Permission to read job status and details.</summary>
    public const string JobsRead = "job.jobs.read";
    /// <summary>Permission to update job status and progress.</summary>
    public const string JobsWrite = "job.jobs.write";
    /// <summary>Permission to manage and assign jobs to machines.</summary>
    public const string JobsManage = "job.jobs.manage";
    /// <summary>Permission to read machine status and availability.</summary>
    public const string MachinesRead = "job.machines.read";

    /// <summary>
    /// Collection of all defined job permissions with descriptions.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { JobsRead, "Read job status and details" },
        { JobsWrite, "Update job status and progress" },
        { JobsManage, "Manage and assign jobs to machines" },
        { MachinesRead, "Read machine status and availability" }
    };

    /// <summary>
    /// Gets all defined permission codes.
    /// </summary>
    public static string[] All => AllWithDescriptions.Keys.ToArray();
}
