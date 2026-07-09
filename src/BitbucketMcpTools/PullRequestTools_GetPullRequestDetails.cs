namespace BitbucketMcpTools;

public partial class PullRequestTools
{
    /// <summary>
    /// Gets detailed information about a pull request including description and changed files.
    /// </summary>
    [McpServerTool(Name = "get_pull_request_details"),
        Description(
            "Gets detailed information about a specific pull request in the Bitbucket repository, " +
            "including the PR description, metadata, and optionally the list of changed files. " +
            "Works for a pull request in any state (Open, Merged, Declined, or Superseded) as long as " +
            "the pull request ID is known. " +
            "Results is a Markdown document mixed with XML tags."
        )]
    public async Task<string> GetPullRequestDetails(
        [Description("The name of the Bitbucket repository to query.")]
        string repoName,

        [Description("The Pull Request Id.")]
        int pullRequestId,

        [Description("Whether to include the list of changed files. Default is true.")]
        bool includeChangedFiles = true)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
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
            var pullRequestResource = bitBucketClient.RepositoryResource
                .PullRequestsResource()
                .PullRequestResource(pullRequestId);

            var pullRequest = await pullRequestResource.GetPullRequestAsync();

            if (pullRequest is null)
            {
                return $"ERROR: Pull request {pullRequestId} not found in repository {repoName}";
            }

            var markdownContents = new StringBuilder();

            // Format dates
            var createdOn = FormatDateTime(pullRequest.created_on);
            var updatedOn = FormatDateTime(pullRequest.updated_on);

            // Build PR metadata section
            markdownContents.AppendLine("# Pull Request Details");
            markdownContents.AppendLine();
            markdownContents.AppendLine($"<PR_METADATA id='{pullRequest.id}' title='{EscapeXmlAttribute(pullRequest.title)}' state='{pullRequest.state}' author='{EscapeXmlAttribute(pullRequest.author?.display_name)}' source_branch='{EscapeXmlAttribute(pullRequest.source?.branch?.name)}' destination_branch='{EscapeXmlAttribute(pullRequest.destination?.branch?.name)}' created_on='{createdOn}' updated_on='{updatedOn}'>");
            markdownContents.AppendLine();
            markdownContents.AppendLine("## Description");
            markdownContents.AppendLine();

            if (!string.IsNullOrWhiteSpace(pullRequest.description))
            {
                foreach (var line in pullRequest.description.Split('\n'))
                {
                    markdownContents.AppendLine(line);
                }
            }
            else
            {
                markdownContents.AppendLine("*No description provided.*");
            }

            markdownContents.AppendLine();
            markdownContents.AppendLine("</PR_METADATA>");
            markdownContents.AppendLine();

            // Include changed files if requested
            if (includeChangedFiles)
            {
                var changedFiles = await GetChangedFilesFromPullRequestAsync(pullRequest, pullRequestResource);

                markdownContents.AppendLine("## Changed Files");
                markdownContents.AppendLine();

                if (changedFiles.Count > 0)
                {
                    markdownContents.AppendLine("| Status | File Path | Lines Added | Lines Removed |");
                    markdownContents.AppendLine("|--------|-----------|-------------|---------------|");

                    foreach (var file in changedFiles)
                    {
                        markdownContents.AppendLine($"| {file.Status} | {file.Path} | {file.LinesAdded} | {file.LinesRemoved} |");
                    }

                    markdownContents.AppendLine();
                    markdownContents.AppendLine($"**Total Changed Files**: {changedFiles.Count}");
                }
                else
                {
                    markdownContents.AppendLine("*No changed files found or unable to retrieve them.*");
                }

                markdownContents.AppendLine();
            }

            return markdownContents.ToString();
        }
        catch (Exception ex)
        {
            var returnMsg = $"ERROR: Failed to get pull request details due to exception '{ex.Message}'";
            if (ex.InnerException != null)
            {
                returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
            }
            return returnMsg;
        }
    }

    /// <summary>
    /// Gets the list of changed files from a pull request using the diffstat endpoint.
    /// </summary>
    private async Task<List<ChangedFileInfo>> GetChangedFilesFromPullRequestAsync(
        PullRequest pullRequest,
        PullRequestResource pullRequestResource)
    {
        var changedFiles = new List<ChangedFileInfo>();

        try
        {
            // Get the diffstat URL from the pull request links
            var diffstatUrl = pullRequest.links?.diffstat?.href;

            if (string.IsNullOrEmpty(diffstatUrl))
            {
                return changedFiles;
            }

            // Access SharpBucket's requester through reflection to use existing authentication
            var sharpBucketProperty = typeof(PullRequestResource).BaseType?
                .GetProperty("SharpBucketV2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (sharpBucketProperty?.GetValue(pullRequestResource) is not ISharpBucketRequesterV2 requester)
            {
                return changedFiles;
            }

            // Fetch all pages of diffstat data
            var currentUrl = diffstatUrl;

            while (!string.IsNullOrEmpty(currentUrl))
            {
                var jsonContent = await requester.SendAsync(
                    HttpMethod.Get,
                    body: null,
                    relativeUrl: currentUrl,
                    requestParameters: null,
                    token: CancellationToken.None);

                var diffstatResponse = JsonSerializer.Deserialize(jsonContent, DiffstatJsonContext.Default.DiffstatResponse);

                if (diffstatResponse?.Values is null)
                {
                    break;
                }

                // Extract file information from each entry
                foreach (var entry in diffstatResponse.Values)
                {
                    var filePath = string.Empty;

                    // Determine file path based on status
                    if (entry.Status == "removed" && entry.Old?.Path != null)
                    {
                        filePath = entry.Old.Path;
                    }
                    else if (entry.New?.Path != null)
                    {
                        filePath = entry.New.Path;
                    }

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        changedFiles.Add(new ChangedFileInfo
                        {
                            Path = filePath,
                            Status = entry.Status ?? "unknown",
                            LinesAdded = entry.LinesAdded ?? 0,
                            LinesRemoved = entry.LinesRemoved ?? 0
                        });
                    }
                }

                // Move to next page if available
                currentUrl = diffstatResponse.Next ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the entire operation
            System.Diagnostics.Debug.WriteLine($"Error fetching changed files from diffstat: {ex.Message}");
        }

        return changedFiles;
    }

    /// <summary>
    /// Formats a DateTime to a consistent string format.
    /// </summary>
    private static string FormatDateTime(DateTime? dateTime)
    {
        if (!dateTime.HasValue)
        {
            return "N/A";
        }

        return dateTime.Value.ToString("dd-MMM-yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture).ToUpper();
    }

    /// <summary>
    /// Escapes special characters for XML attribute values.
    /// </summary>
    private static string EscapeXmlAttribute(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;")
            .Replace("'", "&apos;")
            .Replace("\"", "&quot;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}

/// <summary>
/// Represents information about a changed file in a pull request.
/// </summary>
public class ChangedFileInfo
{
    /// <summary>
    /// The file path relative to the repository root.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The status of the change (e.g., "added", "modified", "removed", "renamed").
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The number of lines added in this file.
    /// </summary>
    public int LinesAdded { get; set; }

    /// <summary>
    /// The number of lines removed in this file.
    /// </summary>
    public int LinesRemoved { get; set; }
}
