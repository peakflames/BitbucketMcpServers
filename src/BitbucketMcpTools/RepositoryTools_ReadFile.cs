namespace BitbucketMcpTools;

public partial class RepositoryTools
{
    private const int DefaultMaxFileSizeBytes = 100 * 1024;

    [McpServerTool(Name = "read_file"),
        Description(
            "Reads the raw contents of a file in the Bitbucket repository at a given revision. " +
            "Output is truncated at a configurable size cap (default 100 KB) with a truncation notice appended. " +
            "Read-only."
        )]
    public async Task<string> ReadFile(
        [Description("The name of the Bitbucket repository to query.")]
        string repoName,

        [Description("The path to the file, relative to the repository root.")]
        string filePath,

        [Description("The branch name, tag name, or commit hash to read the file from. Defaults to the latest commit on the main branch.")]
        string? @ref = null,

        [Description("Maximum number of bytes of file content to return before truncating. Default is 102400 (100 KB).")]
        int maxSizeBytes = DefaultMaxFileSizeBytes)
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
            // SrcResource resolves the main branch revision synchronously when revision is null,
            // so resolve it explicitly first to keep this call fully async.
            var revision = @ref;
            if (string.IsNullOrWhiteSpace(revision))
            {
                revision = await bitBucketClient.RepositoryResource.GetMainBranchRevisionAsync();
            }

            var srcResource = bitBucketClient.RepositoryResource.SrcResource(revision, null);
            var content = await srcResource.GetFileContentAsync(filePath);

            if (content is null)
            {
                return $"ERROR: File '{filePath}' not found in {bitBucketClient.RepositoryFullName} at revision '{revision}'.";
            }

            var contentByteCount = Encoding.UTF8.GetByteCount(content);
            var markdownContents = new StringBuilder();

            markdownContents.AppendLine($"# {filePath}");
            markdownContents.AppendLine();
            markdownContents.AppendLine($"**Repository**: {bitBucketClient.RepositoryFullName} | **Revision**: {revision} | **Size**: {contentByteCount} bytes");
            markdownContents.AppendLine();
            markdownContents.AppendLine("```");

            if (maxSizeBytes > 0 && contentByteCount > maxSizeBytes)
            {
                markdownContents.AppendLine(TruncateUtf8(content, maxSizeBytes));
                markdownContents.AppendLine("```");
                markdownContents.AppendLine();
                markdownContents.AppendLine($"*[TRUNCATED: file is {contentByteCount} bytes; showing first {maxSizeBytes} bytes. Increase maxSizeBytes to see more.]*");
            }
            else
            {
                markdownContents.AppendLine(content);
                markdownContents.AppendLine("```");
            }

            return markdownContents.ToString();
        }
        catch (Exception ex)
        {
            var returnMsg = $"ERROR: Failed to read file due to exception '{ex.Message}'";
            if (ex.InnerException != null)
            {
                returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
            }
            return returnMsg;
        }
    }

    /// <summary>
    /// Truncates a string to at most <paramref name="maxBytes"/> UTF-8 bytes, backing off to the
    /// nearest character boundary so the result never ends on a partial multi-byte sequence.
    /// </summary>
    private static string TruncateUtf8(string content, int maxBytes)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        if (bytes.Length <= maxBytes)
        {
            return content;
        }

        var length = maxBytes;
        while (length > 0 && (bytes[length] & 0xC0) == 0x80)
        {
            length--;
        }

        return Encoding.UTF8.GetString(bytes, 0, length);
    }
}
