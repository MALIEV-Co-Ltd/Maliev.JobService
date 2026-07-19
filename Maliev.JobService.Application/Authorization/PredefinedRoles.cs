namespace Maliev.JobService.Application.Authorization;

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
