using Azma.Auth.ProjectAccess;
using Azma.Common.Results;
using Azma.Projects.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Azma.Projects.Api.Application.Common;

internal static class ProjectGuard
{
    public static async Task<Error?> EnsureAsync(
        ProjectsDbContext db,
        Guid workspaceId,
        Guid userId,
        string accessScope,
        string role,
        Guid projectId,
        ProjectCapability required,
        CancellationToken ct)
    {
        var ok = await ProjectAccessPolicy.HasCapabilityAsync(
            db.Database.GetDbConnection(),
            workspaceId,
            userId,
            accessScope,
            role,
            projectId,
            required,
            ct);
        if (ok) return null;
        return required <= ProjectCapability.View
            ? ProjectAccessErrors.Denied
            : ProjectAccessErrors.InsufficientRole;
    }
}
