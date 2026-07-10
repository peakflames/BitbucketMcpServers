namespace BitbucketMcpTools;

public partial class RepositoryTools
{
    [McpServerTool(Name = "list_branches"),
        Description(
            "Lists branches in a Bitbucket repository. " +
            "Results is a Markdown table containing the following columns: Name, Target Commit, Target Date. " +
            "Read-only."
        )]
    public async Task<string> ListBranches(
        [Description("The name of the Bitbucket repository to query.")]
        string repoName,

        [Description("An optional substring filter applied to the branch name. Omit to list all branches.")]
        string? filter = null)
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
            var parameters = new ListParameters
            {
                Filter = string.IsNullOrWhiteSpace(filter) ? null : $"name ~ \"{filter}\""
            };
            var branches = bitBucketClient.RepositoryResource.BranchesResource.ListBranches(parameters);

            if (branches is null || branches.Count == 0)
            {
                return $"No branches found in {bitBucketClient.RepositoryFullName} matching filter '{filter}'.";
            }

            var markdownContents = new StringBuilder();
            markdownContents.AppendLine("# Branches");
            markdownContents.AppendLine();
            markdownContents.AppendLine($"**Repository**: {bitBucketClient.RepositoryFullName} | **Total Count**: {branches.Count}");
            markdownContents.AppendLine();
            markdownContents.AppendLine("| Name | Target Commit | Target Date |");
            markdownContents.AppendLine("|------|----------------|-------------|");

            foreach (var branch in branches)
            {
                markdownContents.AppendLine($"| {branch.name} | {branch.target?.hash} | {branch.target?.date} |");
            }

            return markdownContents.ToString();
        }
        catch (Exception ex)
        {
            var returnMsg = $"ERROR: Failed to list branches due to exception '{ex.Message}'";
            if (ex.InnerException != null)
            {
                returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
            }
            return returnMsg;
        }
    }
}
