using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.Projects.Api.Application.Common;
using Azma.Projects.Api.Application.Sections;
using Azma.Projects.Api.Application.Suites;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Azma.Projects.Api.Endpoints;

public static class SuiteEndpoints
{
    public static IEndpointRouteBuilder MapSuiteEndpoints(this IEndpointRouteBuilder app)
    {
        var byProject = app.MapGroup("/api/projects/{projectId:guid}/suites")
            .WithTags("Suites").RequireAuthorization();

        byProject.MapGet("/", async (
            Guid projectId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ListSuitesQuery(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope,
                tenant.Role ?? string.Empty,
                projectId), ct);
            return result.ToHttp<IReadOnlyList<SuiteDto>>();
        });

        byProject.MapPost("/", async (
            Guid projectId, [FromBody] CreateSuiteRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateSuiteCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    projectId, req.Name, req.Description), ct);
            return result.ToHttp<SuiteDto>(StatusCodes.Status201Created);
        });

        var single = app.MapGroup("/api/suites/{suiteId:guid}")
            .WithTags("Suites").RequireAuthorization();

        single.MapGet("/tree", async (
            Guid suiteId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetSuiteTreeQuery(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope,
                tenant.Role ?? string.Empty,
                suiteId), ct);
            return result.ToHttp<SuiteTreeDto>();
        });

        single.MapPut("/", async (
            Guid suiteId, [FromBody] UpdateSuiteRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new UpdateSuiteCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    suiteId, req.Name, req.Description), ct);
            return result.ToHttp<SuiteDto>();
        });

        single.MapDelete("/", async (
            Guid suiteId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteSuiteCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope,
                tenant.Role ?? string.Empty,
                suiteId), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        single.MapPost("/reorder", async (
            Guid suiteId, [FromBody] ReorderRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new ReorderSuiteCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    suiteId, req.NewPosition), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        // ── Sections ────────────────────────────────────────────────────
        single.MapPost("/sections", async (
            Guid suiteId, [FromBody] CreateSectionRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateSectionCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    suiteId, req.ParentId, req.Name, req.Description), ct);
            return result.ToHttp<SectionNodeDto>(StatusCodes.Status201Created);
        });

        var section = app.MapGroup("/api/sections/{sectionId:guid}")
            .WithTags("Sections").RequireAuthorization();

        section.MapPut("/", async (
            Guid sectionId, [FromBody] UpdateSectionRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new UpdateSectionCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    sectionId, req.Name, req.Description), ct);
            return result.ToHttp<SectionNodeDto>();
        });

        section.MapDelete("/", async (
            Guid sectionId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteSectionCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope,
                tenant.Role ?? string.Empty,
                sectionId), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        section.MapPost("/move", async (
            Guid sectionId, [FromBody] MoveSectionRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new MoveSectionCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.AccessScope,
                    tenant.Role ?? string.Empty,
                    sectionId, req.NewParentId, req.NewPosition), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        return app;
    }
}

public sealed record CreateSuiteRequest(string Name, string? Description);
public sealed record UpdateSuiteRequest(string Name, string? Description);
public sealed record ReorderRequest(int NewPosition);
public sealed record CreateSectionRequest(Guid? ParentId, string Name, string? Description);
public sealed record UpdateSectionRequest(string Name, string? Description);
public sealed record MoveSectionRequest(Guid? NewParentId, int NewPosition);
