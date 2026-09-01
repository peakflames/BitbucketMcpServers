
namespace BitbucketMcpTools;

public partial class PullRequestTools
{
    [McpServerTool(Name = "list_pull_open_requests"),
        Description(
            "Gets all open pull requests in the Bitbucket repository. " +
            "Results is a Markdown table containing the documents with the following columns: ID, Title, Author, State, Draft, Created, Updated."
         )]
    public async Task<string> ListOpenPullRequests(
        [Description("The name of the Bitbucket repository to query.")]
        string repoName)
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
                var parameters = new SharpBucket.V2.EndPoints.ListPullRequestsParameters
                {
                    Sort = "-updated_on",
                    States = [PullRequestState.Open],
                };
                var pullRequestsResource = bitBucketClient.RepositoryResource.PullRequestsResource();
                List<PullRequest> pullRequests = pullRequestsResource.ListPullRequests(parameters);

                if (pullRequests is null || pullRequests.Count == 0)
                {
                    return $"No pull requests found for {bitBucketClient.RepositoryFullName} or unable to retrieve them.";
                }

                var draftFlags = await GetDraftFlagsAsync(bitBucketClient, "state=OPEN");

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
}
