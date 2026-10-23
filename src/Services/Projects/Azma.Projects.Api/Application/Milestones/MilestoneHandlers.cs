using Azma.Auth.ProjectAccess;
using Azma.Common.Results;
using Azma.Projects.Api.Application.Common;
using Azma.Projects.Api.Domain;
using Azma.Projects.Api.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using static Azma.Projects.Api.Application.Milestones.MilestoneMapping;

namespace Azma.Projects.Api.Application.Milestones;

// ─── Create ──────────────────────────────────────────────────────────────
public sealed record CreateMilestoneCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId,
    string Name,
    string? Description,
    DateTime? DueOnUtc) : IRequest<Result<MilestoneDto>>;

public sealed class CreateMilestoneValidator : AbstractValidator<CreateMilestoneCommand>
{
    public CreateMilestoneValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class CreateMilestoneHandler : IRequestHandler<CreateMilestoneCommand, Result<MilestoneDto>>
{
    private readonly ProjectsDbContext _db;
    public CreateMilestoneHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<MilestoneDto>> Handle(CreateMilestoneCommand cmd, CancellationToken ct)
    {
        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            cmd.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure<MilestoneDto>(denied);

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == cmd.ProjectId && p.WorkspaceId == cmd.WorkspaceId, ct);
        if (project is null) return Result.Failure<MilestoneDto>(ProjectErrors.ProjectNotFound);
        if (project.IsArchived) return Result.Failure<MilestoneDto>(ProjectErrors.ProjectArchived);

        var m = Milestone.Create(cmd.WorkspaceId, cmd.ProjectId, cmd.Name, cmd.Description, cmd.DueOnUtc);
        _db.Milestones.Add(m);
        await _db.SaveChangesAsync(ct);

        return Map(m);
    }
}

// ─── Update ──────────────────────────────────────────────────────────────
public sealed record UpdateMilestoneCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid MilestoneId,
    string Name,
    string? Description,
    DateTime? DueOnUtc) : IRequest<Result<MilestoneDto>>;

public sealed class UpdateMilestoneValidator : AbstractValidator<UpdateMilestoneCommand>
{
    public UpdateMilestoneValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpdateMilestoneHandler : IRequestHandler<UpdateMilestoneCommand, Result<MilestoneDto>>
{
    private readonly ProjectsDbContext _db;
    public UpdateMilestoneHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<MilestoneDto>> Handle(UpdateMilestoneCommand cmd, CancellationToken ct)
    {
        var m = await _db.Milestones
            .FirstOrDefaultAsync(x => x.Id == cmd.MilestoneId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (m is null) return Result.Failure<MilestoneDto>(ProjectErrors.MilestoneNotFound);

        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            m.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure<MilestoneDto>(denied);

        if (m.Status == MilestoneStatus.Closed) return Result.Failure<MilestoneDto>(ProjectErrors.MilestoneClosed);

        m.Update(cmd.Name, cmd.Description, cmd.DueOnUtc);
        await _db.SaveChangesAsync(ct);
        return Map(m);
    }
}

// ─── Close / Reopen ──────────────────────────────────────────────────────
public sealed record CloseMilestoneCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid MilestoneId) : IRequest<Result>;
public sealed record ReopenMilestoneCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid MilestoneId) : IRequest<Result>;

public sealed class CloseReopenMilestoneHandler :
    IRequestHandler<CloseMilestoneCommand, Result>,
    IRequestHandler<ReopenMilestoneCommand, Result>
{
    private readonly ProjectsDbContext _db;
    public CloseReopenMilestoneHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result> Handle(CloseMilestoneCommand cmd, CancellationToken ct)
    {
        var m = await _db.Milestones
            .FirstOrDefaultAsync(x => x.Id == cmd.MilestoneId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (m is null) return Result.Failure(ProjectErrors.MilestoneNotFound);
        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            m.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure(denied);
        m.Close();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(ReopenMilestoneCommand cmd, CancellationToken ct)
    {
        var m = await _db.Milestones
            .FirstOrDefaultAsync(x => x.Id == cmd.MilestoneId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (m is null) return Result.Failure(ProjectErrors.MilestoneNotFound);
        var denied = await ProjectGuard.EnsureAsync(
            _db, cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.CurrentUserRole,
            m.ProjectId, ProjectCapability.Contribute, ct);
        if (denied is not null) return Result.Failure(denied);
        m.Reopen();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── List ───────────────────────────────────────────────────────────────
public sealed record ListMilestonesQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId,
    bool IncludeClosed) : IRequest<Result<IReadOnlyList<MilestoneDto>>>;

public sealed class ListMilestonesHandler : IRequestHandler<ListMilestonesQuery, Result<IReadOnlyList<MilestoneDto>>>
{
    private readonly ProjectsDbContext _db;
    public ListMilestonesHandler(ProjectsDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<MilestoneDto>>> Handle(ListMilestonesQuery q, CancellationToken ct)
    {
        var denied = await ProjectGuard.EnsureAsync(
            _db, q.WorkspaceId, q.CurrentUserId, q.CurrentUserAccessScope, q.CurrentUserRole,
            q.ProjectId, ProjectCapability.View, ct);
        if (denied is not null) return Result.Failure<IReadOnlyList<MilestoneDto>>(denied);

        IQueryable<Milestone> query = _db.Milestones.AsNoTracking()
            .Where(m => m.WorkspaceId == q.WorkspaceId && m.ProjectId == q.ProjectId);

        if (!q.IncludeClosed)
            query = query.Where(m => m.Status == MilestoneStatus.Open);

        var items = await query
            .OrderBy(m => m.Status)
            .ThenBy(m => m.DueOnUtc ?? DateTime.MaxValue)
            .ThenBy(m => m.Name)
            .Select(m => new MilestoneDto(m.Id, m.ProjectId, m.Name, m.Description, m.DueOnUtc,
                m.Status.ToString(), m.CreatedAtUtc, m.ClosedAtUtc))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<MilestoneDto>>(items);
    }
}

internal static class MilestoneMapping
{
    public static MilestoneDto Map(Milestone m) =>
        new(m.Id, m.ProjectId, m.Name, m.Description, m.DueOnUtc,
            m.Status.ToString(), m.CreatedAtUtc, m.ClosedAtUtc);
}
