namespace BitbucketMcpTools;

public partial class RepositoryTools
{
    private static readonly Regex RepoSlugFromHrefPattern = new(@"/repositories/[^/]+/([^/]+)/src/", RegexOptions.Compiled);

    [McpServerTool(Name = "search_code"),
        Description(
            "Searches code content across all repositories in the Bitbucket workspace. " +
            "Narrow the search to a single repository by including 'repo:<slug>' in the query, e.g. 'foo repo:my-repo'. " +
            "Results is a Markdown list of matches grouped by repository and file, with matching line numbers and content. " +
            "Read-only."
        )]
    public async Task<string> SearchCode(
        [Description("The name of a Bitbucket repository used only to establish workspace access/credentials. " +
            "The search itself spans the whole workspace unless narrowed with 'repo:<slug>' in the query.")]
        string repoName,

        [Description("The search query string. Supports Bitbucket code search syntax, including the 'repo:<slug>' qualifier to narrow scope.")]
        string query,

        [Description("The maximum number of results to return. 0 returns all matching results. Default is 20.")]
        int maxResults = 20)
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
            var searchCodeResource = bitBucketClient.SharpBucket
                .WorkspacesEndPoint()
                .WorkspaceResource(bitBucketClient.AccountName)
                .SearchCodeResource;

            var results = searchCodeResource.ListSearchResults(query, maxResults);

            if (results is null || results.Count == 0)
            {
                return $"No code search results found for query '{query}' in workspace '{bitBucketClient.AccountName}'.";
            }

            var markdownContents = new StringBuilder();
            markdownContents.AppendLine("# Code Search Results");
            markdownContents.AppendLine();
            markdownContents.AppendLine($"**Query**: `{query}` | **Total Matches**: {results.Count}");
            markdownContents.AppendLine();

            foreach (var result in results)
            {
                var filePath = result.file?.path ?? "(unknown path)";
                var repoSlug = ExtractRepoSlug(result.file?.links?.self?.href) ?? bitBucketClient.AccountName;

                markdownContents.AppendLine($"## {repoSlug}/{filePath}");
                markdownContents.AppendLine();

                foreach (var contentMatch in result.content_matches ?? [])
                {
                    foreach (var line in contentMatch.lines ?? [])
                    {
                        var lineText = string.Concat((line.segments ?? []).Select(s => s.text));
                        markdownContents.AppendLine($"- L{line.line}: `{lineText}`");
                    }
                }

                markdownContents.AppendLine();
            }

            return markdownContents.ToString();
        }
        catch (Exception ex)
        {
            var returnMsg = $"ERROR: Failed to search code due to exception '{ex.Message}'";
            if (ex.InnerException != null)
            {
                returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
            }
            return returnMsg;
        }
    }

    /// <summary>
    /// The Bitbucket code search response doesn't include the repository name directly,
    /// so it's recovered from the file's "self" link (.../repositories/{workspace}/{repo_slug}/src/...).
    /// </summary>
    private static string? ExtractRepoSlug(string? href)
    {
        if (string.IsNullOrEmpty(href))
        {
            return null;
        }

        var match = RepoSlugFromHrefPattern.Match(href);
        return match.Success ? match.Groups[1].Value : null;
    }
}
