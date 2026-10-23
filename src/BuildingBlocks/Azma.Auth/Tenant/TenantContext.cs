using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Azma.Auth.Tenant;

/// <summary>Provides current user/workspace context for the running request.</summary>
public interface ITenantContext
{
    Guid? UserId { get; }
    Guid? WorkspaceId { get; }
    string? Email { get; }
    string? Role { get; }
    string AccessScope { get; }
    bool IsAuthenticated { get; }

    Guid RequireUserId();
    Guid RequireWorkspaceId();
}

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(AzmaClaims.UserId), out var id) ? id : null;

    public Guid? WorkspaceId =>
        Guid.TryParse(User?.FindFirstValue(AzmaClaims.WorkspaceId), out var id) ? id : null;

    public string? Email => User?.FindFirstValue(AzmaClaims.Email);
    public string? Role => User?.FindFirstValue(AzmaClaims.Role);
    public string AccessScope => User?.FindFirstValue(AzmaClaims.AccessScope) ?? AzmaAccessScopes.Workspace;

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedAccessException("UserId claim missing.");

    public Guid RequireWorkspaceId() =>
        WorkspaceId ?? throw new UnauthorizedAccessException("WorkspaceId claim missing.");
}
