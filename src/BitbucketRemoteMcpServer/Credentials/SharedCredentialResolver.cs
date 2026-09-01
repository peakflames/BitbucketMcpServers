namespace BitbucketRemoteMcpServer.Credentials;

/// <summary>
/// Default resolver, active whenever <c>Broker:Enabled</c> is false: every caller authenticates
/// upstream with this deployment's single shared Bitbucket credential, exactly today's behavior.
/// Ignores <paramref name="user"/> entirely — there is no per-caller identity to apply.
/// </summary>
public sealed class SharedCredentialResolver : IUpstreamCredentialResolver
{
    public Task<Result<string?>> ResolveAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok<string?>(null));
}
