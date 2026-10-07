namespace BitbucketMcpTools;

public partial class RepositoryTools
{
    [McpServerTool(Name = "list_directory"),
        Description(
            "Lists the files and directories in a repository path at a given revision. " +
            "This is a shallow listing only - it does not recurse into subdirectories. " +
            "Results is a Markdown table containing the following columns: Name, Type, Size. " +
            "Read-only."
        )]
    public async Task<string> ListDirectory(
        [Description("The name of the Bitbucket repository to query.")]
        string repoName,

        [Description("The path to the directory, relative to the repository root. Defaults to the repository root.")]
        string? path = null,

        [Description("The branch name, tag name, or commit hash to browse. Defaults to the latest commit on the main branch.")]
        string? @ref = null)
    {
        var pathResult = RepoPathValidator.ValidateAndEscapePath(path, allowEmpty: true);
        if (pathResult.IsFailed)
        {
            return $"ERROR: Invalid path '{path}': {pathResult.Errors.First().Message}";
        }

        var refResult = RepoPathValidator.ValidateRef(@ref);
        if (refResult.IsFailed)
        {
            return $"ERROR: Invalid ref '{@ref}': {refResult.Errors.First().Message}";
        }

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
            // SrcResource resolves the main branch revision synchronously when revision is null,
            // so resolve it explicitly first to keep this call fully async.
            var revision = @ref;
            if (string.IsNullOrWhiteSpace(revision))
            {
                revision = await bitBucketClient.RepositoryResource.GetMainBranchRevisionAsync();
            }

            var srcResource = bitBucketClient.RepositoryResource.SrcResource(revision, null);
            var entries = srcResource.ListSrcEntries(string.IsNullOrEmpty(pathResult.Value) ? null : pathResult.Value);

            var markdownContents = new StringBuilder();
            var displayPath = string.IsNullOrWhiteSpace(path) ? "/" : path;

            markdownContents.AppendLine($"# Directory Listing: {displayPath}");
            markdownContents.AppendLine();
            markdownContents.AppendLine($"**Repository**: {bitBucketClient.RepositoryFullName} | **Revision**: {revision}");
            markdownContents.AppendLine();

            if (entries is null || entries.Count == 0)
            {
                markdownContents.AppendLine("*No entries found at this path.*");
                return markdownContents.ToString();
            }

            markdownContents.AppendLine("| Name | Type | Size (bytes) |");
            markdownContents.AppendLine("|------|------|------|");

            foreach (var entry in entries)
            {
                var type = entry.IsDirectory ? "directory" : "file";
                var size = entry.IsFile ? entry.SrcFile!.size.ToString() : "";
                markdownContents.AppendLine($"| {entry.path} | {type} | {size} |");
            }

            markdownContents.AppendLine();
            markdownContents.AppendLine($"**Total Entries**: {entries.Count}");

            return markdownContents.ToString();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetService<ILogger<RepositoryTools>>();
            return ToolErrorFormatter.Format("list directory", ex, logger);
        }
    }
}
