using System.Net;
using Microsoft.Extensions.Options;

namespace Azma.Gateway.Mcp;

public static class McpEndpointGuards
{
    public static IApplicationBuilder UseAzmaMcpAccessGuard(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            var options = context.RequestServices.GetRequiredService<IOptionsMonitor<McpOptions>>().CurrentValue;
            var decision = McpNetworkAccessPolicy.Evaluate(
                options,
                context.Connection.RemoteIpAddress,
                context.Request.Host.Host,
                context.Request.Headers.Origin.ToString());

            if (!decision.Allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = decision.Error
                });
                return;
            }

            await next(context);
        });

        return app;
    }

    public static IApplicationBuilder UseAzmaMcpLocalOnly(this WebApplication app) =>
        app.UseAzmaMcpAccessGuard();
}

public static class McpNetworkAccessPolicy
{
    public static McpNetworkAccessDecision Evaluate(
        McpOptions options,
        IPAddress? remoteIpAddress,
        string? host,
        string? origin)
    {
        if (options.RequireLoopback)
        {
            if (!IsLoopback(remoteIpAddress))
            {
                return McpNetworkAccessDecision.Denied("Azma MCP is available only from localhost in development.");
            }

            if (!IsAllowedLocalHost(host))
            {
                return McpNetworkAccessDecision.Denied("Azma MCP rejected a non-local Host header.");
            }

            if (!IsAllowedLocalOrigin(origin))
            {
                return McpNetworkAccessDecision.Denied("Azma MCP rejected a non-local Origin header.");
            }

            return McpNetworkAccessDecision.Allow();
        }

        if (options.AllowPrivateNetwork)
        {
            if (!IsLoopback(remoteIpAddress) && !IsPrivateNetworkAddress(remoteIpAddress))
            {
                return McpNetworkAccessDecision.Denied("Azma MCP is available only from localhost or private LAN addresses in development.");
            }

            if (!IsAllowedPrivateNetworkHost(host))
            {
                return McpNetworkAccessDecision.Denied("Azma MCP rejected a non-private Host header.");
            }

            if (!IsAllowedPrivateNetworkOrigin(origin))
            {
                return McpNetworkAccessDecision.Denied("Azma MCP rejected a non-private Origin header.");
            }
        }

        return McpNetworkAccessDecision.Allow();
    }

    private static bool IsLoopback(IPAddress? address)
    {
        if (address is null)
            return true;

        if (IPAddress.IsLoopback(address))
            return true;

        return address.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(address.MapToIPv4());
    }

    private static bool IsAllowedLocalHost(string? host) =>
        string.IsNullOrWhiteSpace(host) ||
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedLocalOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return true;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedPrivateNetworkHost(string? host)
    {
        if (IsAllowedLocalHost(host))
            return true;

        return IPAddress.TryParse(host?.Trim('[', ']'), out var address) &&
               IsPrivateNetworkAddress(address);
    }

    private static bool IsAllowedPrivateNetworkOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return true;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        return IsAllowedPrivateNetworkHost(uri.Host);
    }

    private static bool IsPrivateNetworkAddress(IPAddress? address)
    {
        if (address is null)
            return true;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                   bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31 ||
                   bytes[0] == 192 && bytes[1] == 168 ||
                   bytes[0] == 169 && bytes[1] == 254;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return (bytes[0] & 0xfe) == 0xfc ||
                   bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80;
        }

        return false;
    }
}

public sealed record McpNetworkAccessDecision(bool Allowed, string? Error)
{
    public static McpNetworkAccessDecision Allow() => new(true, null);

    public static McpNetworkAccessDecision Denied(string error) => new(false, error);
}
