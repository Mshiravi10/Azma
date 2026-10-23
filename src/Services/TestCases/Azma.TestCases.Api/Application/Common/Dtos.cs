using Azma.TestCases.Api.Domain;
using Azma.Automation;
using System.Text.Json;

namespace Azma.TestCases.Api.Application.Common;

public sealed record TestStepDto(int Order, string Action, string? ExpectedResult);

public sealed record TestCaseDto(
    Guid Id,
    Guid ProjectId,
    Guid? SuiteId,
    Guid? SectionId,
    string Code,
    string Title,
    string? Preconditions,
    bool HasAutomation,
    AutomationEngine AutomationEngine,
    AutomationStatus AutomationStatus,
    int? AutomationDefinitionVersion,
    string? AutomationScenarioPath,
    string? AutomationRecordingJson,
    TestCaseType Type,
    TestCasePriority Priority,
    TestCaseStatus Status,
    int Version,
    IReadOnlyList<string> Tags,
    IReadOnlyList<TestStepDto> Steps,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? LastExecutedAtUtc,
    string? LastExecutionStatus,
    Guid? LastExecutionRunId,
    Guid? LastExecutionRunCaseId,
    Guid? LastExecutedByUserId,
    IReadOnlyDictionary<string, JsonElement> CustomFields,
    Guid? AutomationPrerequisiteTestCaseId);

public sealed record TestCaseSummaryDto(
    Guid Id,
    Guid ProjectId,
    Guid? SuiteId,
    Guid? SectionId,
    string Code,
    string Title,
    bool HasAutomation,
    AutomationEngine AutomationEngine,
    AutomationStatus AutomationStatus,
    int? AutomationDefinitionVersion,
    string? AutomationScenarioPath,
    TestCaseType Type,
    TestCasePriority Priority,
    TestCaseStatus Status,
    int Version,
    IReadOnlyList<string> Tags,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? LastExecutedAtUtc,
    string? LastExecutionStatus,
    Guid? LastExecutionRunId,
    Guid? LastExecutionRunCaseId,
    Guid? LastExecutedByUserId);

public sealed record TestCaseVersionDto(
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    string? ChangeReason);

public sealed record AutomationRecordingSessionDto(
    Guid SessionId,
    string SessionToken,
    DateTime ExpiresAtUtc);

public sealed record TestCaseAutomationDto(
    Guid TestCaseId,
    AutomationEngine AutomationEngine,
    AutomationStatus AutomationStatus,
    int? AutomationDefinitionVersion,
    string? AutomationRecordingJson,
    bool IsValid,
    IReadOnlyList<string> ValidationErrors);

public sealed record CustomFieldDefinitionDto(
    Guid Id,
    Guid ProjectId,
    string Key,
    string Label,
    string? Description,
    CustomFieldType Type,
    IReadOnlyList<string>? Options,
    bool Required,
    int Position,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
