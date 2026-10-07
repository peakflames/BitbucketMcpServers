namespace BitbucketMcpTools;

/// <summary>
/// Validates caller-supplied repository paths and refs before they are placed into a Bitbucket
/// request URL, so a value can only address content inside the configured repository.
/// </summary>
public static class RepoPathValidator
{
    // %2e ('.'), %2f ('/'), %5c ('\') in any case, optionally wrapped in extra %25 layers (double encoding).
    private static readonly Regex EncodedSeparatorOrDot = new(
        @"%(25)*(2e|2f|5c)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Validates a repository-relative file or directory path and returns it with each segment
    /// escaped exactly once, joined with '/'.
    /// </summary>
    public static Result<string> ValidateAndEscapePath(string? path, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return allowEmpty ? Result.Ok(string.Empty) : Result.Fail("path must not be empty");
        }

        var rejection = FindRejection(path);
        if (rejection is not null)
        {
            return Result.Fail(rejection);
        }

        if (path.StartsWith('/'))
        {
            return Result.Fail("path must be relative to the repository root");
        }

        var trimmed = path.EndsWith('/') ? path[..^1] : path;
        var segments = trimmed.Split('/');
        if (segments.Any(segment => segment.Length == 0))
        {
            return Result.Fail("path must not contain empty segments");
        }

        if (segments.Any(segment => segment is "." or ".."))
        {
            return Result.Fail("path must not contain '.' or '..' segments");
        }

        return Result.Ok(string.Join('/', segments.Select(Uri.EscapeDataString)));
    }

    /// <summary>
    /// Validates a branch, tag, or commit reference. Applies the same rejections as
    /// <see cref="ValidateAndEscapePath"/> but allows '/' (branch names such as feature/x) and
    /// performs no escaping.
    /// </summary>
    public static Result ValidateRef(string? @ref)
    {
        if (string.IsNullOrWhiteSpace(@ref))
        {
            return Result.Ok();
        }

        var rejection = FindRejection(@ref);
        if (rejection is not null)
        {
            return Result.Fail(rejection);
        }

        if (@ref.Split('/').Any(segment => segment is "." or ".."))
        {
            return Result.Fail("ref must not contain '.' or '..' segments");
        }

        return Result.Ok();
    }

    private static string? FindRejection(string value)
    {
        if (value.Any(char.IsControl))
        {
            return "control characters are not allowed";
        }

        if (value.IndexOfAny(['\\', '?', '#']) >= 0)
        {
            return "'\\', '?', and '#' are not allowed";
        }

        if (EncodedSeparatorOrDot.IsMatch(value))
        {
            return "percent-encoded '.', '/', and '\\' are not allowed";
        }

        return null;
    }
}
