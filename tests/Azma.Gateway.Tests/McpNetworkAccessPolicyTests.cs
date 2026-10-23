using System.Net;
using Azma.Gateway.Mcp;

namespace Azma.Gateway.Tests;

public sealed class McpNetworkAccessPolicyTests
{
    [Fact]
    public void Evaluate_WhenLoopbackIsRequired_RejectsLanClients()
    {
        var decision = McpNetworkAccessPolicy.Evaluate(
            new McpOptions { RequireLoopback = true },
            IPAddress.Parse("10.0.0.42"),
            "10.0.0.27",
            origin: null);

        decision.Allowed.Should().BeFalse();
        decision.Error.Should().Contain("localhost");
    }

    [Fact]
    public void Evaluate_WhenPrivateNetworkIsAllowed_AcceptsLanClients()
    {
        var decision = McpNetworkAccessPolicy.Evaluate(
            new McpOptions
            {
                RequireLoopback = false,
                AllowPrivateNetwork = true
            },
            IPAddress.Parse("10.0.0.42"),
            "10.0.0.27",
            "http://10.0.0.27:5173");

        decision.Allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData("8.8.8.8", "10.0.0.27", null)]
    [InlineData("10.0.0.42", "8.8.8.8", null)]
    [InlineData("10.0.0.42", "10.0.0.27", "https://example.com")]
    public void Evaluate_WhenPrivateNetworkIsAllowed_RejectsPublicNetworkParts(
        string remoteIpAddress,
        string host,
        string? origin)
    {
        var decision = McpNetworkAccessPolicy.Evaluate(
            new McpOptions
            {
                RequireLoopback = false,
                AllowPrivateNetwork = true
            },
            IPAddress.Parse(remoteIpAddress),
            host,
            origin);

        decision.Allowed.Should().BeFalse();
    }
}
