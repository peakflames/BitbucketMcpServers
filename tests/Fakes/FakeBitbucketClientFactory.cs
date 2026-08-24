namespace BitbucketRemoteMcpServer.Tests.Fakes;

/// <summary>
/// Test double for <see cref="IBitbucketClientFactory"/> that builds a real
/// <see cref="BitbucketClient"/> pointed at a <see cref="FakeBitbucketServer"/> instead of
/// api.bitbucket.org. Exercises the real SharpBucket request/deserialize path, since SharpBucket
/// itself has no supported HTTP injection point below this level.
/// </summary>
public sealed class FakeBitbucketClientFactory(string baseUrl, string accountName) : IBitbucketClientFactory
{
    [RequiresUnreferencedCode("Uses reflection")]
    public async Task<Result<BitbucketClient>> CreateClientAsync(string repoSlug)
    {
        var client = new BitbucketClient(accountName, repoSlug, "fake-user", "fake-app-password", null, null, baseUrl);
        var result = await client.ConnectAsync();
        return result.IsFailed ? Result.Fail(result.Errors) : Result.Ok(client);
    }
}
