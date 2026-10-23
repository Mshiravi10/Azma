using System.IdentityModel.Tokens.Jwt;
using Azma.Auth;
using Azma.Auth.Jwt;
using Azma.Gateway.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Azma.Gateway.Tests;

public sealed class McpTokenAuthenticationTests
{
    [Fact]
    public async Task TryAuthenticateAsync_WhenAuthorizationIsMissing_ReturnsUnauthorized()
    {
        var context = CreateContext(new StubMcpTokenValidator(null));

        var authenticated = await McpTokenAuthentication.TryAuthenticateAsync(context);

        authenticated.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task TryAuthenticateAsync_WhenTokenIsInvalid_ReturnsUnauthorized()
    {
        var context = CreateContext(new StubMcpTokenValidator(null));
        context.Request.Headers.Authorization = "Bearer azma_mcp_invalid";

        var authenticated = await McpTokenAuthentication.TryAuthenticateAsync(context);

        authenticated.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task TryAuthenticateAsync_WhenTokenIsValid_StoresInternalJwtForSamePrincipal()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var principal = new McpTokenValidationResult(
            userId,
            "user@azma.local",
            "Azma User",
            workspaceId,
            "Workspace",
            "workspace",
            "Admin",
            "Workspace");
        var validator = new StubMcpTokenValidator(principal);
        var context = CreateContext(validator);
        context.Request.Headers.Authorization = "Bearer azma_mcp_valid";

        var authenticated = await McpTokenAuthentication.TryAuthenticateAsync(context);

        authenticated.Should().BeTrue();
        validator.SeenToken.Should().Be("azma_mcp_valid");

        var internalToken = context.Items[McpHttpContextItems.InternalAccessToken] as string;
        internalToken.Should().NotBeNullOrWhiteSpace();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(internalToken);
        jwt.Claims.Single(x => x.Type == AzmaClaims.UserId).Value.Should().Be(userId.ToString());
        jwt.Claims.Single(x => x.Type == AzmaClaims.WorkspaceId).Value.Should().Be(workspaceId.ToString());
        jwt.Claims.Single(x => x.Type == AzmaClaims.Role).Value.Should().Be("Admin");
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Basic abc", "")]
    [InlineData("Bearer azma_mcp_123", "azma_mcp_123")]
    [InlineData("bearer azma_mcp_123", "azma_mcp_123")]
    public void ReadBearerToken_ReturnsOnlyBearerToken(string? header, string expected)
    {
        McpTokenAuthentication.ReadBearerToken(header).Should().Be(expected);
    }

    private static DefaultHttpContext CreateContext(IMcpTokenValidator validator)
    {
        var services = new ServiceCollection();
        services.AddSingleton(validator);
        services.AddSingleton<IJwtTokenGenerator>(new JwtTokenGenerator(new JwtOptions
        {
            Issuer = "azma",
            Audience = "azma-clients",
            SigningKey = "dev-only-please-change-this-key-min-32-chars-long",
            AccessTokenMinutes = 30
        }));

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
    }

    private sealed class StubMcpTokenValidator : IMcpTokenValidator
    {
        private readonly McpTokenValidationResult? _result;

        public StubMcpTokenValidator(McpTokenValidationResult? result) => _result = result;

        public string? SeenToken { get; private set; }

        public Task<McpTokenValidationResult?> ValidateAsync(string rawToken, CancellationToken cancellationToken)
        {
            SeenToken = rawToken;
            return Task.FromResult(_result);
        }
    }
}
