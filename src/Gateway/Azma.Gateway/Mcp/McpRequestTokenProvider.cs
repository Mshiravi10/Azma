namespace Azma.Gateway.Mcp;

public sealed class McpRequestTokenProvider : IAzmaTokenProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public McpRequestTokenProvider(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var token = _httpContextAccessor.HttpContext?.Items[McpHttpContextItems.InternalAccessToken] as string;
        return ValueTask.FromResult(token);
    }

    public void Invalidate()
    {
        _httpContextAccessor.HttpContext?.Items.Remove(McpHttpContextItems.InternalAccessToken);
    }
}
