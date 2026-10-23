using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.Projects.Api.Application.Common;
using Azma.Projects.Api.Application.Milestones;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Azma.Projects.Api.Endpoints;

public static class MilestoneEndpoints
{
    public static IEndpointRouteBuilder MapMilestoneEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}/milestones")
            .WithTags("Milestones").RequireAuthorization();

        group.MapGet("/", async (
            Guid projectId, ITenantContext tenant, ISender sender,
            [FromQuery] bool includeClosed = false, CancellationToken ct = default) =>
        {
            var result = await sender.Send(
                new ListMilestonesQuery(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    projectId,
                    includeClosed), ct);
            return result.ToHttp<IReadOnlyList<MilestoneDto>>();
        });

        group.MapPost("/", async (
            Guid projectId, [FromBody] CreateMilestoneRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateMilestoneCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    projectId, req.Name, req.Description, req.DueOnUtc), ct);
            return result.ToHttp<MilestoneDto>(StatusCodes.Status201Created);
        });

        var single = app.MapGroup("/api/milestones/{milestoneId:guid}")
            .WithTags("Milestones").RequireAuthorization();

        single.MapPut("/", async (
            Guid milestoneId, [FromBody] UpdateMilestoneRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new UpdateMilestoneCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    milestoneId,
                    req.Name, req.Description, req.DueOnUtc), ct);
            return result.ToHttp<MilestoneDto>();
        });

        single.MapPost("/close", async (
            Guid milestoneId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CloseMilestoneCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope,
                tenant.Role ?? string.Empty,
                milestoneId), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        single.MapPost("/reopen", async (
            Guid milestoneId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ReopenMilestoneCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope,
                tenant.Role ?? string.Empty,
                milestoneId), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        return app;
    }
}

public sealed record CreateMilestoneRequest(string Name, string? Description, DateTime? DueOnUtc);
public sealed record UpdateMilestoneRequest(string Name, string? Description, DateTime? DueOnUtc);
