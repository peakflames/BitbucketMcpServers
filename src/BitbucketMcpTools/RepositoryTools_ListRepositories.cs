namespace BitbucketMcpTools;

public partial class RepositoryTools
{
    [McpServerTool(Name = "list_repositories"),
        Description(
            "Lists repositories in the Bitbucket workspace. " +
            "Results is a Markdown table containing the following columns: Slug, Full Name, Description, Language, Updated. " +
            "Read-only."
        )]
    public async Task<string> ListRepositories(
        [Description("The name of a Bitbucket repository used only to establish workspace access/credentials; the listing itself spans the whole workspace.")]
        string repoName,

        [Description("An optional substring filter applied to the repository name/slug. Omit to list all repositories.")]
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
        if (bitBucketClient is null || bitBucketClient.SharpBucket is null)
        {
            return "Internal Error unknown error when creating Bitbucket client";
        }

        try
        {
            var parameters = new ListRepositoriesParameters
            {
                Filter = string.IsNullOrWhiteSpace(filter) ? null : $"name ~ \"{filter}\""
            };
            var repositories = bitBucketClient.SharpBucket
                .RepositoriesEndPoint()
                .RepositoriesResource(bitBucketClient.AccountName)
                .ListRepositories(parameters);

            if (repositories is null || repositories.Count == 0)
            {
                return $"No repositories found in workspace '{bitBucketClient.AccountName}' matching filter '{filter}'.";
            }

            var markdownContents = new StringBuilder();
            markdownContents.AppendLine("# Repositories");
            markdownContents.AppendLine();
            markdownContents.AppendLine($"**Workspace**: {bitBucketClient.AccountName} | **Total Count**: {repositories.Count}");
            markdownContents.AppendLine();
            markdownContents.AppendLine("| Slug | Full Name | Description | Language | Updated |");
            markdownContents.AppendLine("|------|-----------|-------------|----------|---------|");

            foreach (var repo in repositories)
            {
                markdownContents.AppendLine($"| {repo.slug} | {repo.full_name} | {repo.description} | {repo.language} | {repo.updated_on} |");
            }

            return markdownContents.ToString();
        }
        catch (Exception ex)
        {
            var returnMsg = $"ERROR: Failed to list repositories due to exception '{ex.Message}'";
            if (ex.InnerException != null)
            {
                returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
            }
            return returnMsg;
        }
    }
}
