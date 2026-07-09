namespace BitbucketMcpTools;

public partial class PullRequestTools
{
    [McpServerTool(Name = "list_pull_requests"),
        Description(
            "Gets pull requests in the Bitbucket repository filtered by state, newest-updated first by default. " +
            "Unlike list_pull_open_requests, this tool can also retrieve Merged, Declined, and Superseded pull requests. " +
            "Supports optional server-side filters (updatedSince, author, destinationBranch) so callers can pull recent or " +
            "relevant pull requests directly from Bitbucket without over-fetching, which matters on repositories with " +
            "thousands of pull requests. " +
            "Results is a Markdown table containing the following columns: ID, Title, Author, State, Draft, Created, Updated."
         )]
    public async Task<string> ListPullRequests(
        [Description("The name of the Bitbucket repository to query.")]
        string repoName,

        [Description("The pull request state(s) to filter by: Open, Merged, Declined, Superseded, or All. " +
            "Multiple states can be combined as a comma-separated list, e.g. 'Merged,Declined'. Default is Open.")]
        string state = "Open",

        [Description("The maximum number of pull requests to return. 0 returns all matching results. Default is 50.")]
        int maxResults = 50,

        [Description("Sort order: 'newest' (most recently updated first, default), 'oldest' (lowest ID first), " +
            "or 'recently-created' (most recently created first). A raw Bitbucket sort field (e.g. '-created_on') " +
            "is also accepted for advanced use.")]
        string sort = "newest",

        [Description("Only return pull requests updated on or after this date/time, e.g. '2025-01-01' or '2025-01-01T00:00:00Z'. Omit for no date filter.")]
        string? updatedSince = null,

        [Description("Only return pull requests authored by this user (Bitbucket account nickname). Omit for no author filter.")]
        string? author = null,

        [Description("Only return pull requests targeting this destination branch, e.g. 'main'. Omit for no branch filter.")]
        string? destinationBranch = null)
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
                var sortField = MapSort(sort);
                var filter = BuildPullRequestFilter(updatedSince, author, destinationBranch);

                var parameters = new SharpBucket.V2.EndPoints.ListPullRequestsParameters
                {
                    Sort = sortField,
                    States = states,
                    Max = maxResults,
                    Filter = filter,
                };
                var pullRequestsResource = bitBucketClient.RepositoryResource.PullRequestsResource();
                List<PullRequest> pullRequests = pullRequestsResource.ListPullRequests(parameters);

                if (pullRequests is null || pullRequests.Count == 0)
                {
                    return $"No pull requests found for {bitBucketClient.RepositoryFullName} matching state(s) '{state}' or unable to retrieve them.";
                }

                var stateQuery = string.Join("&", states.Select(s => $"state={s.ToString().ToUpperInvariant()}"));
                var draftFlags = await GetDraftFlagsAsync(bitBucketClient, stateQuery, sortField, maxResults, filter);

                var markdownContents = new StringBuilder();

                markdownContents.AppendLine("# Pull Requests");
                markdownContents.AppendLine();
                markdownContents.AppendLine($"**Total Count**: {pullRequests.Count}");
                markdownContents.AppendLine();
                markdownContents.AppendLine($"| ID | Title | Author | State | Draft | Created | Updated |");
                markdownContents.AppendLine($"| ---   | ---   | ---  | ------ | ------ | ------ | ------ |");

                foreach (var pr in pullRequests)
                {
                    var draftText = pr.id is int id && draftFlags.TryGetValue(id, out var isDraft) ? (isDraft ? "Yes" : "No") : "?";
                    markdownContents.AppendLine($"| {pr.id} | {pr.title} | {pr.author?.display_name} | {pr.state} | {draftText} | {pr.created_on} | {pr.updated_on} |");
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

    /// <summary>
    /// Maps a friendly sort keyword to the Bitbucket sort field. A leading "-" means descending.
    /// Unrecognized values are passed through unchanged so power users can supply a raw field.
    /// </summary>
    private static string MapSort(string sort)
    {
        return sort switch
        {
            "newest" => "-updated_on",
            "oldest" => "id",
            "recently-created" => "-created_on",
            _ => sort,
        };
    }

    /// <summary>
    /// Builds a Bitbucket "q" query filter string by AND-ing together whichever of the
    /// optional clauses are present. Returns null when no clause applies.
    /// </summary>
    private static string? BuildPullRequestFilter(string? updatedSince, string? author, string? destinationBranch)
    {
        var clauses = new List<string>();

        if (!string.IsNullOrWhiteSpace(updatedSince))
        {
            clauses.Add($"updated_on >= {updatedSince.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            clauses.Add($"author.nickname = \"{author.Trim()}\"");
        }

        if (!string.IsNullOrWhiteSpace(destinationBranch))
        {
            clauses.Add($"destination.branch.name = \"{destinationBranch.Trim()}\"");
        }

        return clauses.Count > 0 ? string.Join(" AND ", clauses) : null;
    }
}
