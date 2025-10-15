namespace BitbucketMcpTools;

public interface IBitbucketClientFactory
{
    [RequiresUnreferencedCode("Uses reflection")]
    Task<Result<BitbucketClient>> CreateClientAsync();

    string? RepoSlug { get; }
}