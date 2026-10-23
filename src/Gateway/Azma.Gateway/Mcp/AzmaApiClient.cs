using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Azma.Gateway.Mcp;

public sealed class AzmaApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAzmaTokenProvider _tokenProvider;
    private readonly IOptionsMonitor<McpOptions> _options;

    static AzmaApiClient()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public AzmaApiClient(
        IHttpClientFactory httpClientFactory,
        IAzmaTokenProvider tokenProvider,
        IOptionsMonitor<McpOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _options = options;
    }

    public Task<AzmaMcpResult<JsonElement?>> GetAsync(
        string endpoint,
        string successSummary,
        CancellationToken cancellationToken,
        params QueryParam[] query) =>
        SendAsync(HttpMethod.Get, endpoint, null, successSummary, cancellationToken, query);

    public Task<AzmaMcpResult<JsonElement?>> PostAsync(
        string endpoint,
        object? body,
        string successSummary,
        CancellationToken cancellationToken,
        params QueryParam[] query) =>
        SendAsync(HttpMethod.Post, endpoint, body, successSummary, cancellationToken, query);

    public Task<AzmaMcpResult<JsonElement?>> PutAsync(
        string endpoint,
        object? body,
        string successSummary,
        CancellationToken cancellationToken,
        params QueryParam[] query) =>
        SendAsync(HttpMethod.Put, endpoint, body, successSummary, cancellationToken, query);

    public Task<AzmaMcpResult<JsonElement?>> PatchAsync(
        string endpoint,
        object? body,
        string successSummary,
        CancellationToken cancellationToken,
        params QueryParam[] query) =>
        SendAsync(HttpMethod.Patch, endpoint, body, successSummary, cancellationToken, query);

    public Task<AzmaMcpResult<JsonElement?>> DeleteAsync(
        string endpoint,
        string successSummary,
        CancellationToken cancellationToken,
        params QueryParam[] query) =>
        SendAsync(HttpMethod.Delete, endpoint, null, successSummary, cancellationToken, query);

    private async Task<AzmaMcpResult<JsonElement?>> SendAsync(
        HttpMethod method,
        string endpoint,
        object? body,
        string successSummary,
        CancellationToken cancellationToken,
        params QueryParam[] query)
    {
        var sourceEndpoint = BuildSourceEndpoint(endpoint, query);
        var response = await SendOnceSafelyAsync(method, endpoint, body, sourceEndpoint, cancellationToken, query);
        if (response.Failure is not null)
            return response.Failure;

        var message = response.Message!;
        if (message.StatusCode == HttpStatusCode.Unauthorized)
        {
            message.Dispose();
            _tokenProvider.Invalidate();
            response = await SendOnceSafelyAsync(method, endpoint, body, sourceEndpoint, cancellationToken, query);
            if (response.Failure is not null)
                return response.Failure;

            message = response.Message!;
        }

        using var _ = message;

        if (!message.IsSuccessStatusCode)
            return await ToFailureAsync(message, sourceEndpoint, cancellationToken);

        if (message.StatusCode == HttpStatusCode.NoContent)
            return AzmaMcpResult<JsonElement?>.Success(successSummary, null, sourceEndpoint);

        var content = await message.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
            return AzmaMcpResult<JsonElement?>.Success(successSummary, null, sourceEndpoint);

        var data = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        return AzmaMcpResult<JsonElement?>.Success(
            BuildSuccessSummary(successSummary, data),
            data,
            sourceEndpoint);
    }

    private async Task<ApiSendResult> SendOnceSafelyAsync(
        HttpMethod method,
        string endpoint,
        object? body,
        string sourceEndpoint,
        CancellationToken cancellationToken,
        params QueryParam[] query)
    {
        try
        {
            var response = await SendOnceAsync(method, endpoint, body, cancellationToken, query);
            return new ApiSendResult(response, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ApiSendResult(null, TransportFailure(
                "Azma API request timed out before a response was returned.",
                sourceEndpoint));
        }
        catch (HttpRequestException ex)
        {
            return new ApiSendResult(null, TransportFailure(
                $"Azma API request failed before reaching the service: {ex.Message}",
                sourceEndpoint));
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method,
        string endpoint,
        object? body,
        CancellationToken cancellationToken,
        params QueryParam[] query)
    {
        var client = _httpClientFactory.CreateClient(AzmaMcpConstants.ApiHttpClientName);
        using var request = new HttpRequestMessage(method, BuildUri(endpoint, query));

        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        return await client.SendAsync(request, cancellationToken);
    }

    private Uri BuildUri(string endpoint, params QueryParam[] query) =>
        new(_options.CurrentValue.GetInternalApiBaseUri(), BuildRelativeEndpoint(endpoint, query));

    public string BuildSourceEndpoint(string endpoint, params QueryParam[] query) =>
        "/api/" + BuildRelativeEndpoint(endpoint, query).TrimStart('/');

    private static string BuildRelativeEndpoint(string endpoint, params QueryParam[] query)
    {
        var builder = new StringBuilder(endpoint.TrimStart('/'));
        var first = !builder.ToString().Contains('?', StringComparison.Ordinal);

        foreach (var item in query)
        {
            if (item.Value is null)
                continue;

            var value = Convert.ToString(item.Value, System.Globalization.CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            builder.Append(first ? '?' : '&');
            first = false;
            builder
                .Append(Uri.EscapeDataString(item.Name))
                .Append('=')
                .Append(Uri.EscapeDataString(value));
        }

        return builder.ToString();
    }

    private static async Task<AzmaMcpResult<JsonElement?>> ToFailureAsync(
        HttpResponseMessage response,
        string sourceEndpoint,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = TryReadProblemMessage(body);
        var summary = string.IsNullOrWhiteSpace(detail)
            ? $"Azma API returned {(int)response.StatusCode} {response.ReasonPhrase}."
            : $"Azma API returned {(int)response.StatusCode} {response.ReasonPhrase}: {detail}";

        return AzmaMcpResult<JsonElement?>.Failure(
            summary,
            sourceEndpoint,
            nextActions: new[]
            {
                "Check the input IDs, required permissions, and the MCP token configured by the client."
            });
    }

    private static string? TryReadProblemMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            foreach (var key in new[] { "message", "detail", "title", "code" })
            {
                if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
            }

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                return errors.ToString();
        }
        catch (JsonException)
        {
            return body.Length <= 500 ? body : body[..500];
        }

        return null;
    }

    private static AzmaMcpResult<JsonElement?> TransportFailure(string summary, string sourceEndpoint) =>
        AzmaMcpResult<JsonElement?>.Failure(
            summary,
            sourceEndpoint,
            nextActions: new[]
            {
                "Check that the Azma service behind this endpoint is running and reachable from the Gateway."
            });

    private static string BuildSuccessSummary(string prefix, JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("items", out var items) &&
            items.ValueKind == JsonValueKind.Array)
        {
            var count = items.GetArrayLength();
            var total = data.TryGetProperty("totalCount", out var totalCount) && totalCount.TryGetInt32(out var totalValue)
                ? $" Total available: {totalValue}."
                : string.Empty;
            return $"{prefix} Retrieved {count} item(s).{total}";
        }

        if (data.ValueKind == JsonValueKind.Array)
            return $"{prefix} Retrieved {data.GetArrayLength()} item(s).";

        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("id", out var id) &&
            id.ValueKind == JsonValueKind.String)
        {
            var name = data.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? $" Name: {nameElement.GetString()}."
                : string.Empty;
            return $"{prefix} Id: {id.GetString()}.{name}";
        }

        return prefix;
    }
}

public readonly record struct QueryParam(string Name, object? Value);

internal readonly record struct ApiSendResult(
    HttpResponseMessage? Message,
    AzmaMcpResult<JsonElement?>? Failure);
