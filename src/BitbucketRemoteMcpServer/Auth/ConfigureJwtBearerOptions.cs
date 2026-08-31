namespace BitbucketRemoteMcpServer.Auth;

/// <summary>
/// Configures the resource-server gate through DI (resolving IOptions&lt;McpAuthOptions&gt;)
/// rather than an eager copy captured in an AddJwtBearer lambda — capturing an eager copy at
/// registration time would make McpAuthOptionsValidator's ValidateOnStart purely decorative,
/// since the lambda would run before the validator has had a chance to reject it.
/// Authority is set to the external authorization server — JwtBearer fetches its discovery
/// document and JWKS from there (with automatic key-rotation refresh) rather than us holding or
/// resolving signing keys ourselves.
///
/// Implements IConfigureNamedOptions, not plain IConfigureOptions: the options system only
/// invokes a plain IConfigureOptions&lt;T&gt; registration for the *default-named* ("") options
/// instance. JwtBearer's options are requested under the scheme name ("Bearer"), so a plain
/// IConfigureOptions&lt;JwtBearerOptions&gt; here would be silently skipped.
/// </summary>
public sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IOptions<McpAuthOptions> _authOptions;
    private readonly IServiceProvider _serviceProvider;

    public ConfigureJwtBearerOptions(IOptions<McpAuthOptions> authOptions, IServiceProvider serviceProvider)
    {
        _authOptions = authOptions;
        _serviceProvider = serviceProvider;
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
            return;

        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        var auth = _authOptions.Value;

        // SigningKeyProvider is only registered when Broker:Enabled — its presence is this
        // class's only signal that the broker is on, since BrokerOptions itself always resolves
        // (with defaults) whether or not the section was ever bound.
        var signingKeyProvider = _serviceProvider.GetService<Broker.SigningKeyProvider>();
        if (signingKeyProvider is not null)
        {
            ConfigureForSelfHostedBroker(options, auth, signingKeyProvider);
            return;
        }

        options.Authority = auth.Issuer;
        if (!string.IsNullOrWhiteSpace(auth.MetadataAddress))
        {
            options.MetadataAddress = auth.MetadataAddress;
        }

        // Derived from the configured Issuer's scheme, not IHostEnvironment — McpAuthOptionsValidator
        // already only allows a plaintext http Issuer in Development, so this mirrors that decision
        // instead of duplicating it via a second environment check.
        options.RequireHttpsMetadata = auth.Issuer.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        // Keep "sub" as "sub" instead of ASP.NET's default ClaimTypes.NameIdentifier mapping —
        // the broker's credential resolver keys on the raw "sub" claim.
        options.MapInboundClaims = false;

        var tvp = options.TokenValidationParameters;
        tvp.ValidateIssuer = true;
        tvp.ValidateAudience = true;
        tvp.ValidateLifetime = true;
        tvp.ValidateIssuerSigningKey = true;
        tvp.ValidIssuer = auth.Issuer;
        tvp.ValidAudience = auth.ResourceUri;
        tvp.ClockSkew = TimeSpan.FromSeconds(auth.ClockSkewSeconds);

        // Pinned — blocks alg-confusion attacks and "none".
        tvp.ValidAlgorithms = ["RS256"];
    }

    /// <summary>
    /// With the broker enabled this server IS the authorization server, so it must never point
    /// Authority/MetadataAddress at itself over HTTP — JwtBearer's discovery fetch would ask the
    /// process to answer its own request before it has finished starting. Feed the signing key
    /// and issuer directly from the in-process <see cref="Broker.SigningKeyProvider"/> instead of
    /// fetching JWKS over the network.
    /// </summary>
    private void ConfigureForSelfHostedBroker(
        JwtBearerOptions options, McpAuthOptions auth, Broker.SigningKeyProvider signingKeyProvider)
    {
        var brokerOptions = _serviceProvider.GetRequiredService<IOptions<Broker.BrokerOptions>>().Value;

        options.MapInboundClaims = false;

        var tvp = options.TokenValidationParameters;
        tvp.ValidateIssuer = true;
        tvp.ValidateAudience = true;
        tvp.ValidateLifetime = true;
        tvp.ValidateIssuerSigningKey = true;
        tvp.ValidIssuer = brokerOptions.IssuerUri;
        tvp.ValidAudience = auth.ResourceUri;
        tvp.ClockSkew = TimeSpan.FromSeconds(auth.ClockSkewSeconds);
        tvp.ValidAlgorithms = ["RS256"];
        tvp.IssuerSigningKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(signingKeyProvider.Rsa)
        {
            KeyId = signingKeyProvider.KeyId,
        };
    }
}
