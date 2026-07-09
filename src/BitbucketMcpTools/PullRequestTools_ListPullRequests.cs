namespace BitbucketMcpTools;

public partial class PullRequestTools
{
    [McpServerTool(Name = "list_pull_requests"),
        Description(
            "Gets pull requests in the Bitbucket repository filtered by state. " +
            "Unlike list_pull_open_requests, this tool can also retrieve Merged, Declined, and Superseded pull requests. " +
            "Results is a Markdown table containing the following columns: ID, Title, Author, State, Created, Updated."
         )]
    public async Task<string> ListPullRequests(
        [Description("The name of the Bitbucket repository to query.")]
        string repoName,

        [Description("The pull request state(s) to filter by: Open, Merged, Declined, Superseded, or All. " +
            "Multiple states can be combined as a comma-separated list, e.g. 'Merged,Declined'. Default is Open.")]
        string state = "Open",

        [Description("The maximum number of pull requests to return. 0 returns all matching results. Default is 50.")]
        int maxResults = 50)
    {
        string? returnMsg;

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IBitbucketClientFactory>();
            var clientResult = await clientFactory.CreateClientAsync(repoName);
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.First().ToString() ?? "Internal Error (35864) unknown error when creating Bitbucket client";
            }

            var bitBucketClient = clientResult.Value;

            if (bitBucketClient is null)
            {
                return "Internal Error (35865) unknown error when creating Bitbucket client";
            }

            if (bitBucketClient.RepositoryResource is null)
            {
                return "Internal Error (35866) unknown error when creating Bitbucket client";
            }

            try
            {
                var states = ParsePullRequestStates(state);

                var parameters = new SharpBucket.V2.EndPoints.ListPullRequestsParameters
                {
                    Sort = "id",
                    States = states,
                    Max = maxResults,
                };
                var pullRequestsResource = bitBucketClient.RepositoryResource.PullRequestsResource();
                List<PullRequest> pullRequests = pullRequestsResource.ListPullRequests(parameters);

                if (pullRequests is null || pullRequests.Count == 0)
                {
                    return $"No pull requests found for {bitBucketClient.RepositoryFullName} matching state(s) '{state}' or unable to retrieve them.";
                }

                var markdownContents = new StringBuilder();

                markdownContents.AppendLine("# Pull Requests");
                markdownContents.AppendLine();
                markdownContents.AppendLine($"**Total Count**: {pullRequests.Count}");
                markdownContents.AppendLine();
                markdownContents.AppendLine($"| ID | Title | Author | State | Created | Updated |");
                markdownContents.AppendLine($"| ---   | ---   | ---  | ------ | ------ | ------ |");

                foreach (var pr in pullRequests)
                {
                    markdownContents.AppendLine($"| {pr.id} | {pr.title} | {pr.author?.display_name} | {pr.state} | {pr.created_on} | {pr.updated_on} |");
                }

                markdownContents.AppendLine();
                return markdownContents.ToString();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to get Bitbucket info due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        }
    }

    /// <summary>
    /// Parses a comma-separated list of pull request state names (or "All") into the set of
    /// <see cref="PullRequestState"/> values to filter on.
    /// </summary>
    private static IReadOnlyCollection<PullRequestState> ParsePullRequestStates(string? state)
    {
        if (string.IsNullOrWhiteSpace(state) || string.Equals(state.Trim(), "All", StringComparison.OrdinalIgnoreCase))
        {
            return [PullRequestState.Open, PullRequestState.Merged, PullRequestState.Declined, PullRequestState.Superseded];
        }

        var parsedStates = new List<PullRequestState>();
        foreach (var part in state.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0 && Enum.TryParse<PullRequestState>(trimmed, ignoreCase: true, out var parsed))
            {
                parsedStates.Add(parsed);
            }
        }

        return parsedStates.Count > 0 ? parsedStates : [PullRequestState.Open];
    }
}
