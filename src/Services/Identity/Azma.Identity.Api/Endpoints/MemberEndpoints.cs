using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.Identity.Api.Application.Members;
using Azma.Identity.Api.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Azma.Identity.Api.Endpoints;

public static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/members").WithTags("Members").RequireAuthorization();

        group.MapGet("/", async (ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ListMembersQuery(tenant.RequireWorkspaceId()), ct);
            return result.ToHttp();
        });

        group.MapGet("/candidates", async (
            ITenantContext tenant, ISender sender,
            [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(
                new ListMemberCandidatesQuery(
                    tenant.RequireWorkspaceId(),
                    tenant.Role ?? string.Empty,
                    search,
                    page,
                    pageSize), ct);
            return result.ToHttp();
        });

        group.MapPost("/", async (
            [FromBody] AddExistingMemberRequest req, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var role = string.IsNullOrWhiteSpace(req.Role)
                ? WorkspaceRole.Tester
                : Enum.TryParse<WorkspaceRole>(req.Role, ignoreCase: true, out var r)
                    ? r
                    : WorkspaceRole.Tester;
            var result = await sender.Send(
                new AddExistingMemberCommand(
                    tenant.RequireWorkspaceId(),
                    tenant.RequireUserId(),
                    tenant.Role ?? string.Empty,
                    req.UserId,
                    role), ct);
            return result.ToHttp(StatusCodes.Status201Created);
        });

        group.MapGet("/invitations", async (ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ListInvitationsQuery(tenant.RequireWorkspaceId()), ct);
            return result.ToHttp();
        });

        group.MapPost("/invitations",
            async ([FromBody] InviteMemberRequest req, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var role = Enum.TryParse<WorkspaceRole>(req.Role, ignoreCase: true, out var r) ? r : WorkspaceRole.Tester;
            var cmd = new InviteMemberCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.Role ?? string.Empty,
                req.Email,
                role);
            var result = await sender.Send(cmd, ct);
            return result.ToHttp(StatusCodes.Status201Created);
        });

        // Public endpoint — invitee may not be authenticated yet.
        app.MapPost("/api/invitations/accept",
            async ([FromBody] AcceptInvitationRequest req, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new AcceptInvitationCommand(req.Token, req.Password, req.DisplayName), ct);
            return result.ToHttp();
        }).AllowAnonymous().WithTags("Members");

        // Admin creates user directly (with temporary password)
        var admin = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization();
        admin.MapPost("/users",
            async ([FromBody] AdminCreateUserRequest req, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var role = Enum.TryParse<WorkspaceRole>(req.Role, ignoreCase: true, out var r) ? r : WorkspaceRole.Tester;
            var cmd = new AdminCreateUserCommand(
                tenant.RequireWorkspaceId(),
                tenant.Role ?? string.Empty,
                req.Email,
                req.DisplayName,
                role);
            var result = await sender.Send(cmd, ct);
            return result.ToHttp(StatusCodes.Status201Created);
        });

        // Change role
        group.MapPatch("/{userId:guid}",
            async (Guid userId, [FromBody] ChangeMemberRoleRequest req, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var role = Enum.TryParse<WorkspaceRole>(req.Role, ignoreCase: true, out var r) ? r : WorkspaceRole.Tester;
            var cmd = new ChangeMemberRoleCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.Role ?? string.Empty,
                userId,
                role);
            var result = await sender.Send(cmd, ct);
            return result.ToHttp();
        });

        // Remove from workspace
        group.MapDelete("/{userId:guid}",
            async (Guid userId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var cmd = new RemoveMemberCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.Role ?? string.Empty,
                userId);
            var result = await sender.Send(cmd, ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        // Transfer ownership
        group.MapPost("/{userId:guid}/transfer-ownership",
            async (Guid userId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var cmd = new TransferOwnershipCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.Role ?? string.Empty,
                userId);
            var result = await sender.Send(cmd, ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        return app;
    }
}

public sealed record InviteMemberRequest(string Email, string Role);
public sealed record AcceptInvitationRequest(string Token, string? Password, string? DisplayName);
public sealed record AdminCreateUserRequest(string Email, string DisplayName, string Role);
public sealed record AddExistingMemberRequest(Guid UserId, string? Role);
public sealed record ChangeMemberRoleRequest(string Role);
