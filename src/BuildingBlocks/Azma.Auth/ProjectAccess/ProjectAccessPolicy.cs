using System.Data.Common;

namespace Azma.Auth.ProjectAccess;

public sealed class ProjectVisibility
{
    public static readonly ProjectVisibility Unrestricted = new(null, new HashSet<Guid>());

    public ProjectVisibility(IReadOnlySet<Guid>? allowedProjectIds, IReadOnlySet<Guid> deniedProjectIds)
    {
        AllowedProjectIds = allowedProjectIds;
        DeniedProjectIds = deniedProjectIds;
    }

    /// <summary>Null means every workspace project is allowed, except <see cref="DeniedProjectIds"/>.</summary>
    public IReadOnlySet<Guid>? AllowedProjectIds { get; }

    public IReadOnlySet<Guid> DeniedProjectIds { get; }

    public bool Contains(Guid projectId) =>
        (AllowedProjectIds is null || AllowedProjectIds.Contains(projectId))
        && !DeniedProjectIds.Contains(projectId);

    public Guid[]? AllowedArray() => AllowedProjectIds?.ToArray();

    public Guid[] DeniedArray() =>
        DeniedProjectIds.Count == 0 ? Array.Empty<Guid>() : DeniedProjectIds.ToArray();
}

public static class ProjectAccessPolicy
{
    private const int OwnerRole = 0;
    private const int AdminRole = 1;
    private const int TesterRole = 2;
    private const int ViewerRole = 3;
    private const int ExcludedRole = 99;

    public static bool IsProjectScoped(string? accessScope) =>
        string.Equals(accessScope, AzmaAccessScopes.ProjectScoped, StringComparison.OrdinalIgnoreCase);

    public static bool IsWorkspaceAdmin(string? workspaceRole) =>
        string.Equals(workspaceRole, AzmaRoles.Owner, StringComparison.OrdinalIgnoreCase)
        || string.Equals(workspaceRole, AzmaRoles.Admin, StringComparison.OrdinalIgnoreCase);

    public static int Rank(string? roleName) => roleName?.Trim().ToLowerInvariant() switch
    {
        "owner" => (int)ProjectCapability.Own,
        "admin" or "manager" => (int)ProjectCapability.Manage,
        "tester" => (int)ProjectCapability.Contribute,
        "viewer" => (int)ProjectCapability.View,
        _ => 0,
    };

    public static int Rank(int storedRole) => storedRole switch
    {
        OwnerRole => (int)ProjectCapability.Own,
        AdminRole => (int)ProjectCapability.Manage,
        TesterRole => (int)ProjectCapability.Contribute,
        ViewerRole => (int)ProjectCapability.View,
        _ => 0,
    };

    public static Task<bool> CanAccessProjectAsync(
        DbConnection connection,
        Guid workspaceId,
        Guid userId,
        string? accessScope,
        Guid projectId,
        CancellationToken ct,
        string? workspaceRole = null,
        ProjectCapability required = ProjectCapability.View) =>
        HasCapabilityAsync(connection, workspaceId, userId, accessScope, workspaceRole, projectId, required, ct);

    public static Task<bool> CanMutateProjectAsync(
        DbConnection connection,
        Guid workspaceId,
        Guid userId,
        string? accessScope,
        string? workspaceRole,
        Guid projectId,
        CancellationToken ct) =>
        HasCapabilityAsync(
            connection, workspaceId, userId, accessScope, workspaceRole, projectId,
            ProjectCapability.Contribute, ct);

    public static Task<bool> CanManageProjectAsync(
        DbConnection connection,
        Guid workspaceId,
        Guid userId,
        string? accessScope,
        string? workspaceRole,
        Guid projectId,
        CancellationToken ct) =>
        HasCapabilityAsync(
            connection, workspaceId, userId, accessScope, workspaceRole, projectId,
            ProjectCapability.Manage, ct);

    public static async Task<bool> HasCapabilityAsync(
        DbConnection connection,
        Guid workspaceId,
        Guid userId,
        string? accessScope,
        string? workspaceRole,
        Guid projectId,
        ProjectCapability required,
        CancellationToken ct)
    {
        var effective = await ResolveEffectiveRankAsync(
            connection, workspaceId, userId, accessScope, workspaceRole, projectId, ct);
        return effective >= (int)required;
    }

    public static async Task<ProjectVisibility> GetVisibilityAsync(
        DbConnection connection,
        Guid workspaceId,
        Guid userId,
        string? accessScope,
        string? workspaceRole,
        CancellationToken ct)
    {
        // Workspace owner/admin must still see every project so they can assign roles,
        // including projects where they (or others) have an Excluded override.
        if (IsWorkspaceAdmin(workspaceRole))
            return ProjectVisibility.Unrestricted;

        await using var _ = await OpenIfNeededAsync(connection, ct);

        if (IsProjectScoped(accessScope))
        {
            var allowed = new HashSet<Guid>();
            var denied = new HashSet<Guid>();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT "ProjectId", "Role"
                FROM projects.project_memberships
                WHERE "WorkspaceId" = @workspaceId
                  AND "UserId" = @userId
                """;
            Add(cmd, "workspaceId", workspaceId);
            Add(cmd, "userId", userId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var projectId = reader.GetGuid(0);
                var role = reader.GetInt32(1);
                if (role == ExcludedRole)
                    denied.Add(projectId);
                else
                    allowed.Add(projectId);
            }

            return new ProjectVisibility(allowed, denied);
        }

        var excluded = new HashSet<Guid>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT "ProjectId"
                FROM projects.project_memberships
                WHERE "WorkspaceId" = @workspaceId
                  AND "UserId" = @userId
                  AND "Role" = @excludedRole
                """;
            Add(cmd, "workspaceId", workspaceId);
            Add(cmd, "userId", userId);
            Add(cmd, "excludedRole", ExcludedRole);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                excluded.Add(reader.GetGuid(0));
        }

        return excluded.Count == 0
            ? ProjectVisibility.Unrestricted
            : new ProjectVisibility(null, excluded);
    }

    public static async Task<IReadOnlySet<Guid>?> GetAccessibleProjectIdsAsync(
        DbConnection connection,
        Guid workspaceId,
        Guid userId,
        string? accessScope,
        CancellationToken ct,
        string? workspaceRole = null)
    {
        var visibility = await GetVisibilityAsync(
            connection, workspaceId, userId, accessScope, workspaceRole, ct);
        return visibility.AllowedProjectIds;
    }

    private static async Task<int> ResolveEffectiveRankAsync(
        DbConnection connection,
        Guid workspaceId,
        Guid userId,
        string? accessScope,
        string? workspaceRole,
        Guid projectId,
        CancellationToken ct)
    {
        await using var _ = await OpenIfNeededAsync(connection, ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT "Role"
            FROM projects.project_memberships
            WHERE "WorkspaceId" = @workspaceId
              AND "ProjectId" = @projectId
              AND "UserId" = @userId
            LIMIT 1
            """;
        Add(cmd, "workspaceId", workspaceId);
        Add(cmd, "projectId", projectId);
        Add(cmd, "userId", userId);

        var scalar = await cmd.ExecuteScalarAsync(ct);
        if (scalar is not null && scalar is not DBNull)
        {
            var stored = Convert.ToInt32(scalar);
            if (stored == ExcludedRole)
            {
                // Workspace admins keep manage rights so they can recover access / assign roles.
                return IsWorkspaceAdmin(workspaceRole) ? (int)ProjectCapability.Manage : 0;
            }
            return Rank(stored);
        }

        if (IsProjectScoped(accessScope))
            return IsWorkspaceAdmin(workspaceRole) ? (int)ProjectCapability.Manage : 0;

        var inherited = Rank(workspaceRole);
        return inherited == 0 ? (int)ProjectCapability.View : inherited;
    }

    private static void Add(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static async Task<IAsyncDisposable> OpenIfNeededAsync(DbConnection connection, CancellationToken ct)
    {
        if (connection.State == System.Data.ConnectionState.Open)
            return NoopAsyncDisposable.Instance;

        await connection.OpenAsync(ct);
        return new CloseConnectionOnDispose(connection);
    }

    private sealed class CloseConnectionOnDispose(DbConnection connection) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            connection.Close();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        public static readonly NoopAsyncDisposable Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
