namespace BitbucketMcpTools;

public partial class PullRequestTools
{  
    // [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_pull_request_comments"), 
        Description(
            "Gets all comments for a specific pull request in the Bitbucket repository. " +
            "Results is a Markdwon document mixed with XML tags."
         )]
    public async Task<string> GetPullRequestComments(
        [Description("The name of the Bitbucket repository to query.")]
        string repoName,

        [Description("The Pull Request Id.")]
        int pullRequestId,

        [Description("Whether to include inline comments or not. Default is false.")]
        bool includeInlineComments = false)
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
                // /repositories/{workspace}/{repo_slug}/pullrequests/{pull_request_id}/comments
                var pullRequestsComments = bitBucketClient.RepositoryResource
                                            .PullRequestsResource()
                                            .PullRequestResource(pullRequestId)
                                            .CommentsResource;

                
                var markdownContents = new StringBuilder();


                markdownContents.AppendLine($"# Comments for Pull Request {pullRequestId}");
                markdownContents.AppendLine();

                await foreach (var comment in pullRequestsComments.EnumerateCommentsAsync())
                {
                    if (comment is null)
                    {
                        continue;
                    }

                    if (!includeInlineComments && comment.inline is not null)
                    {
                        // Skip inline comments if not requested
                        continue;
                    }

                    // <COMMENT user='Andy Collins' created_on='2025-09-11T12:00:28.813770+00:00' updated_on='2025-09-25T20:22:31.750502+00:00' type='general'>

                    // TODO: make the date format more readable
                    

                    var createdOn = comment.created_on != null 
                        ? DateTime.TryParse(comment.created_on, out var createdDate) 
                            ? createdDate.ToString("dd-MMM-yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture).ToUpper()
                            : comment.created_on
                        : "N/A";
                    var updatedOn = comment.updated_on != null 
                        ? DateTime.TryParse(comment.updated_on, out var updatedDate) 
                            ? updatedDate.ToString("dd-MMM-yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture).ToUpper()
                            : comment.updated_on
                        : "N/A";

                    if (comment.inline is not null)
                    {
                        markdownContents.AppendLine($"<COMMENT user='{comment.user?.display_name}' created_on='{createdOn}' updated_on='{updatedOn}' type='inline' inline_from='{comment.inline?.from}' inline_to='{comment.inline?.to}' inline_path='{comment.inline?.path}'>");
                    }
                    else
                    {
                        markdownContents.AppendLine($"<COMMENT user='{comment.user?.display_name}' created_on='{createdOn}' updated_on='{updatedOn}' type='general'>");
                    }

                    comment.content.raw?.Split('\n').ToList().ForEach(line =>
                    {
                        markdownContents.AppendLine(line);
                    });
                    
                    markdownContents.AppendLine("</COMMENT>");
                    markdownContents.AppendLine();
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
