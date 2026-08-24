namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Captures the full <c>tools/call</c> body for two representative tools — a simple repo-scoped
/// GET (<c>get_commit</c>) and a paginated list (<c>list_branches</c>) — against a
/// <see cref="FakeBitbucketServer"/>, and asserts string equality against the exact markdown the
/// tool code produces. This is the pre-Phase-1 baseline: proof that the SDK 2.1.0 upgrade and the
/// stateless-transport switch in this same change are byte-identical to today's tool output, not
/// just "still returns 200". Full 11-tool coverage is tracked as a follow-up rather than blocking
/// this change — these two exercise both tool shapes (single-resource GET, paginated list) end to
/// end through the real SharpBucket request/deserialize path.
/// </summary>
public class GoldenOutputRegressionTests : IAsyncLifetime
{
    private const string AccountName = "fake-workspace";
    private const string RepoSlug = "demo-repo";

    private FakeBitbucketServer _bitbucket = null!;
    private BitbucketMcpServerFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _bitbucket = await FakeBitbucketServer.StartAsync();

        _bitbucket.OnRepository(AccountName, RepoSlug,
            $$"""{"full_name":"{{AccountName}}/{{RepoSlug}}","name":"{{RepoSlug}}","slug":"{{RepoSlug}}","scm":"git"}""");

        _factory = new BitbucketMcpServerFactory()
            .With("BitbucketCloudConfig:AccountName", AccountName)
            .WithPostConfigureServices(services =>
                services.Replace(ServiceDescriptor.Scoped<IBitbucketClientFactory>(
                    _ => new FakeBitbucketClientFactory(_bitbucket.BaseUrl, AccountName))));
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await _bitbucket.DisposeAsync();
    }

    [Fact]
    public async Task GetCommit_Output_IsByteIdentical()
    {
        _bitbucket.OnCommit(AccountName, RepoSlug, "abcdef1234567890",
            """
            {
              "hash": "abcdef1234567890",
              "author": { "raw": "Test Author <test@example.invalid>" },
              "date": "2026-08-24T12:00:00+00:00",
              "message": "Test commit message",
              "parents": [ { "hash": "1111111111111111" } ]
            }
            """);

        var body = await CallTool("get_commit", new { repoName = RepoSlug, revision = "abcdef1234567890" });
        var text = ExtractToolResultText(body);

        Assert.Equal(BuildExpectedCommitMarkdown(), text);
    }

    [Fact]
    public async Task ListBranches_Output_IsByteIdentical()
    {
        _bitbucket.OnBranches(AccountName, RepoSlug,
            """
            {
              "values": [
                { "name": "main", "target": { "hash": "aaaa1111", "date": "2026-08-20T10:00:00+00:00" } }
              ],
              "next": null
            }
            """);

        var body = await CallTool("list_branches", new { repoName = RepoSlug });
        var text = ExtractToolResultText(body);

        Assert.Equal(BuildExpectedBranchesMarkdown(), text);
    }

    private static string BuildExpectedCommitMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Commit Details");
        sb.AppendLine();
        sb.AppendLine($"**Repository**: {AccountName}/{RepoSlug}");
        sb.AppendLine("**Hash**: abcdef1234567890");
        sb.AppendLine("**Author**: Test Author <test@example.invalid>");
        sb.AppendLine("**Date**: 2026-08-24T12:00:00+00:00");
        sb.AppendLine("**Parent(s)**: 1111111111111111");
        sb.AppendLine();
        sb.AppendLine("## Message");
        sb.AppendLine();
        sb.AppendLine("Test commit message");
        return sb.ToString();
    }

    private static string BuildExpectedBranchesMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Branches");
        sb.AppendLine();
        sb.AppendLine($"**Repository**: {AccountName}/{RepoSlug} | **Total Count**: 1");
        sb.AppendLine();
        sb.AppendLine("| Name | Target Commit | Target Date |");
        sb.AppendLine("|------|----------------|-------------|");
        sb.AppendLine("| main | aaaa1111 | 2026-08-20T10:00:00+00:00 |");
        return sb.ToString();
    }

    private async Task<string> CallTool(string toolName, object arguments)
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
        return body;
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
}
