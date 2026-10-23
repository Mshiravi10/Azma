using Azma.Auth;
using Azma.Auth.ProjectAccess;
using Azma.Common.Pagination;
using Azma.Common.Results;
using Azma.EventBus.Contracts.Projects;
using Azma.Projects.Api.Application.Common;
using Azma.Projects.Api.Domain;
using Azma.Projects.Api.Infrastructure.Persistence;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Projects.Api.Application.Projects;

// ─── Create ───────────────────────────────────────────────────────────────
public sealed record CreateProjectCommand(
    Guid WorkspaceId,
    Guid CreatedByUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    string Name,
    string Code,
    string? Description) : IRequest<Result<ProjectDto>>;

public sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(160);
        RuleFor(x => x.Code).NotEmpty().MinimumLength(2).MaximumLength(16)
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("کد فقط می‌تواند شامل حروف انگلیسی، عدد، _ و - باشد.");
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class CreateProjectHandler : IRequestHandler<CreateProjectCommand, Result<ProjectDto>>
{
    private readonly ProjectsDbContext _db;
    private readonly IPublishEndpoint _publisher;

    public CreateProjectHandler(ProjectsDbContext db, IPublishEndpoint publisher)
    {
        _db = db; _publisher = publisher;
    }

    public async Task<Result<ProjectDto>> Handle(CreateProjectCommand cmd, CancellationToken ct)
    {
        if (ProjectAccessPolicy.IsProjectScoped(cmd.CurrentUserAccessScope)
            || !ProjectAccessPolicy.IsWorkspaceAdmin(cmd.CurrentUserRole))
            return Result.Failure<ProjectDto>(ProjectAccessErrors.WorkspaceAdminOnly);

        var code = cmd.Code.Trim().ToUpperInvariant();

        var taken = await _db.Projects
            .AnyAsync(p => p.WorkspaceId == cmd.WorkspaceId && p.Code == code, ct);
        if (taken)
            return Result.Failure<ProjectDto>(ProjectErrors.CodeAlreadyTaken);

        var project = Project.Create(cmd.WorkspaceId, cmd.Name, code, cmd.Description, cmd.CreatedByUserId);

        // Seed a default suite so the project is immediately usable.
        var suite = Suite.Create(cmd.WorkspaceId, project.Id, "Master", null, 0);

        // Create Owner membership for the project creator
        var ownerMembership = ProjectMembership.Create(
            cmd.WorkspaceId, project.Id, cmd.CreatedByUserId, ProjectMembershipRole.Owner, cmd.CreatedByUserId);

        _db.Projects.Add(project);
        _db.Suites.Add(suite);
        _db.ProjectMemberships.Add(ownerMembership);
        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new ProjectCreated(project.WorkspaceId, project.Id, project.Name, project.Code), ct);

        return new ProjectDto(project.Id, project.Name, project.Code, project.Description,
            project.IsArchived, project.CreatedAtUtc, project.ArchivedAtUtc);
    }
}

// ─── Update ───────────────────────────────────────────────────────────────
public sealed record UpdateProjectCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId,
    string Name,
    string? Description) : IRequest<Result<ProjectDto>>;

public sealed class UpdateProjectValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpdateProjectHandler : IRequestHandler<UpdateProjectCommand, Result<ProjectDto>>
{
    private readonly ProjectsDbContext _db;
    public UpdateProjectHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<ProjectDto>> Handle(UpdateProjectCommand cmd, CancellationToken ct)
    {
        if (!await ProjectAccessPolicy.CanManageProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                cmd.CurrentUserRole,
                cmd.ProjectId,
                ct))
            return Result.Failure<ProjectDto>(ProjectAccessErrors.InsufficientRole);

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == cmd.ProjectId && p.WorkspaceId == cmd.WorkspaceId, ct);
        if (project is null)
            return Result.Failure<ProjectDto>(ProjectErrors.ProjectNotFound);
        if (project.IsArchived)
            return Result.Failure<ProjectDto>(ProjectErrors.ProjectArchived);

        project.Update(cmd.Name, cmd.Description);
        await _db.SaveChangesAsync(ct);

        return new ProjectDto(project.Id, project.Name, project.Code, project.Description,
            project.IsArchived, project.CreatedAtUtc, project.ArchivedAtUtc);
    }
}

// ─── Archive / Restore ───────────────────────────────────────────────────
public sealed record ArchiveProjectCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId) : IRequest<Result>;

public sealed record RestoreProjectCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId) : IRequest<Result>;

public sealed class ArchiveProjectHandler :
    IRequestHandler<ArchiveProjectCommand, Result>,
    IRequestHandler<RestoreProjectCommand, Result>
{
    private readonly ProjectsDbContext _db;
    private readonly IPublishEndpoint _publisher;

    public ArchiveProjectHandler(ProjectsDbContext db, IPublishEndpoint publisher)
    {
        _db = db; _publisher = publisher;
    }

    public async Task<Result> Handle(ArchiveProjectCommand cmd, CancellationToken ct)
    {
        if (!await ProjectAccessPolicy.CanManageProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                cmd.CurrentUserRole,
                cmd.ProjectId,
                ct))
            return Result.Failure(ProjectAccessErrors.InsufficientRole);

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == cmd.ProjectId && p.WorkspaceId == cmd.WorkspaceId, ct);
        if (project is null) return Result.Failure(ProjectErrors.ProjectNotFound);

        project.Archive();
        await _db.SaveChangesAsync(ct);
        await _publisher.Publish(new ProjectArchived(project.WorkspaceId, project.Id), ct);
        return Result.Success();
    }

    public async Task<Result> Handle(RestoreProjectCommand cmd, CancellationToken ct)
    {
        if (!await ProjectAccessPolicy.CanManageProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                cmd.CurrentUserRole,
                cmd.ProjectId,
                ct))
            return Result.Failure(ProjectAccessErrors.InsufficientRole);

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == cmd.ProjectId && p.WorkspaceId == cmd.WorkspaceId, ct);
        if (project is null) return Result.Failure(ProjectErrors.ProjectNotFound);

        project.Restore();
        await _db.SaveChangesAsync(ct);
        await _publisher.Publish(new ProjectRestored(project.WorkspaceId, project.Id, project.Code), ct);
        return Result.Success();
    }
}

// ─── Get / List ──────────────────────────────────────────────────────────
public sealed record GetProjectQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId) : IRequest<Result<ProjectDto>>;

public sealed class GetProjectHandler : IRequestHandler<GetProjectQuery, Result<ProjectDto>>
{
    private readonly ProjectsDbContext _db;
    public GetProjectHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<ProjectDto>> Handle(GetProjectQuery q, CancellationToken ct)
    {
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                q.ProjectId,
                ct,
                q.CurrentUserRole))
            return Result.Failure<ProjectDto>(ProjectAccessErrors.Denied);

        var p = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.ProjectId && x.WorkspaceId == q.WorkspaceId, ct);
        if (p is null) return Result.Failure<ProjectDto>(ProjectErrors.ProjectNotFound);
        return new ProjectDto(p.Id, p.Name, p.Code, p.Description, p.IsArchived, p.CreatedAtUtc, p.ArchivedAtUtc);
    }
}

public sealed record ListProjectsQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    string? Search,
    bool IncludeArchived,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<ProjectDto>>>;

public sealed class ListProjectsHandler : IRequestHandler<ListProjectsQuery, Result<PagedResult<ProjectDto>>>
{
    private readonly ProjectsDbContext _db;
    public ListProjectsHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<PagedResult<ProjectDto>>> Handle(ListProjectsQuery q, CancellationToken ct)
    {
        var page = new PageRequest(q.Page, q.PageSize);

        IQueryable<Project> query = _db.Projects.AsNoTracking()
            .Where(p => p.WorkspaceId == q.WorkspaceId);

        var visibility = await ProjectAccessPolicy.GetVisibilityAsync(
            _db.Database.GetDbConnection(),
            q.WorkspaceId,
            q.CurrentUserId,
            q.CurrentUserAccessScope,
            q.CurrentUserRole,
            ct);
        var allowed = visibility.AllowedArray();
        var denied = visibility.DeniedArray();
        if (allowed is not null)
            query = query.Where(p => allowed.Contains(p.Id));
        if (denied.Length > 0)
            query = query.Where(p => !denied.Contains(p.Id));

        if (!q.IncludeArchived)
            query = query.Where(p => p.ArchivedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, $"%{s}%") ||
                EF.Functions.ILike(p.Code, $"%{s}%"));
        }

        var total = await query.LongCountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip(page.Skip).Take(page.Take)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Code, p.Description, p.ArchivedAtUtc != null,
                p.CreatedAtUtc, p.ArchivedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<ProjectDto>(items, page.Page, page.Take, total);
    }
}
