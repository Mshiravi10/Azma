using Azma.Auth.ProjectAccess;
using Azma.Common.Results;
using Azma.Projects.Api.Application.Common;
using Azma.Projects.Api.Domain;
using Azma.Projects.Api.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Projects.Api.Application.Suites;

// ─── Create Suite ────────────────────────────────────────────────────────
public sealed record CreateSuiteCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId,
    string Name,
    string? Description) : IRequest<Result<SuiteDto>>;

public sealed class CreateSuiteValidator : AbstractValidator<CreateSuiteCommand>
{
    public CreateSuiteValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class CreateSuiteHandler : IRequestHandler<CreateSuiteCommand, Result<SuiteDto>>
{
    private readonly ProjectsDbContext _db;
    public CreateSuiteHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<SuiteDto>> Handle(CreateSuiteCommand cmd, CancellationToken ct)
    {
        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            cmd.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure<SuiteDto>(denied);

        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == cmd.ProjectId && p.WorkspaceId == cmd.WorkspaceId, ct);
        if (project is null) return Result.Failure<SuiteDto>(ProjectErrors.ProjectNotFound);
        if (project.IsArchived) return Result.Failure<SuiteDto>(ProjectErrors.ProjectArchived);

        var maxPos = await _db.Suites
            .Where(s => s.ProjectId == cmd.ProjectId)
            .Select(s => (int?)s.Position).MaxAsync(ct) ?? -1;

        var suite = Suite.Create(cmd.WorkspaceId, cmd.ProjectId, cmd.Name, cmd.Description, maxPos + 1);
        _db.Suites.Add(suite);
        await _db.SaveChangesAsync(ct);

        return new SuiteDto(suite.Id, suite.ProjectId, suite.Name, suite.Description, suite.Position, suite.CreatedAtUtc);
    }
}

// ─── Update Suite ────────────────────────────────────────────────────────
public sealed record UpdateSuiteCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid SuiteId,
    string Name,
    string? Description) : IRequest<Result<SuiteDto>>;

public sealed class UpdateSuiteValidator : AbstractValidator<UpdateSuiteCommand>
{
    public UpdateSuiteValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpdateSuiteHandler : IRequestHandler<UpdateSuiteCommand, Result<SuiteDto>>
{
    private readonly ProjectsDbContext _db;
    public UpdateSuiteHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<SuiteDto>> Handle(UpdateSuiteCommand cmd, CancellationToken ct)
    {
        var s = await _db.Suites
            .FirstOrDefaultAsync(x => x.Id == cmd.SuiteId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (s is null) return Result.Failure<SuiteDto>(ProjectErrors.SuiteNotFound);

        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            s.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure<SuiteDto>(denied);

        s.Update(cmd.Name, cmd.Description);
        await _db.SaveChangesAsync(ct);
        return new SuiteDto(s.Id, s.ProjectId, s.Name, s.Description, s.Position, s.CreatedAtUtc);
    }
}

// ─── Delete Suite ────────────────────────────────────────────────────────
public sealed record DeleteSuiteCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid SuiteId) : IRequest<Result>;

public sealed class DeleteSuiteHandler : IRequestHandler<DeleteSuiteCommand, Result>
{
    private readonly ProjectsDbContext _db;
    public DeleteSuiteHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result> Handle(DeleteSuiteCommand cmd, CancellationToken ct)
    {
        var s = await _db.Suites
            .FirstOrDefaultAsync(x => x.Id == cmd.SuiteId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (s is null) return Result.Failure(ProjectErrors.SuiteNotFound);

        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            s.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure(denied);

        _db.Suites.Remove(s);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── List Suites (flat) ──────────────────────────────────────────────────
public sealed record ListSuitesQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId)
    : IRequest<Result<IReadOnlyList<SuiteDto>>>;

public sealed class ListSuitesHandler : IRequestHandler<ListSuitesQuery, Result<IReadOnlyList<SuiteDto>>>
{
    private readonly ProjectsDbContext _db;
    public ListSuitesHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<SuiteDto>>> Handle(ListSuitesQuery q, CancellationToken ct)
    {
        var denied = await ProjectGuard.EnsureAsync(
            _db, q.WorkspaceId, q.CurrentUserId, q.CurrentUserAccessScope, q.CurrentUserRole,
            q.ProjectId, ProjectCapability.View, ct);
        if (denied is not null) return Result.Failure<IReadOnlyList<SuiteDto>>(denied);

        var items = await _db.Suites.AsNoTracking()
            .Where(s => s.WorkspaceId == q.WorkspaceId && s.ProjectId == q.ProjectId)
            .OrderBy(s => s.Position)
            .Select(s => new SuiteDto(s.Id, s.ProjectId, s.Name, s.Description, s.Position, s.CreatedAtUtc))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<SuiteDto>>(items);
    }
}

// ─── Get Suite Tree (suite + sections) ───────────────────────────────────
public sealed record GetSuiteTreeQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid SuiteId) : IRequest<Result<SuiteTreeDto>>;

public sealed class GetSuiteTreeHandler : IRequestHandler<GetSuiteTreeQuery, Result<SuiteTreeDto>>
{
    private readonly ProjectsDbContext _db;
    public GetSuiteTreeHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<SuiteTreeDto>> Handle(GetSuiteTreeQuery q, CancellationToken ct)
    {
        var suite = await _db.Suites.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == q.SuiteId && s.WorkspaceId == q.WorkspaceId, ct);
        if (suite is null) return Result.Failure<SuiteTreeDto>(ProjectErrors.SuiteNotFound);

        var denied = await ProjectGuard.EnsureAsync(
            _db, q.WorkspaceId, q.CurrentUserId, q.CurrentUserAccessScope, q.CurrentUserRole,
            suite.ProjectId, ProjectCapability.View, ct);
        if (denied is not null) return Result.Failure<SuiteTreeDto>(denied);

        var sections = await _db.Sections.AsNoTracking()
            .Where(x => x.SuiteId == q.SuiteId)
            .OrderBy(x => x.Position)
            .ToListAsync(ct);

        var byParent = sections
            .GroupBy(x => x.ParentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Position).ToList());

        IReadOnlyList<SectionNodeDto> Build(Guid? parentId)
        {
            if (!byParent.TryGetValue(parentId ?? Guid.Empty, out var children))
                return Array.Empty<SectionNodeDto>();
            return children.Select(c =>
                new SectionNodeDto(c.Id, c.SuiteId, c.ParentId, c.Name, c.Description, c.Position, Build(c.Id)))
                .ToList();
        }

        return new SuiteTreeDto(suite.Id, suite.Name, suite.Position, Build(null));
    }
}
