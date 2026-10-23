using System.Net;
using System.Text.Json;
using Azma.Gateway.Mcp;

namespace Azma.Gateway.Tests;

public sealed class AzmaApiClientTests
{
    [Fact]
    public async Task GetAsync_AddsBearerTokenAndSummarizesPagedResults()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"items":[{"id":"a"},{"id":"b"}],"totalCount":7}""");
        var tokenProvider = new StubTokenProvider("abc123");
        var client = CreateClient(handler, tokenProvider);

        var result = await client.GetAsync(
            "/projects",
            "Listed projects.",
            CancellationToken.None,
            new QueryParam("page", 2),
            new QueryParam("pageSize", 25));

        result.Ok.Should().BeTrue();
        result.SourceEndpoint.Should().Be("/api/projects?page=2&pageSize=25");
        result.Summary.Should().Contain("Retrieved 2 item(s)");
        result.Summary.Should().Contain("Total available: 7");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.Should().Be("abc123");
    }

    [Fact]
    public async Task GetAsync_WhenApiReturnsProblemDetails_MapsFailure()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.BadRequest, """{"code":"bad.input","message":"Invalid project id"}""");
        var client = CreateClient(handler, new StubTokenProvider("abc123"));

        var result = await client.GetAsync("/projects/not-a-guid", "Loaded project.", CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Summary.Should().Contain("400");
        result.Summary.Should().Contain("Invalid project id");
        result.NextActions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAsync_WhenUnauthorized_InvalidatesTokenAndRetriesOnce()
    {
        var handler = new QueueHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized);
        handler.EnqueueJson(HttpStatusCode.OK, """{"id":"11111111-1111-1111-1111-111111111111","name":"Azma"}""");
        var tokenProvider = new StubTokenProvider("abc123");
        var client = CreateClient(handler, tokenProvider);

        var result = await client.GetAsync("/projects/11111111-1111-1111-1111-111111111111", "Loaded project.", CancellationToken.None);

        result.Ok.Should().BeTrue();
        tokenProvider.Invalidations.Should().Be(1);
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAsync_WhenApiTransportFails_ReturnsMcpFailure()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("connection refused"));
        var client = CreateClient(handler, new StubTokenProvider("abc123"));

        var result = await client.GetAsync("/projects", "Listed projects.", CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Summary.Should().Contain("connection refused");
        result.SourceEndpoint.Should().Be("/api/projects");
        result.NextActions.Should().ContainSingle();
    }

    [Fact]
    public async Task DangerousTool_WhenConfirmIsFalse_DoesNotCallApi()
    {
        var handler = new QueueHttpMessageHandler();
        var tools = new AzmaMcpTools(CreateClient(handler, new StubTokenProvider("abc123")));

        var result = await tools.azma_delete_test_case(Guid.NewGuid(), confirm: false, CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Summary.Should().Contain("Confirmation required");
        handler.Requests.Should().BeEmpty();
    }

    private static AzmaApiClient CreateClient(QueueHttpMessageHandler handler, IAzmaTokenProvider tokenProvider) =>
        new(
            new StubHttpClientFactory(handler),
            tokenProvider,
            new StubOptionsMonitor<McpOptions>(new McpOptions
            {
                InternalApiBaseUrl = "http://localhost:8080/api"
            }));
}
