using Azma.Auth;

namespace Azma.Gateway.Mcp;

public static class McpTokenAuthentication
{
    public static IApplicationBuilder UseAzmaMcpTokenAuthentication(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            if (!await TryAuthenticateAsync(context))
                return;

            await next(context);
        });

        return app;
    }

    public static async Task<bool> TryAuthenticateAsync(HttpContext context)
    {
        var rawToken = ReadBearerToken(context.Request.Headers.Authorization.ToString());
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            await WriteUnauthorizedAsync(context, "Azma MCP requires an Authorization bearer token.");
            return false;
        }

        var validator = context.RequestServices.GetRequiredService<IMcpTokenValidator>();
        var principal = await validator.ValidateAsync(rawToken, context.RequestAborted);
        if (principal is null)
        {
            await WriteUnauthorizedAsync(context, "Azma MCP token is invalid, expired, or revoked.");
            return false;
        }

        var jwt = context.RequestServices.GetRequiredService<IJwtTokenGenerator>();
        var accessToken = jwt.Generate(
            principal.UserId,
            principal.WorkspaceId,
            principal.Email,
            principal.DisplayName,
            principal.Role,
            principal.AccessScope);

        context.Items[McpHttpContextItems.ValidatedPrincipal] = principal;
        context.Items[McpHttpContextItems.InternalAccessToken] = accessToken.Token;

        return true;
    }

    public static string ReadBearerToken(string? authorizationHeader)
    {
        const string prefix = "Bearer ";

        return !string.IsNullOrWhiteSpace(authorizationHeader) &&
               authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[prefix.Length..].Trim()
            : string.Empty;
    }

    private static Task WriteUnauthorizedAsync(HttpContext context, string error)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new { error });
    }
}
