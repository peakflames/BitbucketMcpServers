namespace BitbucketRemoteMcpServer.Tests;

/// <summary>McpAuth:Enabled defaults to false (the OSS default) — this must stay a strict no-op:
/// no authentication/authorization middleware, no challenge, calls succeed exactly as before auth
/// existed at all.</summary>
public class AuthDisabledRegressionTests : IClassFixture<TestServerFactory>
{
    private readonly TestServerFactory _factory;

    public AuthDisabledRegressionTests(TestServerFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProtectedResourceMetadata_IsNotServedWhenAuthIsDisabled()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/.well-known/oauth-protected-resource/mcp");

        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task McpEndpoint_ToolsList_SucceedsWithNoBearerToken()
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode);
    }
}
