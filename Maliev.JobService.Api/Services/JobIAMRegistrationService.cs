using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.JobService.Application.Authorization;

namespace Maliev.JobService.Api.Services;

/// <summary>
/// Background service to register permissions and roles with the IAM service on startup.
/// </summary>
public class JobIAMRegistrationService : IAMRegistrationService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobIAMRegistrationService"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    public JobIAMRegistrationService(IConfiguration configuration, ILogger<JobIAMRegistrationService> logger)
        : base(configuration, logger, "job")
    {
    }

    /// <inheritdoc/>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return JobPermissions.AllWithDescriptions.Select(p => new PermissionRegistration
        {
            PermissionId = p.Key,
            Description = p.Value
        });
    }

    /// <inheritdoc/>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return JobPredefinedRoles.All.Select(r => new RoleRegistration
        {
            RoleId = r.RoleId,
            Description = r.Description,
            PermissionIds = r.Permissions.ToList(),
            IsCustom = false
        });
    }
}
