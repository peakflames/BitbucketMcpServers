namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Covers the workspace-scoped connection path <see cref="BitbucketClient"/> and
/// <see cref="BitbucketRemoteClientFactory"/> gained for <c>list_repositories</c>/<c>search_code</c>
/// — those tools have no single repository to bootstrap credentials against, so a null
/// <c>repoSlug</c> now validates against the workspace itself (<c>GET /workspaces/{workspace}</c>)
/// instead of faking a repo just to exercise the connection.
/// </summary>
public class WorkspaceScopedClientTests
{
    private const string AccountName = "fake-workspace";

    private sealed class StaticCredentialResolver(Result<string?> result) : IUpstreamCredentialResolver
    {
        public Task<Result<string?>> ResolveAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    [Fact]
    public async Task ConnectAsync_WithNoRepoSlug_ValidatesAgainstTheWorkspaceInstead()
    {
        await using var bitbucket = await FakeBitbucketServer.StartAsync();
        bitbucket.OnWorkspace(AccountName, """{"slug":"fake-workspace","name":"Fake Workspace"}""");

        var client = new BitbucketClient(AccountName, repoSlug: null, "fake-user", "fake-app-password", null, null,
                                         baseUrl: bitbucket.BaseUrl);

        var result = await client.ConnectAsync();

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Null(client.RepositoryResource);
        Assert.Null(client.Repository);
    }

    [Fact]
    public async Task ConnectAsync_WithNoRepoSlug_WorkspaceNotFound_FailsWithAWorkspaceScopedMessage()
    {
        await using var bitbucket = await FakeBitbucketServer.StartAsync();
        // Deliberately not calling OnWorkspace: FakeBitbucketServer 404s any unregistered key.

        var client = new BitbucketClient(AccountName, repoSlug: null, "fake-user", "fake-app-password", null, null,
                                         baseUrl: bitbucket.BaseUrl);

        var result = await client.ConnectAsync();

        Assert.True(result.IsFailed);
        Assert.Contains(AccountName, result.Errors.First().Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateWorkspaceClientAsync_SendsTheResolvedAccessToken_AsTheBearerHeaderOnTheWire()
    {
        await using var bitbucket = await FakeBitbucketServer.StartAsync();
        bitbucket.OnWorkspace(AccountName, """{"slug":"fake-workspace","name":"Fake Workspace"}""");

        var factory = new BitbucketRemoteClientFactory(
            new BitbucketProjectConfig { AccountName = AccountName },
            NullLogger<BitbucketRemoteClientFactory>.Instance,
            httpContextAccessor: null,
            new StaticCredentialResolver(Result.Ok<string?>("token-for-this-caller")),
            baseUrl: bitbucket.BaseUrl);

        var result = await factory.CreateWorkspaceClientAsync();

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(["Bearer token-for-this-caller"], bitbucket.AuthorizationHeaders);
    }

    [Fact]
    public async Task CreateWorkspaceClientAsync_CredentialResolutionFails_NeverCallsBitbucket()
    {
        await using var bitbucket = await FakeBitbucketServer.StartAsync();
        bitbucket.OnWorkspace(AccountName, """{"slug":"fake-workspace","name":"Fake Workspace"}""");

        var factory = new BitbucketRemoteClientFactory(
            new BitbucketProjectConfig { AccountName = AccountName, Username = "shared-user", AppPassword = "shared-password" },
            NullLogger<BitbucketRemoteClientFactory>.Instance,
            httpContextAccessor: null,
            new StaticCredentialResolver(Result.Fail<string?>("caller's Bitbucket authorization was not found")),
            baseUrl: bitbucket.BaseUrl);

        var result = await factory.CreateWorkspaceClientAsync();

        Assert.True(result.IsFailed);
        Assert.Empty(bitbucket.AuthorizationHeaders);
    }
}
