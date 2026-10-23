using Microsoft.EntityFrameworkCore;

namespace Azma.Runs.Api.Infrastructure.Persistence;

public static class ProjectRefSynchronizer
{
    public static async Task<int> SyncFromProjectsAsync(
        RunsDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var updated = await db.Database.ExecuteSqlRawAsync("""
            UPDATE runs.project_refs pr
            SET
                "Code" = upper(p."Code"),
                "IsArchived" = p."ArchivedAtUtc" IS NOT NULL
            FROM projects.projects p
            WHERE pr."Id" = p."Id"
              AND pr."WorkspaceId" = p."WorkspaceId"
              AND (
                  pr."Code" <> upper(p."Code")
                  OR pr."IsArchived" <> (p."ArchivedAtUtc" IS NOT NULL)
              )
            """, cancellationToken);

        var inserted = await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO runs.project_refs ("Id", "WorkspaceId", "Code", "IsArchived", "NextRunSequence")
            SELECT p."Id", p."WorkspaceId", upper(p."Code"), p."ArchivedAtUtc" IS NOT NULL, 1
            FROM projects.projects p
            ON CONFLICT ("Id") DO NOTHING
            """, cancellationToken);

        if (inserted > 0 || updated > 0)
        {
            logger.LogInformation(
                "Synchronized project_refs from projects.projects. Inserted: {Inserted}, updated: {Updated}.",
                inserted,
                updated);
        }

        return inserted + updated;
    }

    public static Task<int> EnsureFromProjectsAsync(
        RunsDbContext db,
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO runs.project_refs ("Id", "WorkspaceId", "Code", "IsArchived", "NextRunSequence")
            SELECT p."Id", p."WorkspaceId", upper(p."Code"), p."ArchivedAtUtc" IS NOT NULL, 1
            FROM projects.projects p
            WHERE p."Id" = {projectId}
              AND p."WorkspaceId" = {workspaceId}
            ON CONFLICT ("Id") DO UPDATE
            SET
                "Code" = EXCLUDED."Code",
                "IsArchived" = EXCLUDED."IsArchived"
            WHERE runs.project_refs."WorkspaceId" = EXCLUDED."WorkspaceId"
            """, cancellationToken);
}
