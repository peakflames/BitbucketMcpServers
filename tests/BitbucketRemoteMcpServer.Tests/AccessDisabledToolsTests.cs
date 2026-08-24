namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Access:DisabledTools is the transitional-window seam: once authentication exists but a later
/// phase's per-user credential passthrough does not, `search_code` and `list_repositories` must
/// disappear from tools/list and be denied on tools/call — a permission mirror was explicitly
/// rejected as an alternative, so removing them is the only safe interim choice. This must
/// require McpAuth:Enabled (AccessOptionsValidator) and must be a strict no-op in AuditOnly mode.
/// </summary>
public sealed class AccessDisabledToolsTests : IAsyncLifetime
{
    private const string ResourceUri = "https://bitbucket-mcp.example.invalid/mcp";

    private readonly StubAuthorizationServerFixture _stubAs = new();
    private BitbucketMcpServerFactory? _factory;

    public async Task InitializeAsync()
    {
        await _stubAs.InitializeAsync();
        _stubAs.State.DefaultAudience = ResourceUri;

        _factory = new BitbucketMcpServerFactory()
            .WithEnvironment("Development")
            .With("McpAuth:Enabled", "true")
            .With("McpAuth:Issuer", _stubAs.IssuerUrl)
            .With("McpAuth:ResourceUri", ResourceUri)
            .With("Access:Enabled", "true")
            .With("Access:DisabledTools:0", "search_code")
            .With("Access:DisabledTools:1", "list_repositories");
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _stubAs.DisposeAsync();
    }

    [Fact]
    public async Task ToolsList_OmitsDisabledTools()
    {
        var body = await SendToolsListAsync();

        Assert.DoesNotContain("search_code", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("list_repositories", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("get_commit", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Full wire-level parity with a genuinely unregistered tool name is not achievable here: the
    /// SDK answers a truly unknown name with a JSON-RPC protocol error (-32602) from a check that
    /// runs before the filter pipeline, while a CallToolFilter can only ever produce a
    /// CallToolResult (a registered tool's own error shape). What this filter can and does
    /// guarantee is that the message never names the real reason (an access policy) — it reads
    /// exactly like a client trying to call a tool name that isn't registered.
    /// </summary>
    [Fact]
    public async Task CallTool_OnDisabledTool_ReadsAsAnUnknownToolNotAnAccessDenial()
    {
        var body = await SendCallToolAsync("search_code", """{"searchQuery":"eVTOL"}""");

        Assert.Contains("\"isError\":true", body, StringComparison.Ordinal);
        Assert.Contains("Unknown tool", body, StringComparison.Ordinal);
        Assert.Contains("search_code", body, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("policy", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> SendToolsListAsync()
    {
        var client = await CreateAuthenticatedClientAsync();

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
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}: {body}");
        return body;
    }

    private async Task<string> SendCallToolAsync(string toolName, string argumentsJson)
    {
        var client = await CreateAuthenticatedClientAsync();

        var payload = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"" +
            toolName + "\",\"arguments\":" + argumentsJson + "}}";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var token = await _stubAs.IssueTokenAsync();
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

}
