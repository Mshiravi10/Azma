using Azma.Gateway.Mcp;

namespace Azma.Gateway.Tests;

public sealed class McpAuditLoggingTests
{
    [Fact]
    public void DescribeRequest_ForToolCall_ReadsToolNameAndSanitizesArguments()
    {
        var request = McpAuditLogging.DescribeRequest(
            """
            {
              "jsonrpc": "2.0",
              "id": 7,
              "method": "tools/call",
              "params": {
                "name": "azma_create_project",
                "arguments": {
                  "name": "Azma",
                  "currentPassword": "secret-password",
                  "nested": { "authorization": "Bearer abc" }
                }
              }
            }
            """,
            new McpOptions());

        request.Id.Should().Be("7");
        request.Method.Should().Be("tools/call");
        request.ToolName.Should().Be("azma_create_project");
        request.ArgumentsJson.Should().Contain("\"name\":\"Azma\"");
        request.ArgumentsJson.Should().Contain("\"currentPassword\":\"[redacted]\"");
        request.ArgumentsJson.Should().Contain("\"authorization\":\"[redacted]\"");
        request.ArgumentsJson.Should().NotContain("secret-password");
        request.ArgumentsJson.Should().NotContain("Bearer abc");
    }

    [Fact]
    public void DescribeResponse_ForToolResult_ReadsOutcomeSummaryAndSourceEndpoint()
    {
        var response = McpAuditLogging.DescribeResponse(
            """
            event: message
            data: {"result":{"content":[{"type":"text","text":"{\"ok\":false,\"summary\":\"Azma API returned 409 Conflict.\",\"sourceEndpoint\":\"/api/testcases\"}"}]},"id":7,"jsonrpc":"2.0"}

            """,
            200);

        response.Outcome.Should().Be("failed");
        response.Summary.Should().Be("Azma API returned 409 Conflict.");
        response.SourceEndpoint.Should().Be("/api/testcases");
    }
}
