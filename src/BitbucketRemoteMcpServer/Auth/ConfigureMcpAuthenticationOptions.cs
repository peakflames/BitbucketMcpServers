namespace BitbucketRemoteMcpServer.Auth;

/// <summary>
/// ResourceMetadataUri is deliberately left untouched (default null) so the SDK auto-serves
/// /.well-known/oauth-protected-resource[/mcp] itself rather than us hand-rolling that endpoint.
/// jwks_uri is intentionally omitted from ResourceMetadata — RFC 9728's jwks_uri means
/// resource-response signing, not token signing, and it MUST be https, which would break
/// localhost dev.
///
/// Implements IConfigureNamedOptions, not plain IConfigureOptions — see ConfigureJwtBearerOptions
/// for why: a plain IConfigureOptions&lt;T&gt; is only invoked for the default-named ("") options
/// instance, and the Mcp scheme's options are requested under its own scheme name.
/// </summary>
public sealed class ConfigureMcpAuthenticationOptions : IConfigureNamedOptions<McpAuthenticationOptions>
{
    private readonly IOptions<McpAuthOptions> _authOptions;
    private readonly IServiceProvider _serviceProvider;

    public ConfigureMcpAuthenticationOptions(IOptions<McpAuthOptions> authOptions, IServiceProvider serviceProvider)
    {
        _authOptions = authOptions;
        _serviceProvider = serviceProvider;
    }

    public void Configure(string? name, McpAuthenticationOptions options)
    {
        if (!string.Equals(name, McpAuthenticationDefaults.AuthenticationScheme, StringComparison.Ordinal))
            return;

        Configure(options);
    }

    public void Configure(McpAuthenticationOptions options)
    {
        var auth = _authOptions.Value;

        // With the broker enabled this server is its own authorization server, so the resource
        // metadata must advertise itself rather than the external issuer McpAuth:Issuer would
        // otherwise name. Same "is SigningKeyProvider registered" signal as
        // ConfigureJwtBearerOptions.
        var brokerServiceProvider = _serviceProvider.GetService<Broker.SigningKeyProvider>() is not null
            ? _serviceProvider.GetRequiredService<IOptions<Broker.BrokerOptions>>().Value
            : null;
        var authorizationServer = brokerServiceProvider?.IssuerUri ?? auth.Issuer;

        options.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = auth.ResourceUri,
            AuthorizationServers = new List<string> { authorizationServer },
            BearerMethodsSupported = new List<string> { "header" },
            ScopesSupported = new List<string>(auth.ScopesSupported),
        };
    }
}
