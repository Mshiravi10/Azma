using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.Projects.Api.Application.Members;
using Azma.Projects.Api.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Azma.Projects.Api.Endpoints;

public static class ProjectMemberEndpoints
{
    public static IEndpointRouteBuilder MapProjectMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}/members")
            .WithTags("ProjectMembers")
            .RequireAuthorization();

        group.MapGet("/", async (
            Guid projectId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new ListProjectMembersQuery(
                    tenant.RequireWorkspaceId(),
                    projectId,
                    tenant.RequireUserId(),
                    tenant.Role ?? string.Empty,
                    tenant.AccessScope), ct);
            return result.ToHttp();
        });

        group.MapGet("/candidates", async (
            Guid projectId, ITenantContext tenant, ISender sender,
            [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(
                new ListProjectMemberCandidatesQuery(
                    tenant.RequireWorkspaceId(),
                    projectId,
                    tenant.RequireUserId(),
                    tenant.Role ?? string.Empty,
                    search,
                    page,
                    pageSize), ct);
            return result.ToHttp();
        });

        app.MapGet("/api/projects/{projectId:guid}/member-candidates", async (
            Guid projectId, ITenantContext tenant, ISender sender,
            [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(
                new ListProjectMemberCandidatesQuery(
                    tenant.RequireWorkspaceId(),
                    projectId,
                    tenant.RequireUserId(),
                    tenant.Role ?? string.Empty,
                    search,
                    page,
                    pageSize), ct);
            return result.ToHttp();
        }).WithTags("ProjectMembers").RequireAuthorization();

        // User-centric: get all project role overrides for a specific user
        app.MapGet("/api/members/{userId:guid}/project-roles", async (
            Guid userId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new ListUserProjectRolesQuery(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.Role ?? string.Empty,
                    userId), ct);
            return result.ToHttp();
        }).WithTags("ProjectMembers").RequireAuthorization();

        group.MapPost("/", async (
            Guid projectId, [FromBody] AddProjectMemberRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Role)
                || !Enum.TryParse<ProjectMembershipRole>(req.Role, ignoreCase: true, out var role)
                || role == ProjectMembershipRole.Owner)
            {
                return Results.BadRequest(new { code = "projects.invalid_role", message = "نقش پروژه نامعتبر است. یکی از Admin، Tester، Viewer یا Excluded را بفرستید." });
            }
            var result = await sender.Send(
                new AddProjectMemberCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.Role ?? string.Empty,
                    projectId,
                    req.UserId,
                    role), ct);
            return result.ToHttp(StatusCodes.Status201Created);
        });

        group.MapPut("/{userId:guid}", async (
            Guid projectId, Guid userId, [FromBody] SetProjectMemberRoleRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            if (!Enum.TryParse<ProjectMembershipRole>(req.Role, ignoreCase: true, out var role)
                || role == ProjectMembershipRole.Owner)
            {
                return Results.BadRequest(new { code = "projects.invalid_role", message = "نقش پروژه نامعتبر است. یکی از Admin، Tester، Viewer یا Excluded را بفرستید." });
            }
            var result = await sender.Send(
                new SetProjectMemberRoleCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.Role ?? string.Empty,
                    projectId,
                    userId,
                    role), ct);
            return result.ToHttp();
        });

        group.MapDelete("/{userId:guid}", async (
            Guid projectId, Guid userId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new ResetProjectMemberCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.Role ?? string.Empty,
                    projectId,
                    userId), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        group.MapPost("/{userId:guid}/transfer-ownership", async (
            Guid projectId, Guid userId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new TransferProjectOwnershipCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    projectId,
                    userId), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        return app;
    }
}

public sealed record AddProjectMemberRequest(Guid UserId, string? Role);
public sealed record SetProjectMemberRoleRequest(string Role);
