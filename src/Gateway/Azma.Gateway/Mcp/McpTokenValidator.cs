using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Azma.Gateway.Mcp;

public interface IMcpTokenValidator
{
    Task<McpTokenValidationResult?> ValidateAsync(string rawToken, CancellationToken cancellationToken);
}

public sealed class McpTokenValidator : IMcpTokenValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<McpOptions> _options;

    public McpTokenValidator(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptionsMonitor<McpOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options;
    }

    public async Task<McpTokenValidationResult?> ValidateAsync(
        string rawToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var cacheKey = BuildCacheKey(rawToken);
        if (_cache.TryGetValue(cacheKey, out McpTokenValidationResult? cached))
            return cached;

        var client = _httpClientFactory.CreateClient(AzmaMcpConstants.AuthHttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_options.CurrentValue.GetInternalApiBaseUri(), "auth/mcp-tokens/validate"));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }

        using var _ = response;
        if (!response.IsSuccessStatusCode)
            return null;

        var body = await response.Content.ReadFromJsonAsync<McpTokenValidationResult>(
            JsonOptions,
            cancellationToken);

        if (body is null || body.UserId == Guid.Empty || body.WorkspaceId == Guid.Empty)
            return null;

        var cacheSeconds = Math.Clamp(_options.CurrentValue.TokenValidationCacheSeconds, 0, 300);
        if (cacheSeconds > 0)
            _cache.Set(cacheKey, body, TimeSpan.FromSeconds(cacheSeconds));

        return body;
    }

    private static string BuildCacheKey(string rawToken)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return "azma:mcp-token:" + Convert.ToHexString(hash);
    }
}

public sealed record McpTokenValidationResult(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid WorkspaceId,
    string WorkspaceName,
    string WorkspaceSlug,
    string Role,
    string AccessScope);
