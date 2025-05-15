
namespace BitbucketMcpTools;

public interface IBitbucketClientFactory
{
    [RequiresUnreferencedCode("Uses reflection")]
    Task<Result<BitbucketClient>> CreateClientAsync();
}

public class BitbucketClientFactory(BitBucketConfig config) : IBitbucketClientFactory
{
    private readonly BitBucketConfig _config = config;

    [RequiresUnreferencedCode("Uses reflection")]
    public async Task<Result<BitbucketClient>> CreateClientAsync()
    {
        var bitbucketClient = new BitbucketClient(_config.BitbucketUsername, _config.BitbucketAppPassword, _config.AccountName, _config.RepoSlug);
        var result = await bitbucketClient.ConnectAsync();
        if (result.IsFailed)
        {
            return Result.Fail(result.Errors.First());
        }

        return Result.Ok(bitbucketClient);
    }
}
