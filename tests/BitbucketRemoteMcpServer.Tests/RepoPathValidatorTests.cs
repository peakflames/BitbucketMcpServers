namespace BitbucketRemoteMcpServer.Tests;

public class RepoPathValidatorTests
{
    [Theory]
    [InlineData("../x")]
    [InlineData("a/../../x")]
    [InlineData("a/./b")]
    [InlineData("..%2fx")]
    [InlineData("..%2Fx")]
    [InlineData("%2e%2e/x")]
    [InlineData("%2E%2E/x")]
    [InlineData("%252e%252e/x")]
    [InlineData("%25252e/x")]
    [InlineData("a%5cb")]
    [InlineData("a%255Cb")]
    [InlineData("a\\b")]
    [InlineData("//abs")]
    [InlineData("//")]
    [InlineData("/../x")]
    [InlineData("/%2e%2e/x")]
    [InlineData("/")]
    [InlineData("a?b")]
    [InlineData("a#b")]
    [InlineData("a\nb")]
    [InlineData("a\0b")]
    [InlineData("a//b")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateAndEscapePath_Rejects(string? path) =>
        Assert.True(RepoPathValidator.ValidateAndEscapePath(path, allowEmpty: false).IsFailed);

    [Theory]
    [InlineData("a/b c/d.txt", "a/b%20c/d.txt")]
    [InlineData("README.md", "README.md")]
    [InlineData("/README.md", "README.md")]
    [InlineData("/src/b c/d.txt", "src/b%20c/d.txt")]
    [InlineData("src/dir/", "src/dir")]
    [InlineData("/src/dir/", "src/dir")]
    [InlineData("100%.txt", "100%25.txt")]
    [InlineData("dir/file+name&x=1.txt", "dir/file%2Bname%26x%3D1.txt")]
    public void ValidateAndEscapePath_AcceptsAndEscapesEachSegmentOnce(string path, string expected)
    {
        var result = RepoPathValidator.ValidateAndEscapePath(path, allowEmpty: false);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    public void ValidateAndEscapePath_AllowsEmpty_ForTheDirectoryRoot(string? path)
    {
        var result = RepoPathValidator.ValidateAndEscapePath(path, allowEmpty: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Value);
    }

    [Theory]
    [InlineData("feature/x")]
    [InlineData("release/1.2.3")]
    [InlineData("main")]
    [InlineData("0123456789abcdef0123456789abcdef01234567")]
    [InlineData(null)]
    public void ValidateRef_Accepts(string? @ref) =>
        Assert.True(RepoPathValidator.ValidateRef(@ref).IsSuccess);

    [Theory]
    [InlineData("..")]
    [InlineData("a/../b")]
    [InlineData("%2e%2e")]
    [InlineData("%252e%252e")]
    [InlineData("a%2fb")]
    [InlineData("a\\b")]
    [InlineData("a?b")]
    [InlineData("a#b")]
    [InlineData("a\nb")]
    public void ValidateRef_Rejects(string @ref) =>
        Assert.True(RepoPathValidator.ValidateRef(@ref).IsFailed);
}

/// <summary>
/// End-to-end: rejected inputs never produce a request to the upstream, and accepted inputs reach
/// it with exactly one level of percent-encoding.
/// </summary>
public class ReadFilePathHardeningTests : IAsyncLifetime
{
    private ToolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ToolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static bool IsSrcRequest(string path) => path.Contains("/src/", StringComparison.Ordinal);

    [Theory]
    [InlineData("../../x")]
    [InlineData("..%2f..%2fx")]
    [InlineData("%2e%2e/x")]
    [InlineData("%252e%252e/x")]
    [InlineData("a\\..\\x")]
    [InlineData("//abs/path")]
    [InlineData("a?b=c")]
    [InlineData("a#frag")]
    public async Task ReadFile_RejectsUnsafePaths_WithoutReachingTheUpstream(string filePath)
    {
        var text = await _host.CallToolTextAsync("read_file",
            new { repoName = ToolTestHost.RepoSlug, filePath, @ref = "main" });

        Assert.StartsWith("ERROR: Invalid path '", text, StringComparison.Ordinal);
        Assert.DoesNotContain(_host.Bitbucket.RequestedPaths, IsSrcRequest);
    }

    [Theory]
    [InlineData("../../x")]
    [InlineData("%2e%2e/x")]
    public async Task ListDirectory_RejectsUnsafePaths_WithoutReachingTheUpstream(string path)
    {
        var text = await _host.CallToolTextAsync("list_directory",
            new { repoName = ToolTestHost.RepoSlug, path, @ref = "main" });

        Assert.StartsWith("ERROR: Invalid path '", text, StringComparison.Ordinal);
        Assert.DoesNotContain(_host.Bitbucket.RequestedPaths, IsSrcRequest);
    }

    /// <summary>
    /// Documents what Peakflames.SharpBucket does on its own with the same inputs, bypassing the
    /// tool-level validator: every dot-segment and encoded variant is refused before a request is
    /// sent. The validator above is defense in depth on top of this.
    /// </summary>
    [Theory]
    [InlineData("../../x")]
    [InlineData("..%2f..%2fx")]
    [InlineData("%2e%2e/x")]
    [InlineData("%252e%252e/x")]
    [InlineData("a\b")]
    public async Task SharpBucket_RefusesUnsafeSegments_BeforeSendingARequest(string filePath)
    {
        var client = new BitbucketClient(ToolTestHost.AccountName, ToolTestHost.RepoSlug, "fake-user", "fake-app-password",
                                         null, null, baseUrl: _host.Bitbucket.BaseUrl);
        Assert.True((await client.ConnectAsync()).IsSuccess);
        var src = client.RepositoryResource!.SrcResource("main", null);

        await Assert.ThrowsAsync<ArgumentException>(() => src.GetFileContentAsync(filePath));

        Assert.DoesNotContain(_host.Bitbucket.RequestedPaths, IsSrcRequest);
    }

    [Fact]
    public async Task ReadFile_RejectsAnUnsafeRef_WithoutReachingTheUpstream()
    {
        var text = await _host.CallToolTextAsync("read_file",
            new { repoName = ToolTestHost.RepoSlug, filePath = "a.txt", @ref = "../other" });

        Assert.StartsWith("ERROR: Invalid ref '", text, StringComparison.Ordinal);
        Assert.DoesNotContain(_host.Bitbucket.RequestedPaths, IsSrcRequest);
    }

    [Fact]
    public async Task ReadFile_NestedPathWithASpace_ReachesTheUpstreamEncodedExactlyOnce()
    {
        _host.Bitbucket.OnSrc($"{ToolTestHost.AccountName}/{ToolTestHost.RepoSlug}/main/a/b c/d.txt", "hello");

        var text = await _host.CallToolTextAsync("read_file",
            new { repoName = ToolTestHost.RepoSlug, filePath = "a/b c/d.txt", @ref = "main" });

        Assert.Contains("# a/b c/d.txt", text, StringComparison.Ordinal);
        Assert.Contains("hello", text, StringComparison.Ordinal);

        var srcRequest = Assert.Single(_host.Bitbucket.RequestedPaths, IsSrcRequest);
        Assert.Contains("/src/main/a/b%20c/d.txt", srcRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("%2520", srcRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadFile_LeadingSlashPath_IsTreatedAsRepositoryRelative()
    {
        _host.Bitbucket.OnSrc($"{ToolTestHost.AccountName}/{ToolTestHost.RepoSlug}/main/a/d.txt", "rooted");

        var text = await _host.CallToolTextAsync("read_file",
            new { repoName = ToolTestHost.RepoSlug, filePath = "/a/d.txt", @ref = "main" });

        Assert.Contains("rooted", text, StringComparison.Ordinal);
        var srcRequest = Assert.Single(_host.Bitbucket.RequestedPaths, IsSrcRequest);
        Assert.Contains("/src/main/a/d.txt", srcRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("/src/main//", srcRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadFile_SlashedBranchRef_IsPassedThroughUnescaped()
    {
        _host.Bitbucket.OnSrc($"{ToolTestHost.AccountName}/{ToolTestHost.RepoSlug}/feature/x/a.txt", "on a branch");

        var text = await _host.CallToolTextAsync("read_file",
            new { repoName = ToolTestHost.RepoSlug, filePath = "a.txt", @ref = "feature/x" });

        Assert.Contains("on a branch", text, StringComparison.Ordinal);
        var srcRequest = Assert.Single(_host.Bitbucket.RequestedPaths, IsSrcRequest);
        Assert.Contains("/src/feature/x/a.txt", srcRequest, StringComparison.Ordinal);
    }
}
