using Azma.Auth;
using Azma.Auth.ProjectAccess;
using Azma.Common.Pagination;
using Azma.Common.Results;
using Azma.EventBus.Contracts.Identity;
using Azma.Projects.Api.Application.Common;
using Azma.Projects.Api.Domain;
using Azma.Projects.Api.Infrastructure.Persistence;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Projects.Api.Application.Members;

// ─── DTO ─────────────────────────────────────────────────────────────────
public sealed record ProjectMemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string EffectiveRole,
    string Source,                  // "workspace" | "override" | "excluded"
    string? OverrideRole,           // when source == override or excluded
    DateTime? AddedAtUtc,
    bool IsActive);

// Response for list endpoint including current user's project role
public sealed record ProjectMembersListResponse(
    IReadOnlyList<ProjectMemberDto> Members,
    string? CurrentUserProjectRole);  // "Owner" | "Admin" | "Tester" | "Viewer" | "Excluded" | null

public sealed record ProjectMemberCandidateDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    bool IsInProject,
    string? ProjectRole);

// ─── List Project Members ────────────────────────────────────────────────
public sealed record ListProjectMembersQuery(
    Guid WorkspaceId,
    Guid ProjectId,
    Guid CurrentUserId,
    string WorkspaceRole,
    string AccessScope) : IRequest<Result<ProjectMembersListResponse>>;

public sealed class ListProjectMembersHandler
    : IRequestHandler<ListProjectMembersQuery, Result<ProjectMembersListResponse>>
{
    private readonly ProjectsDbContext _db;
    public ListProjectMembersHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<ProjectMembersListResponse>> Handle(
        ListProjectMembersQuery q, CancellationToken ct)
    {
        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == q.ProjectId && p.WorkspaceId == q.WorkspaceId, ct);
        if (project is null)
            return Result.Failure<ProjectMembersListResponse>(ProjectErrors.ProjectNotFound);

        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.AccessScope,
                q.ProjectId,
                ct,
                q.WorkspaceRole))
            return Result.Failure<ProjectMembersListResponse>(ProjectAccessErrors.Denied);

        // Get current user's project role
        var currentUserMembership = await _db.ProjectMemberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProjectId == q.ProjectId && m.UserId == q.CurrentUserId, ct);
        var currentUserProjectRole = currentUserMembership?.Role.ToString();

        // Pull all workspace users from the read-model and join with overrides for this project.
        var users = await _db.UserDirectory.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

        var overrides = await _db.ProjectMemberships.AsNoTracking()
            .Where(m => m.ProjectId == q.ProjectId)
            .ToListAsync(ct);

        var overrideMap = overrides.ToDictionary(m => m.UserId);

        // Workspace role for the *displayed* members defaults to "Tester" (we don't have per-user
        // workspace role in the read-model). We render override info accurately and tag inherited
        // rows just as "workspace" since the actual workspace role lives in Identity service. The
        // Members page in workspace settings shows the precise workspace role.
        var list = users.Select(u =>
        {
            if (overrideMap.TryGetValue(u.Id, out var ov))
            {
                if (ov.Role == ProjectMembershipRole.Excluded)
                {
                    return new ProjectMemberDto(
                        u.Id, u.Email, u.DisplayName,
                        u.AvatarObjectKey is null ? null : $"/api/users/{u.Id}/avatar?v={u.UpdatedAtUtc.Ticks}",
                        EffectiveRole: "Excluded",
                        Source: "excluded",
                        OverrideRole: "Excluded",
                        AddedAtUtc: ov.AddedAtUtc,
                        IsActive: u.IsActive);
                }

                return new ProjectMemberDto(
                    u.Id, u.Email, u.DisplayName,
                    u.AvatarObjectKey is null ? null : $"/api/users/{u.Id}/avatar?v={u.UpdatedAtUtc.Ticks}",
                    EffectiveRole: ov.Role.ToString(),
                    Source: "override",
                    OverrideRole: ov.Role.ToString(),
                    AddedAtUtc: ov.AddedAtUtc,
                    IsActive: u.IsActive);
            }

            return new ProjectMemberDto(
                u.Id, u.Email, u.DisplayName,
                u.AvatarObjectKey is null ? null : $"/api/users/{u.Id}/avatar?v={u.UpdatedAtUtc.Ticks}",
                EffectiveRole: "Inherited",
                Source: "workspace",
                OverrideRole: null,
                AddedAtUtc: null,
                IsActive: u.IsActive);
        }).ToList();

        return Result.Success<ProjectMembersListResponse>(new ProjectMembersListResponse(list, currentUserProjectRole));
    }
}

// ─── List Global User Candidates ─────────────────────────────────────────────
public sealed record ListProjectMemberCandidatesQuery(
    Guid WorkspaceId,
    Guid ProjectId,
    Guid CurrentUserId,
    string CurrentUserRole,
    string? Search,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<ProjectMemberCandidateDto>>>;

public sealed class ListProjectMemberCandidatesHandler
    : IRequestHandler<ListProjectMemberCandidatesQuery, Result<PagedResult<ProjectMemberCandidateDto>>>
{
    private readonly ProjectsDbContext _db;
    public ListProjectMemberCandidatesHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<PagedResult<ProjectMemberCandidateDto>>> Handle(
        ListProjectMemberCandidatesQuery q, CancellationToken ct)
    {
        var projectExists = await _db.Projects.AsNoTracking()
            .AnyAsync(p => p.Id == q.ProjectId && p.WorkspaceId == q.WorkspaceId, ct);
        if (!projectExists)
            return Result.Failure<PagedResult<ProjectMemberCandidateDto>>(ProjectErrors.ProjectNotFound);

        if (!await ProjectMemberAuthorization.CanManageMembersAsync(
                _db, q.WorkspaceId, q.ProjectId, q.CurrentUserId, q.CurrentUserRole, ct))
            return Result.Failure<PagedResult<ProjectMemberCandidateDto>>(ProjectErrors.AdminOnly);

        var page = new PageRequest(q.Page, q.PageSize);
        var users = _db.UserDirectory.AsNoTracking()
            .Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var like = $"%{q.Search.Trim()}%";
            users = users.Where(u =>
                EF.Functions.ILike(u.DisplayName, like) ||
                EF.Functions.ILike(u.Email, like));
        }

        var total = await users.LongCountAsync(ct);

        var overrides = await _db.ProjectMemberships.AsNoTracking()
            .Where(m => m.ProjectId == q.ProjectId && m.WorkspaceId == q.WorkspaceId)
            .ToDictionaryAsync(m => m.UserId, ct);

        var items = await users
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Email)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.AvatarObjectKey,
                u.UpdatedAtUtc,
            })
            .ToListAsync(ct);

        var dtos = items.Select(u =>
        {
            var inProject = overrides.TryGetValue(u.Id, out var membership);
            return new ProjectMemberCandidateDto(
                u.Id,
                u.Email,
                u.DisplayName,
                u.AvatarObjectKey is null ? null : $"/api/users/{u.Id}/avatar?v={u.UpdatedAtUtc.Ticks}",
                inProject,
                membership?.Role.ToString());
        }).ToList();

        return new PagedResult<ProjectMemberCandidateDto>(dtos, page.Page, page.Take, total);
    }
}

// ─── List Project Roles For A User ───────────────────────────────────────
public sealed record UserProjectRoleDto(
    Guid ProjectId,
    string ProjectName,
    string ProjectCode,
    string Role);

public sealed record ListUserProjectRolesQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserRole,
    Guid TargetUserId) : IRequest<Result<IReadOnlyList<UserProjectRoleDto>>>;

public sealed class ListUserProjectRolesHandler
    : IRequestHandler<ListUserProjectRolesQuery, Result<IReadOnlyList<UserProjectRoleDto>>>
{
    private readonly ProjectsDbContext _db;
    public ListUserProjectRolesHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<UserProjectRoleDto>>> Handle(
        ListUserProjectRolesQuery q, CancellationToken ct)
    {
        if (q.CurrentUserId != q.TargetUserId && !ProjectAccessPolicy.IsWorkspaceAdmin(q.CurrentUserRole))
            return Result.Failure<IReadOnlyList<UserProjectRoleDto>>(ProjectErrors.AdminOnly);

        var roles = await _db.ProjectMemberships.AsNoTracking()
            .Where(m => m.WorkspaceId == q.WorkspaceId && m.UserId == q.TargetUserId)
            .Join(_db.Projects.AsNoTracking(),
                m => m.ProjectId,
                p => p.Id,
                (m, p) => new UserProjectRoleDto(p.Id, p.Name, p.Code, m.Role.ToString()))
            .OrderBy(r => r.ProjectCode)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<UserProjectRoleDto>>(roles);
    }
}

// ─── Set Project Member Role (override) ─────────────────────────────────
public sealed record AddProjectMemberCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserRole,
    Guid ProjectId,
    Guid TargetUserId,
    ProjectMembershipRole Role) : IRequest<Result<ProjectMemberDto>>;

public sealed class AddProjectMemberValidator : AbstractValidator<AddProjectMemberCommand>
{
    public AddProjectMemberValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum().NotEqual(ProjectMembershipRole.Owner)
            .WithMessage("Use ownership transfer to assign the project owner.");
    }
}

public sealed class AddProjectMemberHandler
    : IRequestHandler<AddProjectMemberCommand, Result<ProjectMemberDto>>
{
    private readonly ProjectsDbContext _db;
    private readonly IPublishEndpoint _publisher;

    public AddProjectMemberHandler(ProjectsDbContext db, IPublishEndpoint publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<Result<ProjectMemberDto>> Handle(AddProjectMemberCommand cmd, CancellationToken ct)
    {
        if (cmd.Role == ProjectMembershipRole.Owner)
            return Result.Failure<ProjectMemberDto>(ProjectErrors.CannotSetOwnerDirectly);

        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == cmd.ProjectId && p.WorkspaceId == cmd.WorkspaceId, ct);
        if (project is null)
            return Result.Failure<ProjectMemberDto>(ProjectErrors.ProjectNotFound);

        if (!await ProjectMemberAuthorization.CanManageMembersAsync(
                _db, cmd.WorkspaceId, cmd.ProjectId, cmd.CurrentUserId, cmd.CurrentUserRole, ct))
            return Result.Failure<ProjectMemberDto>(ProjectErrors.AdminOnly);

        var user = await _db.UserDirectory.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == cmd.TargetUserId, ct);
        if (user is null)
            return Result.Failure<ProjectMemberDto>(ProjectErrors.UserNotFound);
        if (!user.IsActive)
            return Result.Failure<ProjectMemberDto>(ProjectErrors.UserInactive);

        var existing = await _db.ProjectMemberships
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == cmd.WorkspaceId &&
                m.ProjectId == cmd.ProjectId &&
                m.UserId == cmd.TargetUserId, ct);

        if (existing is null)
        {
            existing = ProjectMembership.Create(
                cmd.WorkspaceId, cmd.ProjectId, cmd.TargetUserId, cmd.Role, cmd.CurrentUserId);
            _db.ProjectMemberships.Add(existing);
        }
        else
        {
            if (existing.Role == ProjectMembershipRole.Owner)
                return Result.Failure<ProjectMemberDto>(ProjectErrors.CannotModifyProjectOwner);
            existing.ChangeRole(cmd.Role);
        }

        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new EnsureWorkspaceMemberRequested(
            cmd.WorkspaceId,
            cmd.TargetUserId,
            AzmaRoles.Viewer,
            AzmaAccessScopes.ProjectScoped,
            cmd.CurrentUserId), ct);

        var avatarUrl = user.AvatarObjectKey is null
            ? null
            : $"/api/users/{user.Id}/avatar?v={user.UpdatedAtUtc.Ticks}";

        return cmd.Role == ProjectMembershipRole.Excluded
            ? new ProjectMemberDto(user.Id, user.Email, user.DisplayName, avatarUrl,
                "Excluded", "excluded", "Excluded", existing.AddedAtUtc, user.IsActive)
            : new ProjectMemberDto(user.Id, user.Email, user.DisplayName, avatarUrl,
                cmd.Role.ToString(), "override", cmd.Role.ToString(), existing.AddedAtUtc, user.IsActive);
    }
}

public sealed record SetProjectMemberRoleCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserRole,
    Guid ProjectId,
    Guid TargetUserId,
    ProjectMembershipRole Role) : IRequest<Result<ProjectMemberDto>>;

public sealed class SetProjectMemberRoleValidator : AbstractValidator<SetProjectMemberRoleCommand>
{
    public SetProjectMemberRoleValidator()
    {
        RuleFor(x => x.Role).IsInEnum().NotEqual(ProjectMembershipRole.Owner)
            .WithMessage("Use ownership transfer to assign the project owner.");
    }
}

public sealed class SetProjectMemberRoleHandler
    : IRequestHandler<SetProjectMemberRoleCommand, Result<ProjectMemberDto>>
{
    private readonly ProjectsDbContext _db;
    private readonly IPublishEndpoint _publisher;
    public SetProjectMemberRoleHandler(ProjectsDbContext db, IPublishEndpoint publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<Result<ProjectMemberDto>> Handle(SetProjectMemberRoleCommand cmd, CancellationToken ct)
    {
        if (cmd.Role == ProjectMembershipRole.Owner)
            return Result.Failure<ProjectMemberDto>(ProjectErrors.CannotSetOwnerDirectly);

        // Get user's project role override (if any)
        if (!await ProjectMemberAuthorization.CanManageMembersAsync(
                _db, cmd.WorkspaceId, cmd.ProjectId, cmd.CurrentUserId, cmd.CurrentUserRole, ct))
            return Result.Failure<ProjectMemberDto>(ProjectErrors.AdminOnly);

        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == cmd.ProjectId && p.WorkspaceId == cmd.WorkspaceId, ct);
        if (project is null)
            return Result.Failure<ProjectMemberDto>(ProjectErrors.ProjectNotFound);

        var user = await _db.UserDirectory.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == cmd.TargetUserId, ct);
        if (user is null)
            return Result.Failure<ProjectMemberDto>(ProjectErrors.UserNotFound);
        if (!user.IsActive)
            return Result.Failure<ProjectMemberDto>(ProjectErrors.UserInactive);

        var existing = await _db.ProjectMemberships
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == cmd.WorkspaceId &&
                m.ProjectId == cmd.ProjectId &&
                m.UserId == cmd.TargetUserId, ct);

        if (existing is null)
        {
            existing = ProjectMembership.Create(
                cmd.WorkspaceId, cmd.ProjectId, cmd.TargetUserId, cmd.Role, cmd.CurrentUserId);
            _db.ProjectMemberships.Add(existing);
        }
        else
        {
            if (existing.Role == ProjectMembershipRole.Owner)
                return Result.Failure<ProjectMemberDto>(ProjectErrors.CannotModifyProjectOwner);
            existing.ChangeRole(cmd.Role);
        }

        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new EnsureWorkspaceMemberRequested(
            cmd.WorkspaceId,
            cmd.TargetUserId,
            AzmaRoles.Viewer,
            AzmaAccessScopes.ProjectScoped,
            cmd.CurrentUserId), ct);

        var avatarUrl = user.AvatarObjectKey is null
            ? null
            : $"/api/users/{user.Id}/avatar?v={user.UpdatedAtUtc.Ticks}";

        return cmd.Role == ProjectMembershipRole.Excluded
            ? new ProjectMemberDto(user.Id, user.Email, user.DisplayName, avatarUrl,
                "Excluded", "excluded", "Excluded", existing.AddedAtUtc, user.IsActive)
            : new ProjectMemberDto(user.Id, user.Email, user.DisplayName, avatarUrl,
                cmd.Role.ToString(), "override", cmd.Role.ToString(), existing.AddedAtUtc, user.IsActive);
    }
}

// ─── Reset Member To Workspace (delete override) ────────────────────────
public sealed record TransferProjectOwnershipCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    Guid ProjectId,
    Guid NewOwnerUserId) : IRequest<Result>;

public sealed class TransferProjectOwnershipHandler
    : IRequestHandler<TransferProjectOwnershipCommand, Result>
{
    private readonly ProjectsDbContext _db;
    private readonly IPublishEndpoint _publisher;

    public TransferProjectOwnershipHandler(ProjectsDbContext db, IPublishEndpoint publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<Result> Handle(TransferProjectOwnershipCommand cmd, CancellationToken ct)
    {
        var projectExists = await _db.Projects.AsNoTracking()
            .AnyAsync(p => p.Id == cmd.ProjectId && p.WorkspaceId == cmd.WorkspaceId, ct);
        if (!projectExists)
            return Result.Failure(ProjectErrors.ProjectNotFound);

        if (cmd.NewOwnerUserId == cmd.CurrentUserId)
            return Result.Success();

        var targetUser = await _db.UserDirectory.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == cmd.NewOwnerUserId, ct);
        if (targetUser is null)
            return Result.Failure(ProjectErrors.UserNotFound);
        if (!targetUser.IsActive)
            return Result.Failure(ProjectErrors.UserInactive);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var currentOwner = await _db.ProjectMemberships
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == cmd.WorkspaceId &&
                m.ProjectId == cmd.ProjectId &&
                m.UserId == cmd.CurrentUserId &&
                m.Role == ProjectMembershipRole.Owner, ct);

        if (currentOwner is null)
            return Result.Failure(ProjectErrors.ProjectOwnerOnly);

        var targetMembership = await _db.ProjectMemberships
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == cmd.WorkspaceId &&
                m.ProjectId == cmd.ProjectId &&
                m.UserId == cmd.NewOwnerUserId, ct);

        currentOwner.ChangeRole(ProjectMembershipRole.Admin);

        if (targetMembership is null)
        {
            targetMembership = ProjectMembership.Create(
                cmd.WorkspaceId,
                cmd.ProjectId,
                cmd.NewOwnerUserId,
                ProjectMembershipRole.Owner,
                cmd.CurrentUserId);
            _db.ProjectMemberships.Add(targetMembership);
        }
        else
        {
            targetMembership.ChangeRole(ProjectMembershipRole.Owner);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _publisher.Publish(new EnsureWorkspaceMemberRequested(
            cmd.WorkspaceId,
            cmd.NewOwnerUserId,
            AzmaRoles.Viewer,
            AzmaAccessScopes.ProjectScoped,
            cmd.CurrentUserId), ct);

        return Result.Success();
    }
}

public sealed record ResetProjectMemberCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserRole,
    Guid ProjectId,
    Guid TargetUserId) : IRequest<Result>;

public sealed class ResetProjectMemberHandler : IRequestHandler<ResetProjectMemberCommand, Result>
{
    private readonly ProjectsDbContext _db;
    public ResetProjectMemberHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result> Handle(ResetProjectMemberCommand cmd, CancellationToken ct)
    {
        // Get user's project role override (if any)
        if (!await ProjectMemberAuthorization.CanManageMembersAsync(
                _db, cmd.WorkspaceId, cmd.ProjectId, cmd.CurrentUserId, cmd.CurrentUserRole, ct))
            return Result.Failure(ProjectErrors.AdminOnly);

        var existing = await _db.ProjectMemberships
            .FirstOrDefaultAsync(m =>
                m.ProjectId == cmd.ProjectId &&
                m.UserId == cmd.TargetUserId &&
                m.WorkspaceId == cmd.WorkspaceId, ct);

        if (existing is null)
            return Result.Success(); // already inheriting

        if (existing.Role == ProjectMembershipRole.Owner)
            return Result.Failure(ProjectErrors.CannotModifyProjectOwner);

        _db.ProjectMemberships.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal static class ProjectMemberAuthorization
{
    public static async Task<bool> CanManageMembersAsync(
        ProjectsDbContext db,
        Guid workspaceId,
        Guid projectId,
        Guid userId,
        string workspaceRole,
        CancellationToken ct)
    {
        if (ProjectAccessPolicy.IsWorkspaceAdmin(workspaceRole))
            return true;

        var membership = await db.ProjectMemberships.AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == workspaceId &&
                m.ProjectId == projectId &&
                m.UserId == userId, ct);

        return membership is { Role: ProjectMembershipRole.Owner or ProjectMembershipRole.Admin };
    }

    public static Task<bool> IsProjectOwnerAsync(
        ProjectsDbContext db,
        Guid workspaceId,
        Guid projectId,
        Guid userId,
        CancellationToken ct) =>
        db.ProjectMemberships.AsNoTracking()
            .AnyAsync(m =>
                m.WorkspaceId == workspaceId &&
                m.ProjectId == projectId &&
                m.UserId == userId &&
                m.Role == ProjectMembershipRole.Owner, ct);
}
