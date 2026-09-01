using System.Security.Claims;

namespace BitbucketRemoteMcpServer.Auth;

/// <summary>
/// Our own token issuer always emits space-delimited "scope" (RFC 9068 / RFC 6749 §3.3), but
/// JwtBearer's claim mapping and some clients emit "scp" as a JSON array — handle both so the
/// resource-server gate isn't quietly bypassed by a claim-shape mismatch.
/// </summary>
public static class ScopeClaimHelper
{
    private const string ScopeClaimType = "scope";
    private const string ScpClaimType = "scp";

    public static bool HasScope(ClaimsPrincipal user, string requiredScope)
    {
        foreach (var scope in GetScopes(user))
        {
            if (string.Equals(scope, requiredScope, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static IEnumerable<string> GetScopes(ClaimsPrincipal user)
    {
        foreach (var claim in user.FindAll(ScopeClaimType))
        {
            foreach (var token in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                yield return token;
        }

        foreach (var claim in user.FindAll(ScpClaimType))
        {
            foreach (var token in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                yield return token;
        }
    }
}
