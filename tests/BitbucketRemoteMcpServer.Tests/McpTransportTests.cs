namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Proves the Phase 0 breaking change actually took effect: the legacy `/sse`, `/message`, and
/// root `/` mounts are gone, `/mcp` is stateless (no Mcp-Session-Id), and both the handshake and
/// tools/list still work end to end.
/// </summary>
public class McpTransportTests : IClassFixture<TestServerFactory>
{
    private readonly TestServerFactory _factory;

    public McpTransportTests(TestServerFactory factory) => _factory = factory;

    [Fact]
    public async Task LegacySseEndpoint_IsGone()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/sse");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LegacyMessageEndpoint_IsGone()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/message", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RootMcpMount_IsGone()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task McpEndpoint_Initialize_SucceedsWithoutSessionHeader()
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "method": "initialize",
                  "params": {
                    "protocolVersion": "2025-06-18",
                    "capabilities": {},
                    "clientInfo": { "name": "regression-test-client", "version": "1.0.0" }
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}: {body}");
        Assert.False(response.Headers.Contains("Mcp-Session-Id"), "Stateless mode must not emit Mcp-Session-Id.");
        Assert.Contains("serverInfo", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task McpEndpoint_ToolsList_IncludesKnownBitbucketTools()
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """
                {
                  "jsonrpc": "2.0",
                  "id": 2,
                  "method": "tools/list",
                  "params": {}
                }
                """,
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}: {body}");
        Assert.Contains("list_repositories", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("get_commit", body, StringComparison.OrdinalIgnoreCase);
    }
}
