using Azma.Gateway.Mcp;
using Microsoft.AspNetCore.Http;

namespace Azma.Gateway.Tests;

public sealed class McpRequestTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_ReturnsInternalTokenFromCurrentHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Items[McpHttpContextItems.InternalAccessToken] = "jwt-token";
        var accessor = new HttpContextAccessor { HttpContext = context };
        var provider = new McpRequestTokenProvider(accessor);

        var token = await provider.GetAccessTokenAsync(CancellationToken.None);

        token.Should().Be("jwt-token");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenIsMissing_ReturnsNull()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var provider = new McpRequestTokenProvider(accessor);

        var token = await provider.GetAccessTokenAsync(CancellationToken.None);

        token.Should().BeNull();
    }
}
