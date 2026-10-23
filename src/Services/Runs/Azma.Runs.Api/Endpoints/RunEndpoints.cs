using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.Common.Pagination;
using Azma.Runs.Api.Application.Common;
using Azma.Runs.Api.Application.Runs;
using Azma.Runs.Api.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Azma.Runs.Api.Endpoints;

public static class RunEndpoints
{
    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var runs = app.MapGroup("/api/runs").WithTags("Runs").RequireAuthorization();

        runs.MapGet("/", async (
            ITenantContext tenant, ISender sender,
            [FromQuery] Guid? projectId, [FromQuery] Guid? milestoneId,
            [FromQuery] RunStatus? status, [FromQuery] string? search,
            [FromQuery] bool includeDeleted = false,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        {
            var result = await sender.Send(new ListRunsQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                projectId, milestoneId, status, search,
                includeDeleted, page, pageSize), ct);
            return result.ToHttp<PagedResult<RunSummaryDto>>();
        });

        runs.MapGet("/{id:guid}", async (Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRunQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<RunDto>();
        });

        runs.MapPost("/", async ([FromBody] CreateRunRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateRunCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(),
                tenant.AccessScope, tenant.Role ?? string.Empty,
                req.ProjectId, req.MilestoneId, req.Name, req.Description,
                req.TestCaseIds ?? Array.Empty<Guid>(),
                req.Environment, req.DueDateUtc, req.AutoExecute ?? false), ct);
            return result.ToHttp<RunDto>(StatusCodes.Status201Created);
        });

        runs.MapPost("/{id:guid}/automatic", async (Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new QueueRunAutomationCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                id, "manual"), ct);
            return result.ToHttp<RunAutomationExecutionDto>(StatusCodes.Status202Accepted);
        });

        runs.MapGet("/{id:guid}/automation-executions", async (
            Guid id, ITenantContext tenant, ISender sender,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        {
            var result = await sender.Send(new ListRunAutomationExecutionsQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                id, page, pageSize), ct);
            return result.ToHttp<PagedResult<RunAutomationExecutionDto>>();
        });

        runs.MapPost("/{id:guid}/close", async (Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CloseRunCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<RunDto>();
        });

        runs.MapPost("/{id:guid}/reopen", async (Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ReopenRunCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<RunDto>();
        });

        runs.MapPost("/{id:guid}/duplicate", async (Guid id, [FromBody] DuplicateRunRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DuplicateRunCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id, req.Name), ct);
            return result.ToHttp<RunDto>(StatusCodes.Status201Created);
        });

        runs.MapDelete("/{id:guid}", async (Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteRunCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        runs.MapPatch("/{id:guid}", async (Guid id, [FromBody] UpdateRunRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateRunCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                id, req.Name, req.Description,
                req.Environment, req.DueDateUtc), ct);
            return result.ToHttp<RunDto>();
        });

        runs.MapGet("/{id:guid}/cases", async (
            Guid id, ITenantContext tenant, ISender sender,
            [FromQuery] ResultStatus? status, [FromQuery] Guid? assignedTo,
            [FromQuery] string? search,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default) =>
        {
            var result = await sender.Send(new ListRunCasesQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                id, status, assignedTo, search, page, pageSize), ct);
            return result.ToHttp<PagedResult<RunCaseDto>>();
        });

        runs.MapPost("/{id:guid}/cases", async (Guid id, [FromBody] AddCasesRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AddRunCasesCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id, req.TestCaseIds), ct);
            return result.ToHttp<RunDto>();
        });

        runs.MapDelete("/{id:guid}/cases/{caseId:guid}", async (Guid id, Guid caseId,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RemoveRunCaseCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id, caseId), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        runs.MapPost("/{id:guid}/comments", async (
            Guid id, [FromBody] CommentRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AddCommentCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(),
                tenant.AccessScope, tenant.Role ?? string.Empty,
                id, req.RunCaseId, req.Body), ct);
            return result.ToHttp<RunCommentDto>(StatusCodes.Status201Created);
        });

        runs.MapGet("/{id:guid}/comments", async (
            Guid id, ITenantContext tenant, ISender sender,
            [FromQuery] Guid? runCaseId, CancellationToken ct) =>
        {
            var result = await sender.Send(new ListCommentsQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id, runCaseId), ct);
            return result.ToHttp<IReadOnlyList<RunCommentDto>>();
        });

        // ─── Result endpoints (top-level for ergonomics) ─────────────────
        var results = app.MapGroup("/api/results").WithTags("Results").RequireAuthorization();

        results.MapPost("/", async ([FromBody] RecordResultRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RecordResultCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(),
                tenant.AccessScope, tenant.Role ?? string.Empty,
                req.RunCaseId, req.Status, req.Comment, req.ElapsedSeconds), ct);
            return result.ToHttp<RunResultDto>(StatusCodes.Status201Created);
        });

        results.MapPost("/{runCaseId:guid}/automatic", async (
            Guid runCaseId, [FromBody] ExecuteAutomaticRunRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ExecuteAutomaticRunCaseCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, runCaseId,
                req.Gateway, req.Frontend), ct);
            return result.ToHttp<AutomaticRunResultDto>(StatusCodes.Status201Created);
        });

        results.MapGet("/{runCaseId:guid}", async (
            Guid runCaseId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ListResultsQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, runCaseId), ct);
            return result.ToHttp<IReadOnlyList<RunResultDto>>();
        });

        results.MapPut("/{runCaseId:guid}/assignee", async (
            Guid runCaseId, [FromBody] AssignRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AssignRunCaseCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                runCaseId, req.AssignedToUserId), ct);
            return result.ToHttp<RunCaseDto>();
        });

        results.MapPost("/bulk", async ([FromBody] BulkRecordResultRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new BulkRecordResultCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(),
                tenant.AccessScope, tenant.Role ?? string.Empty,
                req.RunCaseIds, req.Status, req.Comment), ct);
            return result.ToHttp<BulkRecordResultResponse>(StatusCodes.Status201Created);
        });

        return app;
    }
}

public sealed record CreateRunRequest(
    Guid ProjectId,
    Guid? MilestoneId,
    string Name,
    string? Description,
    Guid[]? TestCaseIds,
    string? Environment,
    DateTime? DueDateUtc,
    bool? AutoExecute);

public sealed record CommentRequest(Guid? RunCaseId, string Body);

public sealed record RecordResultRequest(
    Guid RunCaseId,
    ResultStatus Status,
    string? Comment,
    int? ElapsedSeconds);

public sealed record ExecuteAutomaticRunRequest(
    string? Gateway,
    string? Frontend);

public sealed record AssignRequest(Guid? AssignedToUserId);

public sealed record UpdateRunRequest(string? Name, string? Description, string? Environment, DateTime? DueDateUtc);

public sealed record DuplicateRunRequest(string? Name);

public sealed record AddCasesRequest(Guid[] TestCaseIds);

public sealed record BulkRecordResultRequest(
    Guid[] RunCaseIds,
    ResultStatus Status,
    string? Comment);
