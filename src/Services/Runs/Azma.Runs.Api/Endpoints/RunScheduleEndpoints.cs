using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.Common.Pagination;
using Azma.Runs.Api.Application.Common;
using Azma.Runs.Api.Application.Runs;
using Azma.Runs.Api.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Azma.Runs.Api.Endpoints;

public static class RunScheduleEndpoints
{
    public static IEndpointRouteBuilder MapRunScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/run-schedules").WithTags("Run Schedules").RequireAuthorization();

        group.MapGet("/", async (
            ITenantContext tenant,
            ISender sender,
            [FromQuery] Guid? projectId,
            [FromQuery] bool includeInactive = true,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new ListRunSchedulesQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                projectId, includeInactive, page, pageSize), ct);
            return result.ToHttp<PagedResult<RunScheduleDto>>();
        });

        group.MapGet("/{id:guid}", async (Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRunScheduleQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<RunScheduleDetailDto>();
        });

        group.MapPost("/", async (
            [FromBody] CreateRunScheduleRequest req,
            ITenantContext tenant,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateRunScheduleCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                req.ProjectId, req.Name, req.Description, req.Environment,
                req.RecurrenceType, req.TimeOfDayMinutes, req.WeekDaysMask,
                req.TimeZoneId, req.StartAtUtc, req.EndAtUtc,
                req.TestCaseIds ?? Array.Empty<Guid>(),
                req.Loops, req.Threads, req.Rampup, req.Skippable, req.Gateway, req.Frontend), ct);
            return result.ToHttp<RunScheduleDetailDto>(StatusCodes.Status201Created);
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateRunScheduleRequest req,
            ITenantContext tenant,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateRunScheduleCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                id, req.Name, req.Description, req.Environment,
                req.RecurrenceType, req.TimeOfDayMinutes, req.WeekDaysMask,
                req.TimeZoneId, req.StartAtUtc, req.EndAtUtc, req.TestCaseIds,
                req.Loops, req.Threads, req.Rampup, req.Skippable, req.Gateway, req.Frontend), ct);
            return result.ToHttp<RunScheduleDetailDto>();
        });

        group.MapPost("/{id:guid}/pause", async (Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new PauseRunScheduleCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<RunScheduleDto>();
        });

        group.MapPost("/{id:guid}/resume", async (Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ResumeRunScheduleCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<RunScheduleDto>();
        });

        group.MapPost("/{id:guid}/run-now", async (Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RunScheduleNowCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<RunScheduleDto>();
        });

        group.MapDelete("/{id:guid}", async (Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteRunScheduleCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        group.MapGet("/{id:guid}/executions", async (
            Guid id,
            ITenantContext tenant,
            ISender sender,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new ListRunScheduleExecutionsQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                id, page, pageSize), ct);
            return result.ToHttp<PagedResult<RunScheduleExecutionDto>>();
        });

        return app;
    }
}

public sealed record CreateRunScheduleRequest(
    Guid ProjectId,
    string Name,
    string? Description,
    string? Environment,
    ScheduleRecurrenceType RecurrenceType,
    int TimeOfDayMinutes,
    int WeekDaysMask,
    string? TimeZoneId,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    Guid[]? TestCaseIds,
    int? Loops,
    int? Threads,
    int? Rampup,
    bool? Skippable,
    string? Gateway,
    string? Frontend);

public sealed record UpdateRunScheduleRequest(
    string? Name,
    string? Description,
    string? Environment,
    ScheduleRecurrenceType? RecurrenceType,
    int? TimeOfDayMinutes,
    int? WeekDaysMask,
    string? TimeZoneId,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    Guid[]? TestCaseIds,
    int? Loops,
    int? Threads,
    int? Rampup,
    bool? Skippable,
    string? Gateway,
    string? Frontend);
