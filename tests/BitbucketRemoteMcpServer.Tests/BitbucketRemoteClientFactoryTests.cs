namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Proves the wiring between <see cref="IUpstreamCredentialResolver"/> and
/// <see cref="BitbucketRemoteClientFactory"/>: whatever access token the resolver returns is what
/// actually reaches the wire (observed at a <see cref="FakeBitbucketServer"/>, not inferred from
/// configuration), and a resolver failure short-circuits before any Bitbucket call is made rather
/// than silently falling back to the shared credential.
/// </summary>
public class BitbucketRemoteClientFactoryTests
{
    private const string AccountName = "fake-workspace";
    private const string RepoSlug = "demo-repo";

    private const string RepositoryJson =
        """{"full_name":"fake-workspace/demo-repo","name":"demo-repo","slug":"demo-repo","scm":"git"}""";

    private sealed class StaticCredentialResolver(Result<string?> result) : IUpstreamCredentialResolver
    {
        public Task<Result<string?>> ResolveAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    [Fact]
    public async Task CreateClientAsync_SendsTheResolvedAccessToken_AsTheBearerHeaderOnTheWire()
    {
        await using var bitbucket = await FakeBitbucketServer.StartAsync();
        bitbucket.OnRepository(AccountName, RepoSlug, RepositoryJson);

        var factory = new BitbucketRemoteClientFactory(
            new BitbucketProjectConfig { AccountName = AccountName },
            NullLogger<BitbucketRemoteClientFactory>.Instance,
            httpContextAccessor: null,
            new StaticCredentialResolver(Result.Ok<string?>("token-for-this-caller")),
            baseUrl: bitbucket.BaseUrl);

        var result = await factory.CreateClientAsync(RepoSlug);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(["Bearer token-for-this-caller"], bitbucket.AuthorizationHeaders);
    }

    [Fact]
    public async Task CreateClientAsync_DifferentResolvedTokens_ProduceDifferentBearerHeaders()
    {
        await using var bitbucket = await FakeBitbucketServer.StartAsync();
        bitbucket.OnRepository(AccountName, RepoSlug, RepositoryJson);

        Task<Result<BitbucketClient>> CreateFor(string accessToken) => new BitbucketRemoteClientFactory(
            new BitbucketProjectConfig { AccountName = AccountName },
            NullLogger<BitbucketRemoteClientFactory>.Instance,
            httpContextAccessor: null,
            new StaticCredentialResolver(Result.Ok<string?>(accessToken)),
            baseUrl: bitbucket.BaseUrl).CreateClientAsync(RepoSlug);

        Assert.True((await CreateFor("token-for-user-a")).IsSuccess);
        Assert.True((await CreateFor("token-for-user-b")).IsSuccess);

        Assert.Equal(["Bearer token-for-user-a", "Bearer token-for-user-b"], bitbucket.AuthorizationHeaders);
    }

    [Fact]
    public async Task CreateClientAsync_CredentialResolutionFails_NeverCallsBitbucketAndNeverFallsBackToTheSharedCredential()
    {
        await using var bitbucket = await FakeBitbucketServer.StartAsync();
        bitbucket.OnRepository(AccountName, RepoSlug, RepositoryJson);

        var factory = new BitbucketRemoteClientFactory(
            new BitbucketProjectConfig { AccountName = AccountName, Username = "shared-user", AppPassword = "shared-password" },
            NullLogger<BitbucketRemoteClientFactory>.Instance,
            httpContextAccessor: null,
            new StaticCredentialResolver(Result.Fail<string?>("caller's Bitbucket authorization was not found")),
            baseUrl: bitbucket.BaseUrl);

        var result = await factory.CreateClientAsync(RepoSlug);

        Assert.True(result.IsFailed);
        Assert.Empty(bitbucket.AuthorizationHeaders);
    }
}
