using Azma.Auth;
using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.Identity.Api.Application.Auth;
using Azma.Identity.Api.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Azma.Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async ([FromBody] RegisterRequest req, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new RegisterCommand(req.Email, req.Password, req.DisplayName, req.WorkspaceName), ct);
            return result.ToHttp<AuthTokensDto>(StatusCodes.Status201Created);
        }).AllowAnonymous();

        group.MapPost("/login", async ([FromBody] LoginRequest req, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new LoginCommand(req.Email, req.Password, req.WorkspaceId), ct);
            return result.ToHttp();
        }).AllowAnonymous();

        group.MapPost("/refresh", async ([FromBody] RefreshRequest req, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RefreshCommand(req.RefreshToken), ct);
            return result.ToHttp();
        }).AllowAnonymous();

        group.MapGet("/me", async (ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new MeQuery(tenant.RequireUserId(), tenant.RequireWorkspaceId()), ct);
            return result.ToHttp();
        }).RequireAuthorization();

        group.MapPost("/switch-workspace/{workspaceId:guid}",
            async (Guid workspaceId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SwitchWorkspaceCommand(tenant.RequireUserId(), workspaceId), ct);
            return result.ToHttp();
        }).RequireAuthorization();

        group.MapPost("/change-password",
            async ([FromBody] ChangePasswordRequest req, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new ChangePasswordCommand(tenant.RequireUserId(), req.CurrentPassword, req.NewPassword), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        }).RequireAuthorization();

        group.MapGet("/mcp-tokens", async (ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new ListMcpTokensQuery(tenant.RequireUserId(), tenant.RequireWorkspaceId()),
                ct);
            return result.ToHttp();
        }).RequireAuthorization();

        group.MapPost("/mcp-tokens",
            async ([FromBody] CreateMcpTokenRequest req, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateMcpTokenCommand(
                    tenant.RequireUserId(),
                    tenant.RequireWorkspaceId(),
                    req.Name,
                    req.CurrentPassword,
                    req.ExpiresInDays),
                ct);
            return result.ToHttp(StatusCodes.Status201Created);
        }).RequireAuthorization();

        group.MapDelete("/mcp-tokens/{id:guid}",
            async (Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new RevokeMcpTokenCommand(tenant.RequireUserId(), tenant.RequireWorkspaceId(), id),
                ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        }).RequireAuthorization();

        group.MapPost("/mcp-tokens/validate", async (HttpContext http, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ValidateMcpTokenCommand(ReadBearerToken(http)), ct);
            return result.ToHttp();
        }).AllowAnonymous();

        return app;
    }

    private static string ReadBearerToken(HttpContext http)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : string.Empty;
    }
}

public sealed record RegisterRequest(string Email, string Password, string DisplayName, string WorkspaceName);
public sealed record LoginRequest(string Email, string Password, Guid? WorkspaceId);
public sealed record RefreshRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record CreateMcpTokenRequest(string Name, string CurrentPassword, int? ExpiresInDays);
