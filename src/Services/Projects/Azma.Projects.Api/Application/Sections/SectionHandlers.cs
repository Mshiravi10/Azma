using Azma.Auth.ProjectAccess;
using Azma.Common.Results;
using Azma.Projects.Api.Application.Common;
using Azma.Projects.Api.Domain;
using Azma.Projects.Api.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Projects.Api.Application.Sections;

// ─── Create Section ──────────────────────────────────────────────────────
public sealed record CreateSectionCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid SuiteId,
    Guid? ParentId,
    string Name,
    string? Description) : IRequest<Result<SectionNodeDto>>;

public sealed class CreateSectionValidator : AbstractValidator<CreateSectionCommand>
{
    public CreateSectionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(1).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class CreateSectionHandler : IRequestHandler<CreateSectionCommand, Result<SectionNodeDto>>
{
    private readonly ProjectsDbContext _db;
    public CreateSectionHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<SectionNodeDto>> Handle(CreateSectionCommand cmd, CancellationToken ct)
    {
        var suite = await _db.Suites.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == cmd.SuiteId && s.WorkspaceId == cmd.WorkspaceId, ct);
        if (suite is null) return Result.Failure<SectionNodeDto>(ProjectErrors.SuiteNotFound);

        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            suite.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure<SectionNodeDto>(denied);

        if (cmd.ParentId is { } parentId)
        {
            var parent = await _db.Sections.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == parentId && x.WorkspaceId == cmd.WorkspaceId, ct);
            if (parent is null) return Result.Failure<SectionNodeDto>(ProjectErrors.SectionNotFound);
            if (parent.SuiteId != cmd.SuiteId)
                return Result.Failure<SectionNodeDto>(ProjectErrors.SectionParentMismatch);
        }

        var maxPos = await _db.Sections
            .Where(x => x.SuiteId == cmd.SuiteId && x.ParentId == cmd.ParentId)
            .Select(x => (int?)x.Position).MaxAsync(ct) ?? -1;

        var section = Section.Create(
            cmd.WorkspaceId, suite.ProjectId, cmd.SuiteId, cmd.ParentId,
            cmd.Name, cmd.Description, maxPos + 1);

        _db.Sections.Add(section);
        await _db.SaveChangesAsync(ct);

        return new SectionNodeDto(section.Id, section.SuiteId, section.ParentId,
            section.Name, section.Description, section.Position, Array.Empty<SectionNodeDto>());
    }
}

// ─── Update Section ──────────────────────────────────────────────────────
public sealed record UpdateSectionCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid SectionId,
    string Name,
    string? Description) : IRequest<Result<SectionNodeDto>>;

public sealed class UpdateSectionValidator : AbstractValidator<UpdateSectionCommand>
{
    public UpdateSectionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(1).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpdateSectionHandler : IRequestHandler<UpdateSectionCommand, Result<SectionNodeDto>>
{
    private readonly ProjectsDbContext _db;
    public UpdateSectionHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<SectionNodeDto>> Handle(UpdateSectionCommand cmd, CancellationToken ct)
    {
        var s = await _db.Sections
            .FirstOrDefaultAsync(x => x.Id == cmd.SectionId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (s is null) return Result.Failure<SectionNodeDto>(ProjectErrors.SectionNotFound);

        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            s.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure<SectionNodeDto>(denied);

        s.Update(cmd.Name, cmd.Description);
        await _db.SaveChangesAsync(ct);
        return new SectionNodeDto(s.Id, s.SuiteId, s.ParentId, s.Name, s.Description, s.Position, Array.Empty<SectionNodeDto>());
    }
}

// ─── Delete Section ──────────────────────────────────────────────────────
public sealed record DeleteSectionCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid SectionId) : IRequest<Result>;

public sealed class DeleteSectionHandler : IRequestHandler<DeleteSectionCommand, Result>
{
    private readonly ProjectsDbContext _db;
    public DeleteSectionHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result> Handle(DeleteSectionCommand cmd, CancellationToken ct)
    {
        var s = await _db.Sections
            .FirstOrDefaultAsync(x => x.Id == cmd.SectionId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (s is null) return Result.Failure(ProjectErrors.SectionNotFound);

        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            s.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure(denied);

        _db.Sections.Remove(s);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Move Section (re-parent + re-order) ─────────────────────────────────
public sealed record MoveSectionCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid SectionId,
    Guid? NewParentId,
    int NewPosition) : IRequest<Result>;

public sealed class MoveSectionValidator : AbstractValidator<MoveSectionCommand>
{
    public MoveSectionValidator()
    {
        RuleFor(x => x.NewPosition).GreaterThanOrEqualTo(0);
    }
}

public sealed class MoveSectionHandler : IRequestHandler<MoveSectionCommand, Result>
{
    private readonly ProjectsDbContext _db;
    public MoveSectionHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result> Handle(MoveSectionCommand cmd, CancellationToken ct)
    {
        var section = await _db.Sections
            .FirstOrDefaultAsync(x => x.Id == cmd.SectionId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (section is null) return Result.Failure(ProjectErrors.SectionNotFound);

        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            section.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure(denied);

        if (cmd.NewParentId is { } newParentId)
        {
            if (newParentId == section.Id)
                return Result.Failure(ProjectErrors.SectionCycle);

            var parent = await _db.Sections.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == newParentId && x.WorkspaceId == cmd.WorkspaceId, ct);
            if (parent is null) return Result.Failure(ProjectErrors.SectionNotFound);
            if (parent.SuiteId != section.SuiteId)
                return Result.Failure(ProjectErrors.SectionParentMismatch);

            // Cycle check: walk up the new parent's chain — must not reach section.Id
            var cursorId = parent.ParentId;
            while (cursorId is { } cid)
            {
                if (cid == section.Id) return Result.Failure(ProjectErrors.SectionCycle);
                cursorId = await _db.Sections.AsNoTracking()
                    .Where(x => x.Id == cid).Select(x => x.ParentId).FirstOrDefaultAsync(ct);
            }
        }

        // Re-pack siblings in the destination so positions become contiguous.
        var siblings = await _db.Sections
            .Where(x => x.SuiteId == section.SuiteId
                     && x.ParentId == cmd.NewParentId
                     && x.Id != section.Id)
            .OrderBy(x => x.Position)
            .ToListAsync(ct);

        var insertAt = Math.Clamp(cmd.NewPosition, 0, siblings.Count);
        siblings.Insert(insertAt, section);

        section.MoveTo(cmd.NewParentId, insertAt);
        for (var i = 0; i < siblings.Count; i++)
        {
            if (siblings[i].Position != i)
                siblings[i].SetPosition(i);
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Reorder Suite (set absolute position among project's suites) ────────
public sealed record ReorderSuiteCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid SuiteId,
    int NewPosition) : IRequest<Result>;

public sealed class ReorderSuiteHandler : IRequestHandler<ReorderSuiteCommand, Result>
{
    private readonly ProjectsDbContext _db;
    public ReorderSuiteHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result> Handle(ReorderSuiteCommand cmd, CancellationToken ct)
    {
        var suite = await _db.Suites
            .FirstOrDefaultAsync(x => x.Id == cmd.SuiteId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (suite is null) return Result.Failure(ProjectErrors.SuiteNotFound);

        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            suite.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure(denied);

        var siblings = await _db.Suites
            .Where(x => x.ProjectId == suite.ProjectId && x.Id != suite.Id)
            .OrderBy(x => x.Position)
            .ToListAsync(ct);

        var insertAt = Math.Clamp(cmd.NewPosition, 0, siblings.Count);
        siblings.Insert(insertAt, suite);
        for (var i = 0; i < siblings.Count; i++)
            if (siblings[i].Position != i) siblings[i].SetPosition(i);

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
