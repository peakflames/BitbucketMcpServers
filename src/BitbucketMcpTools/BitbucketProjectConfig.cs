namespace BitbucketMcpTools;

public class BitbucketProjectConfig
{
    public string RepoSlug { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
    public bool Default { get; set; } = false;
}
