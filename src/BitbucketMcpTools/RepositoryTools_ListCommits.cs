namespace BitbucketMcpTools;

public partial class RepositoryTools
{
    [McpServerTool(Name = "list_commits"),
        Description(
            "Lists commits in a Bitbucket repository, newest first. " +
            "Results is a Markdown table containing the following columns: Hash, Author, Date, Message. " +
            "Read-only."
        )]
    public async Task<string> ListCommits(
        [Description("The name of the Bitbucket repository to query.")]
        string repoName,

        [Description("An optional branch or tag name to list commits from. Omit to list commits across all branches, bookmarks, and tags.")]
        string? branch = null,

        [Description("The maximum number of commits to return. 0 returns all matching results. Default is 25.")]
        int maxResults = 25)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<IBitbucketClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync(repoName);

        if (clientResult.IsFailed)
        {
            return clientResult.Errors.First().ToString() ?? "Internal Error unknown error when creating Bitbucket client";
        }

        var bitBucketClient = clientResult.Value;
        if (bitBucketClient is null || bitBucketClient.RepositoryResource is null)
        {
            return "Internal Error unknown error when creating Bitbucket client";
        }

        try
        {
            var commits = bitBucketClient.RepositoryResource.ListCommits(branch, new ListCommitsParameters { Max = maxResults });

            if (commits is null || commits.Count == 0)
            {
                return $"No commits found in {bitBucketClient.RepositoryFullName}" + (branch is null ? "." : $" for branch/tag '{branch}'.");
            }

            var markdownContents = new StringBuilder();
            markdownContents.AppendLine("# Commits");
            markdownContents.AppendLine();
            markdownContents.AppendLine($"**Repository**: {bitBucketClient.RepositoryFullName} | **Branch/Tag**: {branch ?? "(all)"} | **Total Count**: {commits.Count}");
            markdownContents.AppendLine();
            markdownContents.AppendLine("| Hash | Author | Date | Message |");
            markdownContents.AppendLine("|------|--------|------|---------|");

            foreach (var commit in commits)
            {
                var author = commit.author?.user?.display_name ?? commit.author?.raw ?? "Unknown";
                var message = commit.message?.Split('\n').FirstOrDefault() ?? string.Empty;
                markdownContents.AppendLine($"| {commit.hash} | {author} | {commit.date} | {message} |");
            }

            return markdownContents.ToString();
        }
        catch (Exception ex)
        {
            var returnMsg = $"ERROR: Failed to list commits due to exception '{ex.Message}'";
            if (ex.InnerException != null)
            {
                returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
            }
            return returnMsg;
        }
    }
}
