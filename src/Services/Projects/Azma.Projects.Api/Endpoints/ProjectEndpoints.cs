using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.Common.Pagination;
using Azma.Projects.Api.Application.Common;
using Azma.Projects.Api.Application.Projects;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Azma.Projects.Api.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects").RequireAuthorization();

        group.MapGet("/", async (
            ITenantContext tenant, ISender sender,
            [FromQuery] string? search, [FromQuery] bool includeArchived = false,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        {
            var result = await sender.Send(
                new ListProjectsQuery(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    search,
                    includeArchived,
                    page,
                    pageSize), ct);
            return result.ToHttp<PagedResult<ProjectDto>>();
        });

        group.MapGet("/{projectId:guid}", async (
            Guid projectId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetProjectQuery(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope,
                tenant.Role ?? string.Empty,
                projectId), ct);
            return result.ToHttp<ProjectDto>();
        });

        group.MapPost("/", async (
            [FromBody] CreateProjectRequest req, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateProjectCommand(
                    tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    req.Name, req.Code, req.Description), ct);
            return result.ToHttp<ProjectDto>(StatusCodes.Status201Created);
        });

        group.MapPut("/{projectId:guid}", async (
            Guid projectId, [FromBody] UpdateProjectRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new UpdateProjectCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    projectId,
                    req.Name,
                    req.Description), ct);
            return result.ToHttp<ProjectDto>();
        });

        group.MapPost("/{projectId:guid}/archive", async (
            Guid projectId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ArchiveProjectCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope,
                tenant.Role ?? string.Empty,
                projectId), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        group.MapPost("/{projectId:guid}/restore", async (
            Guid projectId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RestoreProjectCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope,
                tenant.Role ?? string.Empty,
                projectId), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        return app;
    }
}

public sealed record CreateProjectRequest(string Name, string Code, string? Description);
public sealed record UpdateProjectRequest(string Name, string? Description);
