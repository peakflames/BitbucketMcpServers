namespace BitbucketRemoteMcpServer.Broker;

/// <summary>
/// Returns Success immediately when Broker is disabled, same shape as McpAuthOptionsValidator and
/// AccessOptionsValidator, so a malformed Broker section can never break a server with this
/// feature off.
/// </summary>
public sealed class BrokerOptionsValidator : IValidateOptions<BrokerOptions>
{
    private readonly IHostEnvironment _environment;

    public BrokerOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, BrokerOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();
        var isDevelopment = _environment.IsDevelopment();

        if (string.IsNullOrWhiteSpace(options.DatabasePath))
            failures.Add("Broker:DatabasePath must not be blank.");

        ValidateIssuerUri(options, isDevelopment, failures);
        ValidateAbsoluteHttpsUri(options.UpstreamAuthorizeUrl, "UpstreamAuthorizeUrl", failures);
        ValidateAbsoluteHttpsUri(options.UpstreamTokenUrl, "UpstreamTokenUrl", failures);
        ValidateAbsoluteHttpsUri(options.UpstreamUserInfoUrl, "UpstreamUserInfoUrl", failures);

        if (string.IsNullOrWhiteSpace(options.UpstreamClientId))
            failures.Add("Broker:UpstreamClientId must not be blank.");
        if (string.IsNullOrWhiteSpace(options.UpstreamClientSecret))
            failures.Add("Broker:UpstreamClientSecret must not be blank.");
        if (options.UpstreamScopes.Count == 0)
            failures.Add("Broker:UpstreamScopes must not be empty.");

        ValidateStaticClients(options, failures);

        if (options.TransactionLifetimeMinutes <= 0)
            failures.Add("Broker:TransactionLifetimeMinutes must be positive.");
        if (options.ClientCodeLifetimeMinutes <= 0)
            failures.Add("Broker:ClientCodeLifetimeMinutes must be positive.");
        if (options.IssuedAccessTokenLifetimeMinutes <= 0)
            failures.Add("Broker:IssuedAccessTokenLifetimeMinutes must be positive.");
        if (options.IssuedRefreshTokenLifetimeDays <= 0)
            failures.Add("Broker:IssuedRefreshTokenLifetimeDays must be positive.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateIssuerUri(BrokerOptions options, bool isDevelopment, List<string> failures)
    {
        if (!Uri.TryCreate(options.IssuerUri, UriKind.Absolute, out var issuerUri))
        {
            failures.Add("Broker:IssuerUri must be an absolute URI.");
            return;
        }

        var isAllowedScheme = string.Equals(issuerUri.Scheme, "https", StringComparison.Ordinal)
            || (isDevelopment && string.Equals(issuerUri.Scheme, "http", StringComparison.Ordinal));
        if (!isAllowedScheme)
            failures.Add("Broker:IssuerUri must use https (http is only allowed in the Development environment).");

        if (!string.IsNullOrEmpty(issuerUri.Fragment))
            failures.Add("Broker:IssuerUri must not contain a fragment.");
    }

    // Upstream URLs allow http in every environment, not just Development — tests point them at
    // a local fake Bitbucket server over plain http, and that fake never runs in Production.
    private static void ValidateAbsoluteHttpsUri(string value, string fieldName, List<string> failures)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
            failures.Add($"Broker:{fieldName} must be an absolute URI.");
    }

    private static void ValidateStaticClients(BrokerOptions options, List<string> failures)
    {
        foreach (var client in options.StaticClients)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId))
            {
                failures.Add("Broker:StaticClients entries must all have a non-blank ClientId.");
                continue;
            }

            if (client.RedirectUris.Count == 0)
            {
                failures.Add($"Broker:StaticClients client '{client.ClientId}' must list at least one RedirectUri.");
                continue;
            }

            foreach (var redirectUri in client.RedirectUris)
            {
                if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
                    failures.Add($"Broker:StaticClients client '{client.ClientId}' has a non-absolute RedirectUri '{redirectUri}'.");
            }
        }

        var duplicateClientIds = options.StaticClients
            .Select(c => c.ClientId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        foreach (var duplicate in duplicateClientIds)
            failures.Add($"Broker:StaticClients has more than one entry for ClientId '{duplicate}'.");
    }
}
