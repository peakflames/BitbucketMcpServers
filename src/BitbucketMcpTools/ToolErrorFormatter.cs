namespace BitbucketMcpTools;

/// <summary>
/// Builds the error text returned to MCP clients. Raw upstream exception text is never returned:
/// Bitbucket errors surface their HTTP status and message, everything else surfaces only the
/// exception type. All output passes through <see cref="Redact"/>.
/// </summary>
public static class ToolErrorFormatter
{
    private static readonly Regex AuthSchemeValue = new(
        @"(bearer|basic)\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PresignedQueryValue = new(
        @"X-Amz-[A-Za-z-]+=[^&\s'""<]+",
        RegexOptions.Compiled);

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var redacted = AuthSchemeValue.Replace(text, "$1 [REDACTED]");
        return PresignedQueryValue.Replace(redacted, "X-Amz-…=[REDACTED]");
    }

    public static string Describe(Exception ex)
    {
        var description = ex is SharpBucket.BitbucketException bitbucketException
            ? $"{bitbucketException.HttpStatusCode} {bitbucketException.Message}"
            : ex.GetType().Name;

        return Redact(description);
    }

    public static string Format(string operation, Exception ex, ILogger? logger = null)
    {
        logger?.LogWarning("Failed to {Operation}: {Detail}", operation, Redact(ex.ToString()));
        return $"ERROR: Failed to {operation}: {Describe(ex)}";
    }
}
