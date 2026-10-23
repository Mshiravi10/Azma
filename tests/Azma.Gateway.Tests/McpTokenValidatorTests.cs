using System.Net;
using Azma.Gateway.Mcp;
using Microsoft.Extensions.Caching.Memory;

namespace Azma.Gateway.Tests;

public sealed class McpTokenValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenIdentityAcceptsToken_ReturnsValidationResult()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """
        {
          "userId": "11111111-1111-1111-1111-111111111111",
          "email": "user@azma.local",
          "displayName": "Azma User",
          "workspaceId": "22222222-2222-2222-2222-222222222222",
          "workspaceName": "Workspace",
          "workspaceSlug": "workspace",
          "role": "Admin",
          "accessScope": "Workspace"
        }
        """);
        var validator = CreateValidator(handler);

        var result = await validator.ValidateAsync("azma_mcp_valid", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Email.Should().Be("user@azma.local");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.ToString().Should().Be("http://localhost:8080/api/auth/mcp-tokens/validate");
        handler.Requests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.Should().Be("azma_mcp_valid");
    }

    [Fact]
    public async Task ValidateAsync_WhenTokenWasRecentlyValidated_UsesCache()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """
        {
          "userId": "11111111-1111-1111-1111-111111111111",
          "email": "user@azma.local",
          "displayName": "Azma User",
          "workspaceId": "22222222-2222-2222-2222-222222222222",
          "workspaceName": "Workspace",
          "workspaceSlug": "workspace",
          "role": "Admin",
          "accessScope": "Workspace"
        }
        """);
        var validator = CreateValidator(handler);

        var first = await validator.ValidateAsync("azma_mcp_valid", CancellationToken.None);
        var second = await validator.ValidateAsync("azma_mcp_valid", CancellationToken.None);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second.Should().BeSameAs(first);
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ValidateAsync_WhenIdentityRejectsToken_ReturnsNull()
    {
        var handler = new QueueHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized);
        var validator = CreateValidator(handler);

        var result = await validator.ValidateAsync("azma_mcp_invalid", CancellationToken.None);

        result.Should().BeNull();
    }

    private static McpTokenValidator CreateValidator(QueueHttpMessageHandler handler) =>
        new(
            new StubHttpClientFactory(handler),
            new MemoryCache(new MemoryCacheOptions()),
            new StubOptionsMonitor<McpOptions>(new McpOptions
            {
                InternalApiBaseUrl = "http://localhost:8080/api",
                TokenValidationCacheSeconds = 60
            }));
}
