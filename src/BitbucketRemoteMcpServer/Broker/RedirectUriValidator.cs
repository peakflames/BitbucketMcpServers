namespace BitbucketRemoteMcpServer.Broker;

/// <summary>
/// Ported from FastMCP's <c>redirect_validation.py</c>: an exact match, or — per RFC 8252 §7.3 —
/// a loopback host with any port, since a native app's loopback redirect legitimately picks an
/// ephemeral port at runtime that registration cannot pin in advance.
///
/// Comparing on <see cref="Uri.Host"/> rather than the raw string already defeats the
/// <c>http://localhost@evil.com/callback</c> userinfo trick: <c>Uri</c> parses that host as
/// <c>evil.com</c>, with <c>localhost</c> relegated to <see cref="Uri.UserInfo"/>, which this
/// comparison never reads.
/// </summary>
public static class RedirectUriValidator
{
    public static bool IsValid(string candidate, IReadOnlyList<string> registeredRedirectUris)
    {
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var candidateUri))
            return false;

        foreach (var raw in registeredRedirectUris)
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var registeredUri))
                continue;

            if (ExactMatch(candidateUri, registeredUri))
                return true;

            if (IsLoopback(registeredUri) && IsLoopback(candidateUri) && MatchesIgnoringPort(candidateUri, registeredUri))
                return true;
        }

        return false;
    }

    private static bool ExactMatch(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
        && a.Port == b.Port
        && MatchesIgnoringPort(a, b);

    private static bool MatchesIgnoringPort(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.AbsolutePath, b.AbsolutePath, StringComparison.Ordinal)
        && string.Equals(a.Query, b.Query, StringComparison.Ordinal);

    private static bool IsLoopback(Uri uri) =>
        string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal)
        || string.Equals(uri.Host, "::1", StringComparison.Ordinal)
        || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
}
