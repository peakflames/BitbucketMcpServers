namespace BitbucketMcpTools;

public partial class RepositoryTools
{
    [McpServerTool(Name = "get_commit"),
        Description(
            "Gets detailed information about a single commit in a Bitbucket repository. " +
            "Results is a Markdown document with the commit hash, author, date, message, and parent commit(s). " +
            "Read-only."
        )]
    public async Task<string> GetCommit(
        [Description("The name of the Bitbucket repository to query.")]
        string repoName,

        [Description("The commit hash (SHA1), full or abbreviated.")]
        string revision)
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
            var commit = await bitBucketClient.RepositoryResource.GetCommitAsync(revision);

            if (commit is null)
            {
                return $"ERROR: Commit '{revision}' not found in repository {repoName}";
            }

            var author = commit.author?.user?.display_name ?? commit.author?.raw ?? "Unknown";
            var parents = commit.parents is { Count: > 0 }
                ? string.Join(", ", commit.parents.Select(p => p.hash))
                : "(none)";

            var markdownContents = new StringBuilder();
            markdownContents.AppendLine("# Commit Details");
            markdownContents.AppendLine();
            markdownContents.AppendLine($"**Repository**: {bitBucketClient.RepositoryFullName}");
            markdownContents.AppendLine($"**Hash**: {commit.hash}");
            markdownContents.AppendLine($"**Author**: {author}");
            markdownContents.AppendLine($"**Date**: {commit.date}");
            markdownContents.AppendLine($"**Parent(s)**: {parents}");
            markdownContents.AppendLine();
            markdownContents.AppendLine("## Message");
            markdownContents.AppendLine();
            markdownContents.AppendLine(commit.message ?? "*No message.*");

            return markdownContents.ToString();
        }
        catch (Exception ex)
        {
            var returnMsg = $"ERROR: Failed to get commit due to exception '{ex.Message}'";
            if (ex.InnerException != null)
            {
                returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
            }
            return returnMsg;
        }
    }
}
