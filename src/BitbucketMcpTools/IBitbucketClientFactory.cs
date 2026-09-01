namespace BitbucketMcpTools;

public interface IBitbucketClientFactory
{
    [RequiresUnreferencedCode("Uses reflection")]
    Task<Result<BitbucketClient>> CreateClientAsync(string repoSlug);

    /// <summary>For tools that operate across the whole workspace (list_repositories,
    /// search_code) rather than a single repository — validates the resolved credential against
    /// the workspace itself instead of requiring a repo to bootstrap against.</summary>
    [RequiresUnreferencedCode("Uses reflection")]
    Task<Result<BitbucketClient>> CreateWorkspaceClientAsync();
}