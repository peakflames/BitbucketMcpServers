namespace BitbucketRemoteMcpServer.Auth;

public static class OAuthScopes
{
    public const string Read = "bitbucket:read";

    /// <summary>Authorization policy name gating POST /mcp.</summary>
    public const string ReadPolicy = "BitbucketRead";
}
