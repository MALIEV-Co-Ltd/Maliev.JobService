namespace Maliev.JobService.Api.Authorization;

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

/// <summary>
/// Provides access to predefined roles for the Job Service.
/// </summary>
public static class JobPredefinedRoles
{
    /// <summary>Role for administrators with full control.</summary>
    public const string Admin = "roles.job.admin";
    /// <summary>Role for shop floor operators.</summary>
    public const string Operator = "roles.job.operator";
    /// <summary>Role for users with read-only access.</summary>
    public const string Viewer = "roles.job.viewer";

    /// <summary>
    /// Collection of all predefined roles for the Job Service.
    /// </summary>
    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (Admin, "Full administrative control over job service", JobPermissions.All),
        (Operator, "Update job status and progress", new[]
        {
            JobPermissions.JobsRead,
            JobPermissions.JobsWrite,
            JobPermissions.MachinesRead
        }),
        (Viewer, "Read-only access to jobs and machines", new[]
        {
            JobPermissions.JobsRead,
            JobPermissions.MachinesRead
        })
    };
}
