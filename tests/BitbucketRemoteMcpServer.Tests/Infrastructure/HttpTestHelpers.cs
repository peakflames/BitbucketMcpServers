namespace BitbucketRemoteMcpServer.Tests.Infrastructure;

/// <summary>Small parsing helpers the broker endpoint tests need repeatedly: pulling a cookie
/// value back out of a raw Set-Cookie header (the TestServer client has no cookie container of
/// its own — see BitbucketMcpServerFactory), and reading query parameters off a redirect
/// Location.</summary>
internal static class HttpTestHelpers
{
    public static string? ExtractSetCookie(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return null;

        foreach (var value in values)
        {
            var firstSegment = value.Split(';')[0];
            var equalsIndex = firstSegment.IndexOf('=');
            if (equalsIndex < 0)
                continue;

            var name = firstSegment[..equalsIndex];
            if (string.Equals(name, cookieName, StringComparison.Ordinal))
                return firstSegment[(equalsIndex + 1)..];
        }

        return null;
    }

    public static Dictionary<string, string> ParseQuery(string uriOrQuery)
    {
        var query = uriOrQuery.Contains('?') ? uriOrQuery[(uriOrQuery.IndexOf('?') + 1)..] : uriOrQuery;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            result[Uri.UnescapeDataString(parts[0])] = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        return result;
    }
}
