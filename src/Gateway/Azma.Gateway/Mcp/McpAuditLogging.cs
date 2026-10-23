using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Azma.Gateway.Mcp;

public static class McpAuditLogging
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "accessToken",
        "refreshToken",
        "token",
        "rawToken",
        "password",
        "currentPassword",
        "secret",
        "apiKey",
        "apikey",
        "key"
    };

    public static IApplicationBuilder UseAzmaMcpAuditLogging(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            var options = context.RequestServices.GetRequiredService<IOptionsMonitor<McpOptions>>().CurrentValue;
            if (!options.AuditLoggingEnabled)
            {
                await next(context);
                return;
            }

            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Azma.Gateway.McpAudit");

            var request = await DescribeRequestAsync(context.Request, options, context.RequestAborted);
            var originalBody = context.Response.Body;
            await using var capturedBody = new MemoryStream();
            context.Response.Body = capturedBody;

            var sw = Stopwatch.StartNew();
            Exception? exception = null;

            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                sw.Stop();

                capturedBody.Position = 0;
                var responseBody = await ReadBodyAsync(capturedBody, context.RequestAborted);
                capturedBody.Position = 0;
                await capturedBody.CopyToAsync(originalBody, context.RequestAborted);
                context.Response.Body = originalBody;

                var response = DescribeResponse(responseBody, context.Response.StatusCode);
                var principal = context.Items[McpHttpContextItems.ValidatedPrincipal] as McpTokenValidationResult;
                var level = SelectLogLevel(context.Response.StatusCode, exception, response);

                logger.Log(
                    level,
                    exception,
                    "MCP request {McpMethod}{McpToolSuffix} completed with {McpOutcome} ({StatusCode}) in {ElapsedMilliseconds} ms",
                    request.Method,
                    string.IsNullOrWhiteSpace(request.ToolName) ? string.Empty : $" tool={request.ToolName}",
                    response.Outcome,
                    context.Response.StatusCode,
                    sw.ElapsedMilliseconds);

                logger.LogInformation(
                    "MCP audit details: requestId={McpRequestId}, method={McpMethod}, tool={McpToolName}, user={McpUserId}, workspace={McpWorkspaceId}, remoteIp={RemoteIp}, summary={McpSummary}, sourceEndpoint={McpSourceEndpoint}, arguments={McpArguments}",
                    request.Id,
                    request.Method,
                    request.ToolName,
                    principal?.UserId,
                    principal?.WorkspaceId,
                    context.Connection.RemoteIpAddress?.ToString(),
                    response.Summary,
                    response.SourceEndpoint,
                    request.ArgumentsJson);
            }
        });

        return app;
    }

    public static async Task<McpAuditRequest> DescribeRequestAsync(
        HttpRequest request,
        McpOptions options,
        CancellationToken cancellationToken)
    {
        if (!HttpMethods.IsPost(request.Method) || request.Body is null)
            return new McpAuditRequest(null, request.Method, null, null);

        request.EnableBuffering();
        var body = await ReadBodyAsync(request.Body, cancellationToken);
        request.Body.Position = 0;

        return DescribeRequest(body, options);
    }

    public static McpAuditRequest DescribeRequest(string body, McpOptions options)
    {
        if (string.IsNullOrWhiteSpace(body))
            return new McpAuditRequest(null, "unknown", null, null);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var methods = root.EnumerateArray()
                    .Select(ReadMethod)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var tools = root.EnumerateArray()
                    .Select(ReadToolName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new McpAuditRequest(
                    null,
                    methods.Length == 0 ? "batch" : "batch:" + string.Join(",", methods),
                    tools.Length == 0 ? null : string.Join(",", tools),
                    null);
            }

            var method = ReadMethod(root) ?? "unknown";
            var id = root.TryGetProperty("id", out var idElement) ? idElement.ToString() : null;
            var toolName = ReadToolName(root);
            var argumentsJson = options.AuditLogArguments ? ReadToolArguments(root, options) : null;

            return new McpAuditRequest(id, method, toolName, argumentsJson);
        }
        catch (JsonException)
        {
            return new McpAuditRequest(null, "invalid-json", null, null);
        }
    }

    public static McpAuditResponse DescribeResponse(string body, int statusCode)
    {
        if (statusCode >= StatusCodes.Status400BadRequest && string.IsNullOrWhiteSpace(body))
            return new McpAuditResponse("http_error", null, null);

        if (string.IsNullOrWhiteSpace(body))
            return new McpAuditResponse(statusCode < 400 ? "ok" : "http_error", null, null);

        var data = ExtractFirstSseData(body) ?? body;

        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : error.ToString();
                return new McpAuditResponse("mcp_error", message, null);
            }

            if (!root.TryGetProperty("result", out var result))
                return new McpAuditResponse(statusCode < 400 ? "ok" : "http_error", null, null);

            var toolText = ReadFirstToolText(result);
            if (!string.IsNullOrWhiteSpace(toolText))
            {
                var toolResponse = TryDescribeToolText(toolText);
                if (toolResponse is not null)
                    return toolResponse;
            }

            return new McpAuditResponse("ok", null, null);
        }
        catch (JsonException)
        {
            return new McpAuditResponse(statusCode < 400 ? "ok" : "http_error", null, null);
        }
    }

    private static string? ReadMethod(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("method", out var method) &&
        method.ValueKind == JsonValueKind.String
            ? method.GetString()
            : null;

    private static string? ReadToolName(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadMethod(root), "tools/call", StringComparison.OrdinalIgnoreCase) ||
            !root.TryGetProperty("params", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("name", out var name) ||
            name.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return name.GetString();
    }

    private static string? ReadToolArguments(JsonElement root, McpOptions options)
    {
        if (!string.Equals(ReadMethod(root), "tools/call", StringComparison.OrdinalIgnoreCase) ||
            !root.TryGetProperty("params", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("arguments", out var arguments) ||
            arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        return SanitizeJson(arguments, Math.Clamp(options.AuditLogMaxStringLength, 50, 4000));
    }

    private static string SanitizeJson(JsonElement element, int maxStringLength)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteSanitized(writer, element, null, maxStringLength);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteSanitized(
        Utf8JsonWriter writer,
        JsonElement element,
        string? propertyName,
        int maxStringLength)
    {
        if (propertyName is not null && IsSensitiveKey(propertyName))
        {
            writer.WriteStringValue("[redacted]");
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteSanitized(writer, property.Value, property.Name, maxStringLength);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                var count = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (++count > 50)
                    {
                        writer.WriteStringValue("[truncated]");
                        break;
                    }

                    WriteSanitized(writer, item, null, maxStringLength);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(Truncate(element.GetString(), maxStringLength));
                break;

            case JsonValueKind.Number:
                element.WriteTo(writer);
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                writer.WriteBooleanValue(element.GetBoolean());
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        if (SensitiveKeys.Contains(key))
            return true;

        return SensitiveKeys.Any(sensitive =>
            key.Contains(sensitive, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractFirstSseData(string body)
    {
        var builder = new StringBuilder();
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal))
                continue;

            if (builder.Length > 0)
                builder.Append('\n');

            builder.Append(trimmed["data:".Length..].TrimStart());
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static string? ReadFirstToolText(JsonElement result)
    {
        if (!result.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("type", out var type) &&
                string.Equals(type.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                item.TryGetProperty("text", out var text) &&
                text.ValueKind == JsonValueKind.String)
            {
                return text.GetString();
            }
        }

        return null;
    }

    private static McpAuditResponse? TryDescribeToolText(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (!root.TryGetProperty("ok", out var okElement) ||
                okElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return null;
            }

            var summary = root.TryGetProperty("summary", out var summaryElement) &&
                          summaryElement.ValueKind == JsonValueKind.String
                ? summaryElement.GetString()
                : null;

            var sourceEndpoint = root.TryGetProperty("sourceEndpoint", out var sourceEndpointElement) &&
                                 sourceEndpointElement.ValueKind == JsonValueKind.String
                ? sourceEndpointElement.GetString()
                : null;

            return new McpAuditResponse(okElement.GetBoolean() ? "ok" : "failed", summary, sourceEndpoint);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static LogLevel SelectLogLevel(int statusCode, Exception? exception, McpAuditResponse response)
    {
        if (exception is not null || statusCode >= StatusCodes.Status500InternalServerError)
            return LogLevel.Error;

        if (statusCode >= StatusCodes.Status400BadRequest ||
            string.Equals(response.Outcome, "failed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(response.Outcome, "mcp_error", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Warning;
        }

        return LogLevel.Information;
    }

    private static async Task<string> ReadBodyAsync(Stream body, CancellationToken cancellationToken)
    {
        body.Position = 0;
        using var reader = new StreamReader(body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength
            ? value
            : value[..maxLength] + "...";
}

public sealed record McpAuditRequest(
    string? Id,
    string Method,
    string? ToolName,
    string? ArgumentsJson);

public sealed record McpAuditResponse(
    string Outcome,
    string? Summary,
    string? SourceEndpoint);
