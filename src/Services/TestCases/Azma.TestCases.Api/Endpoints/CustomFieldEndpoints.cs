using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.TestCases.Api.Application.Common;
using Azma.TestCases.Api.Application.CustomFields;
using Azma.TestCases.Api.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Azma.TestCases.Api.Endpoints;

public static class CustomFieldEndpoints
{
    public static IEndpointRouteBuilder MapCustomFieldEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/custom-fields").WithTags("CustomFields").RequireAuthorization();

        group.MapGet("/", async (
            ITenantContext tenant, ISender sender,
            [FromQuery] Guid projectId, [FromQuery] bool includeArchived = false,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new ListCustomFieldsQuery(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope, tenant.Role ?? string.Empty,
                projectId,
                includeArchived), ct);
            return result.ToHttp<IReadOnlyList<CustomFieldDefinitionDto>>();
        });

        group.MapPost("/", async (
            [FromBody] CreateCustomFieldRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateCustomFieldCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope, tenant.Role ?? string.Empty,
                req.ProjectId,
                req.Key, req.Label, req.Description, req.Type,
                req.Options, req.Required, req.Position), ct);
            return result.ToHttp<CustomFieldDefinitionDto>(StatusCodes.Status201Created);
        });

        group.MapPut("/{id:guid}", async (
            Guid id, [FromBody] UpdateCustomFieldRequest req,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateCustomFieldCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope, tenant.Role ?? string.Empty,
                id,
                req.Label, req.Description, req.Options, req.Required, req.Position), ct);
            return result.ToHttp<CustomFieldDefinitionDto>();
        });

        group.MapDelete("/{id:guid}", async (
            Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteCustomFieldCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        return app;
    }
}

public sealed record CreateCustomFieldRequest(
    Guid ProjectId, string Key, string Label, string? Description,
    CustomFieldType Type, List<string>? Options, bool Required, int Position);

public sealed record UpdateCustomFieldRequest(
    string Label, string? Description, List<string>? Options, bool Required, int Position);
