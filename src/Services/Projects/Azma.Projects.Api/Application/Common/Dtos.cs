namespace Azma.Projects.Api.Application.Common;

public sealed record ProjectDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc);

public sealed record MilestoneDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    DateTime? DueOnUtc,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc);

public sealed record SuiteDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    int Position,
    DateTime CreatedAtUtc);

public sealed record SectionNodeDto(
    Guid Id,
    Guid SuiteId,
    Guid? ParentId,
    string Name,
    string? Description,
    int Position,
    IReadOnlyList<SectionNodeDto> Children);

public sealed record SuiteTreeDto(
    Guid Id,
    string Name,
    int Position,
    IReadOnlyList<SectionNodeDto> Sections);
