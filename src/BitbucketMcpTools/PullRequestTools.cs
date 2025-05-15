
namespace BitbucketMcpTools;

public sealed partial class PullRequestTools
{
    private readonly IServiceProvider _serviceProvider;

    public PullRequestTools(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "list_pull_requests"), 
        Description(
            "Gets all the open and merged pull requests in the Bitbucket repository. " +
            "Results is a Markdwon table containing the documents with the following columns: ID, Title, Author, State, Created, Updated."
         )]
    public async Task<string> ListPullRequests()
    {
        string? returnMsg;
        
        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IBitbucketClientFactory>();
            var clientResult = await clientFactory.CreateClientAsync();
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
                    Sort = "id",
                    States = [PullRequestState.Open, PullRequestState.Merged]
                };
                var pullRequestsResource = bitBucketClient.RepositoryResource.PullRequestsResource();
                List<PullRequest> pullRequests = pullRequestsResource.ListPullRequests(parameters);

                if (pullRequests is null || pullRequests.Count == 0)
                {
                    return $"No pull requests found for {bitBucketClient.RepositoryFullName} or unable to retrieve them.";
                }

                var markdownContents = new StringBuilder();

                
                markdownContents.AppendLine("# Pull Requests");
                markdownContents.AppendLine();
                markdownContents.AppendLine($"| ID | Title | Author | State | Created | Updated |");
                markdownContents.AppendLine($"| ---   | ---   | ---  | ------ |");

                foreach (var pr in pullRequests)
                {
                    markdownContents.AppendLine($"| {pr.id} | {pr.title} | {pr.author?.display_name} | {pr.state} | {pr.created_on} | {pr.updated_on} |");
                }

                markdownContents.AppendLine();
                return markdownContents.ToString();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to get Polarion Document due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        }
    }

}
