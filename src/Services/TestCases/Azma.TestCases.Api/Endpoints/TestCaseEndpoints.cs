using Azma.Auth.Tenant;
using Azma.Automation;
using Azma.Common.Http;
using Azma.Common.Pagination;
using Azma.TestCases.Api.Application.Common;
using Azma.TestCases.Api.Application.TestCases;
using Azma.TestCases.Api.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Azma.TestCases.Api.Endpoints;

public static class TestCaseEndpoints
{
    public static IEndpointRouteBuilder MapTestCaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/testcases").WithTags("TestCases").RequireAuthorization();

        group.MapGet("/", async (
            ITenantContext tenant, ISender sender,
            [FromQuery] Guid? projectId, [FromQuery] Guid? suiteId, [FromQuery] Guid? sectionId,
            [FromQuery] string? search, [FromQuery] string? tag,
            [FromQuery] TestCaseStatus? status, [FromQuery] TestCasePriority? priority,
            [FromQuery] bool? hasAutomation, [FromQuery] AutomationEngine? automationEngine,
            [FromQuery] TestCaseExecutionState? executionState, [FromQuery] int staleDays = 14,
            [FromQuery] bool includeDeleted = false,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        {
            var result = await sender.Send(new ListTestCasesQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                projectId, suiteId, sectionId, search, tag,
                status, priority, hasAutomation, automationEngine, executionState, staleDays, includeDeleted, page, pageSize), ct);
            return result.ToHttp<PagedResult<TestCaseSummaryDto>>();
        });

        group.MapGet("/{id:guid}", async (
            Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetTestCaseQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<TestCaseDto>();
        });

        group.MapPost("/", async (
            [FromBody] CreateTestCaseRequest req, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateTestCaseCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(),
                tenant.AccessScope, tenant.Role ?? string.Empty,
                req.ProjectId, req.SuiteId, req.SectionId,
                req.Title, req.Preconditions,
                req.Type ?? TestCaseType.Functional,
                req.Priority ?? TestCasePriority.Medium,
                req.Status,
                req.Tags,
                req.Steps?.Select(s => new CreateTestCaseStepInput(s.Action, s.ExpectedResult)).ToList(),
                req.AutomationScenarioPath,
                req.CustomFields), ct);
            return result.ToHttp<TestCaseDto>(StatusCodes.Status201Created);
        });

        group.MapPut("/{id:guid}", async (
            Guid id, [FromBody] UpdateTestCaseRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateTestCaseCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id,
                req.SuiteId, req.SectionId, req.Title, req.Preconditions,
                req.Type, req.Priority, req.Status, req.ChangeReason,
                req.AutomationScenarioPath, req.CustomFields), ct);
            return result.ToHttp<TestCaseDto>();
        });

        group.MapDelete("/{id:guid}", async (
            Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteTestCaseCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        group.MapPut("/{id:guid}/steps", async (
            Guid id, [FromBody] StepInput[] steps,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ReplaceStepsCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id,
                steps.Select(s => new CreateTestCaseStepInput(s.Action, s.ExpectedResult)).ToList()), ct);
            return result.ToHttp<TestCaseDto>();
        });

        group.MapPut("/{id:guid}/tags", async (
            Guid id, [FromBody] string[] tags,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetTagsCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id, tags), ct);
            return result.ToHttp<IReadOnlyList<string>>();
        });

        group.MapGet("/{id:guid}/versions", async (
            Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ListVersionsQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<IReadOnlyList<TestCaseVersionDto>>();
        });

        group.MapPost("/{id:guid}/automation/recordings/start", async (
            Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new StartAutomationRecordingCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<AutomationRecordingSessionDto>(StatusCodes.Status201Created);
        });

        group.MapPost("/{id:guid}/automation/recordings/{sessionId:guid}/events", async (
            Guid id, Guid sessionId, [FromBody] AppendAutomationRecordingEventsRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AppendAutomationRecordingEventsCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                id, sessionId, req.SessionToken, req.Events ?? Array.Empty<JsonElement>()), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        app.MapPost("/api/testcases/automation/recordings/{sessionId:guid}/events", async (
            Guid sessionId, [FromBody] AppendAutomationRecordingEventsRequest req,
            ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AppendAutomationRecordingEventsByTokenCommand(
                sessionId, req.SessionToken, req.Events ?? Array.Empty<JsonElement>()), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        })
        .WithTags("TestCases")
        .AllowAnonymous();

        app.MapPost("/api/testcases/automation/recordings/{sessionId:guid}/finish", async (
            Guid sessionId, [FromBody] FinishAutomationRecordingRequest req,
            ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new FinishAutomationRecordingByTokenCommand(
                sessionId, req.SessionToken, req.Definition), ct);
            return result.ToHttp<TestCaseAutomationDto>();
        })
        .WithTags("TestCases")
        .AllowAnonymous();

        group.MapPost("/{id:guid}/automation/recordings/{sessionId:guid}/finish", async (
            Guid id, Guid sessionId, [FromBody] FinishAutomationRecordingRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new FinishAutomationRecordingCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                id, sessionId, req.SessionToken, req.Definition), ct);
            return result.ToHttp<TestCaseAutomationDto>();
        });

        group.MapPost("/{id:guid}/automation/validate", async (
            Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ValidateAutomationCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<TestCaseAutomationDto>();
        });

        group.MapPut("/{id:guid}/automation/prerequisite", async (
            Guid id, [FromBody] SetAutomationPrerequisiteRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetAutomationPrerequisiteCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                id, req.PrerequisiteTestCaseId), ct);
            return result.ToHttp<TestCaseDto>();
        });

        group.MapPost("/{id:guid}/automation/dry-run", async (
            Guid id, [FromBody] DryRunAutomationRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DryRunAutomationCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                id, req.Gateway, req.Frontend), ct);
            return result.ToHttp<AutomationRunResponse>();
        });

        group.MapPost("/{id:guid}/automation/publish", async (
            Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new PublishAutomationCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<TestCaseAutomationDto>();
        });

        return app;
    }
}

public sealed record StepInput(string Action, string? ExpectedResult);

public sealed record CreateTestCaseRequest(
    Guid ProjectId,
    Guid? SuiteId,
    Guid? SectionId,
    string Title,
    string? Preconditions,
    TestCaseType? Type,
    TestCasePriority? Priority,
    TestCaseStatus? Status,
    string[]? Tags,
    StepInput[]? Steps,
    string? AutomationScenarioPath,
    Dictionary<string, JsonElement>? CustomFields);

public sealed record UpdateTestCaseRequest(
    Guid? SuiteId,
    Guid? SectionId,
    string Title,
    string? Preconditions,
    TestCaseType Type,
    TestCasePriority Priority,
    TestCaseStatus Status,
    string? ChangeReason,
    string? AutomationScenarioPath,
    Dictionary<string, JsonElement>? CustomFields);

public sealed record AppendAutomationRecordingEventsRequest(
    string SessionToken,
    JsonElement[]? Events);

public sealed record FinishAutomationRecordingRequest(
    string SessionToken,
    RecordedScenarioDefinition? Definition);

public sealed record DryRunAutomationRequest(
    string? Gateway,
    string? Frontend);

public sealed record SetAutomationPrerequisiteRequest(Guid? PrerequisiteTestCaseId);
