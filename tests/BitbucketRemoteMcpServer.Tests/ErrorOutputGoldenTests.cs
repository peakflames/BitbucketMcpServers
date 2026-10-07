namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Pins the exact error text agents see, so a 404 stays readable after error-message hardening.
/// </summary>
public class ErrorOutputGoldenTests : IAsyncLifetime
{
    private ToolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ToolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task GetCommit_UnknownRevision_ReturnsAReadable404()
    {
        var text = await _host.CallToolTextAsync("get_commit",
            new { repoName = ToolTestHost.RepoSlug, revision = "deadbeef" });

        Assert.Equal(
            $"ERROR: Failed to get commit: NotFound Not found: {ToolTestHost.AccountName}/{ToolTestHost.RepoSlug}/deadbeef",
            text);
    }

    [Fact]
    public async Task ReadFile_MissingFile_ReturnsAReadable404()
    {
        var text = await _host.CallToolTextAsync("read_file",
            new { repoName = ToolTestHost.RepoSlug, filePath = "missing.txt", @ref = "main" });

        Assert.Equal(
            $"ERROR: Failed to read file: NotFound Not found: {ToolTestHost.AccountName}/{ToolTestHost.RepoSlug}/main/missing.txt",
            text);
    }
}
