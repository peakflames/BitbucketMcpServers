namespace BitbucketRemoteMcpServer.Credentials;

/// <summary>
/// Resolves which credential <see cref="BitbucketClient.ConnectAsync"/> should authenticate a
/// request's upstream Bitbucket calls with. A non-null result is a bearer access token
/// identifying a specific caller — <see cref="BitbucketClient"/> already prioritizes it over the
/// shared service credential (see its ConnectAsync). A null result means "fall back to this
/// deployment's shared credential". <paramref name="user"/> is the caller's validated
/// <see cref="ClaimsPrincipal"/> when <c>McpAuth:Enabled</c> is true, and null otherwise —
/// <see cref="SharedCredentialResolver"/>, the default, ignores it either way.
/// </summary>
public interface IUpstreamCredentialResolver
{
    Task<Result<string?>> ResolveAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default);
}
