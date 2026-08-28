namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Asserts the header <see cref="BitbucketClient"/> actually puts on the wire for each credential
/// shape it supports, observed at a <see cref="FakeBitbucketServer"/> rather than inferred from the
/// configuration it was handed. The bearer path is what per-caller credentials will ride on; the
/// shared-credential case is kept alongside it as the control that proves the existing path is
/// untouched, and that the assertion could tell the two apart.
/// </summary>
public class BearerTokenAuthenticationTests
{
    private const string AccountName = "fake-workspace";
    private const string RepoSlug = "demo-repo";

    private const string RepositoryJson =
        """{"full_name":"fake-workspace/demo-repo","name":"demo-repo","slug":"demo-repo","scm":"git"}""";

    [Fact]
    public async Task ConnectAsync_WithAnAccessToken_SendsItAsABearerToken()
    {
        await using var bitbucket = await FakeBitbucketServer.StartAsync();
        bitbucket.OnRepository(AccountName, RepoSlug, RepositoryJson);

        var client = new BitbucketClient(AccountName, RepoSlug, null, null, null, null,
                                         accessToken: "a-fake-access-token", baseUrl: bitbucket.BaseUrl);

        var result = await client.ConnectAsync();

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(["Bearer a-fake-access-token"], bitbucket.AuthorizationHeaders);
    }

    [Fact]
    public async Task ConnectAsync_WithoutAnAccessToken_StillUsesTheSharedCredential()
    {
        await using var bitbucket = await FakeBitbucketServer.StartAsync();
        bitbucket.OnRepository(AccountName, RepoSlug, RepositoryJson);

        var client = new BitbucketClient(AccountName, RepoSlug, "fake-user", "fake-app-password", null, null,
                                         accessToken: null, baseUrl: bitbucket.BaseUrl);

        var result = await client.ConnectAsync();

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.NotEmpty(bitbucket.AuthorizationHeaders);
        Assert.All(bitbucket.AuthorizationHeaders, header => Assert.StartsWith("Basic ", header));
    }
}
