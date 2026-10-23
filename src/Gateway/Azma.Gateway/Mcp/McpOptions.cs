namespace Azma.Gateway.Mcp;

public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    public bool Enabled { get; set; } = true;

    public string InternalApiBaseUrl { get; set; } = "http://localhost:8080/api";

    public bool RequireLoopback { get; set; } = true;

    public bool AllowPrivateNetwork { get; set; }

    public int HttpTimeoutSeconds { get; set; } = 10;

    public int TokenValidationCacheSeconds { get; set; } = 60;

    public bool AuditLoggingEnabled { get; set; } = true;

    public bool AuditLogArguments { get; set; } = true;

    public int AuditLogMaxStringLength { get; set; } = 500;

    public Uri GetInternalApiBaseUri()
    {
        var value = string.IsNullOrWhiteSpace(InternalApiBaseUrl)
            ? "http://localhost:8080/api"
            : InternalApiBaseUrl.Trim();

        if (!value.EndsWith("/", StringComparison.Ordinal))
            value += "/";

        return new Uri(value, UriKind.Absolute);
    }
}
