using Azma.Auth.ProjectAccess;
using Azma.Common.Results;
using Azma.TestCases.Api.Application.Common;
using Azma.TestCases.Api.Domain;
using Azma.TestCases.Api.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.TestCases.Api.Application.CustomFields;

internal static class CustomFieldMapping
{
    public static CustomFieldDefinitionDto Map(CustomFieldDefinition d) => new(
        d.Id, d.ProjectId, d.Key, d.Label, d.Description, d.Type,
        d.Options?.AsReadOnly(), d.Required, d.Position, d.IsArchived,
        d.CreatedAtUtc, d.UpdatedAtUtc);
}

// ─── List ──────────────────────────────────────────────────────────
public sealed record ListCustomFieldsQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId,
    bool IncludeArchived)
    : IRequest<Result<IReadOnlyList<CustomFieldDefinitionDto>>>;

public sealed class ListCustomFieldsHandler
    : IRequestHandler<ListCustomFieldsQuery, Result<IReadOnlyList<CustomFieldDefinitionDto>>>
{
    private readonly TestCasesDbContext _db;
    public ListCustomFieldsHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> Handle(
        ListCustomFieldsQuery q, CancellationToken ct)
    {
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                q.ProjectId,
                ct,
                q.CurrentUserRole))
            return Result.Failure<IReadOnlyList<CustomFieldDefinitionDto>>(TestCaseErrors.ProjectAccessDenied);

        var query = _db.CustomFieldDefinitions.AsNoTracking()
            .Where(x => x.WorkspaceId == q.WorkspaceId && x.ProjectId == q.ProjectId);
        if (!q.IncludeArchived) query = query.Where(x => !x.IsArchived);
        var defs = await query.OrderBy(x => x.Position).ThenBy(x => x.CreatedAtUtc).ToListAsync(ct);
        IReadOnlyList<CustomFieldDefinitionDto> dtos = defs.Select(CustomFieldMapping.Map).ToList();
        return Result.Success(dtos);
    }
}

// ─── Create ────────────────────────────────────────────────────────
public sealed record CreateCustomFieldCommand(
    Guid WorkspaceId, Guid CurrentUserId, string CurrentUserAccessScope,
    string CurrentUserRole, Guid ProjectId,
    string Key, string Label, string? Description,
    CustomFieldType Type, List<string>? Options,
    bool Required, int Position) : IRequest<Result<CustomFieldDefinitionDto>>;

public sealed class CreateCustomFieldValidator : AbstractValidator<CreateCustomFieldCommand>
{
    public CreateCustomFieldValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Label).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class CreateCustomFieldHandler
    : IRequestHandler<CreateCustomFieldCommand, Result<CustomFieldDefinitionDto>>
{
    private readonly TestCasesDbContext _db;
    public CreateCustomFieldHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result<CustomFieldDefinitionDto>> Handle(CreateCustomFieldCommand cmd, CancellationToken ct)
    {
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                cmd.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Manage))
            return Result.Failure<CustomFieldDefinitionDto>(TestCaseErrors.ProjectAccessDenied);

        var key = CustomFieldDefinition.NormalizeKey(cmd.Key);
        var exists = await _db.CustomFieldDefinitions.AnyAsync(
            x => x.WorkspaceId == cmd.WorkspaceId && x.ProjectId == cmd.ProjectId && x.Key == key, ct);
        if (exists) return Result.Failure<CustomFieldDefinitionDto>(TestCaseErrors.CustomFieldKeyExists);

        var def = CustomFieldDefinition.Create(
            cmd.WorkspaceId, cmd.ProjectId, key, cmd.Label, cmd.Description,
            cmd.Type, cmd.Options, cmd.Required, cmd.Position);

        _db.CustomFieldDefinitions.Add(def);
        await _db.SaveChangesAsync(ct);
        return CustomFieldMapping.Map(def);
    }
}

// ─── Update ────────────────────────────────────────────────────────
public sealed record UpdateCustomFieldCommand(
    Guid WorkspaceId, Guid CurrentUserId, string CurrentUserAccessScope,
    string CurrentUserRole, Guid Id,
    string Label, string? Description,
    List<string>? Options, bool Required, int Position) : IRequest<Result<CustomFieldDefinitionDto>>;

public sealed class UpdateCustomFieldValidator : AbstractValidator<UpdateCustomFieldCommand>
{
    public UpdateCustomFieldValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class UpdateCustomFieldHandler
    : IRequestHandler<UpdateCustomFieldCommand, Result<CustomFieldDefinitionDto>>
{
    private readonly TestCasesDbContext _db;
    public UpdateCustomFieldHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result<CustomFieldDefinitionDto>> Handle(UpdateCustomFieldCommand cmd, CancellationToken ct)
    {
        var def = await _db.CustomFieldDefinitions.FirstOrDefaultAsync(
            x => x.Id == cmd.Id && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (def is null) return Result.Failure<CustomFieldDefinitionDto>(TestCaseErrors.CustomFieldNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                def.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Manage))
            return Result.Failure<CustomFieldDefinitionDto>(TestCaseErrors.ProjectAccessDenied);

        def.Update(cmd.Label, cmd.Description, cmd.Options, cmd.Required, cmd.Position);
        await _db.SaveChangesAsync(ct);
        return CustomFieldMapping.Map(def);
    }
}

// ─── Delete (soft archive) ─────────────────────────────────────────
public sealed record DeleteCustomFieldCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid Id) : IRequest<Result>;

public sealed class DeleteCustomFieldHandler : IRequestHandler<DeleteCustomFieldCommand, Result>
{
    private readonly TestCasesDbContext _db;
    public DeleteCustomFieldHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result> Handle(DeleteCustomFieldCommand cmd, CancellationToken ct)
    {
        var def = await _db.CustomFieldDefinitions.FirstOrDefaultAsync(
            x => x.Id == cmd.Id && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (def is null) return Result.Failure(TestCaseErrors.CustomFieldNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                def.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Manage))
            return Result.Failure(TestCaseErrors.ProjectAccessDenied);
        def.Archive();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
