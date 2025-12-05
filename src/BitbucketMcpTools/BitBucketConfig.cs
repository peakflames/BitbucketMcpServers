namespace BitbucketMcpTools;

public record BitBucketConfig(string AccountName, string RepoSlug, string? BitbucketUsername, string? BitbucketAppPassword, string? BitbucketConsumerKey, string? BitbucketSecretKey);