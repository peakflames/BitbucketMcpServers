namespace BitbucketRemoteMcpServer.Tests.Infrastructure;

/// <summary>
/// Boots the remote MCP server against a <see cref="FakeBitbucketServer"/> (with one registered
/// repository) and drives <c>tools/call</c> over the in-memory transport, following the same
/// harness shape as <see cref="GoldenOutputRegressionTests"/>.
/// </summary>
internal sealed class ToolTestHost : IAsyncDisposable
{
    public const string AccountName = "fake-workspace";
    public const string RepoSlug = "demo-repo";

    private readonly BitbucketMcpServerFactory _factory;

    public FakeBitbucketServer Bitbucket { get; }

    private ToolTestHost(FakeBitbucketServer bitbucket, BitbucketMcpServerFactory factory)
    {
        Bitbucket = bitbucket;
        _factory = factory;
    }

    public static async Task<ToolTestHost> StartAsync(string? accessToken = null)
    {
        var bitbucket = await FakeBitbucketServer.StartAsync();
        bitbucket.OnRepository(AccountName, RepoSlug,
            $$"""{"full_name":"{{AccountName}}/{{RepoSlug}}","name":"{{RepoSlug}}","slug":"{{RepoSlug}}","scm":"git"}""");

        var factory = new BitbucketMcpServerFactory()
            .With("BitbucketCloudConfig:AccountName", AccountName)
            .WithPostConfigureServices(services =>
                services.Replace(ServiceDescriptor.Scoped<IBitbucketClientFactory>(
                    _ => new FakeBitbucketClientFactory(bitbucket.BaseUrl, AccountName, accessToken))));

        return new ToolTestHost(bitbucket, factory);
    }

    /// <summary>Calls a tool and returns the text of its first content block.</summary>
    public async Task<string> CallToolTextAsync(string toolName, object arguments)
    {
        var client = _factory.CreateClient();

        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new { name = toolName, arguments },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}: {body}");
        return ExtractToolResultText(body);
    }

    private static string ExtractToolResultText(string body)
    {
        var dataLine = body
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .FirstOrDefault(line => line.StartsWith("data:", StringComparison.Ordinal));

        var json = dataLine is null ? body : dataLine["data:".Length..].Trim();

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
    }

    public async ValueTask DisposeAsync()
    {
        _factory.Dispose();
        await Bitbucket.DisposeAsync();
    }
}
