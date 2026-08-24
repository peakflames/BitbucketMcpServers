namespace BitbucketRemoteMcpServer.Access;

/// <summary>
/// Returns Success immediately when Access is disabled, same shape as McpAuthOptionsValidator, so
/// a malformed Access section can never break a server with this feature off.
///
/// Access:Enabled without McpAuth:Enabled hides/denies tools for every caller with no way to
/// distinguish an authenticated caller from an unauthenticated one — that is not the transitional
/// posture this feature exists for, so it is refused at startup rather than silently accepted.
/// </summary>
public sealed class AccessOptionsValidator : IValidateOptions<AccessOptions>
{
    private readonly IOptions<Auth.McpAuthOptions> _mcpAuthOptions;

    public AccessOptionsValidator(IOptions<Auth.McpAuthOptions> mcpAuthOptions)
    {
        _mcpAuthOptions = mcpAuthOptions;
    }

    public ValidateOptionsResult Validate(string? name, AccessOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        if (!_mcpAuthOptions.Value.Enabled)
        {
            failures.Add(
                "Access:Enabled requires McpAuth:Enabled — Access exists to hide/deny tools during " +
                "the transitional window where authentication exists but per-user credential " +
                "passthrough does not. Without authentication it would apply to every caller " +
                "unconditionally. Enable McpAuth first, or leave Access disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.IdentityClaim))
            failures.Add("Access:IdentityClaim must not be blank.");

        if (options.DisabledTools.Any(string.IsNullOrWhiteSpace))
            failures.Add("Access:DisabledTools must not contain blank entries.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
