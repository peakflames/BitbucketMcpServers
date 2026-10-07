namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Regression for a file served as a redirect to a presigned URL on another host: the upstream
/// credential must not be forwarded there, and nothing the other host echoes back may reach the
/// tool result.
/// </summary>
public class RedirectReflectionTests : IAsyncLifetime
{
    private const string Token = "tok-9f8e7d6c5b4a-do-not-leak";

    private ToolTestHost _host = null!;
    private FakeS3Server _s3 = null!;

    public async Task InitializeAsync()
    {
        _host = await ToolTestHost.StartAsync(accessToken: Token);
        _s3 = await FakeS3Server.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _s3.DisposeAsync();
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task ReadFile_CrossOriginRedirect_DoesNotForwardOrReflectTheCredential()
    {
        _host.Bitbucket.OnSrcRedirect(
            $"{ToolTestHost.AccountName}/{ToolTestHost.RepoSlug}/main/big.bin",
            $"{_s3.BaseUrl}/bucket/object?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Signature=cafebabe");

        var text = await _host.CallToolTextAsync("read_file",
            new { repoName = ToolTestHost.RepoSlug, filePath = "big.bin", @ref = "main" });

        Assert.StartsWith("ERROR: Failed to read file:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Token, text, StringComparison.Ordinal);
        Assert.DoesNotContain("cafebabe", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentValue", text, StringComparison.Ordinal);

        Assert.Equal(1, _s3.RequestCount);
        Assert.Empty(_s3.AuthorizationHeaders);
        Assert.Contains(_host.Bitbucket.AuthorizationHeaders, header => header == $"Bearer {Token}");
    }
}
