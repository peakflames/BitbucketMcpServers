namespace BitbucketRemoteMcpServer.Auth;

public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Reads McpAuth:Enabled eagerly, but only to pick the branch — every scheme is configured
    /// through IConfigureOptions&lt;T&gt; classes resolving IOptions&lt;McpAuthOptions&gt; from
    /// DI, never from a value captured here. The validator and its ValidateOnStart hook are
    /// registered only inside this enabled branch: registering ValidateOnStart unconditionally
    /// would let a malformed McpAuth section break servers that have auth off, exactly the
    /// regression this opt-in exists to prevent.
    /// </summary>
    public static bool AddMcpAuth(this WebApplicationBuilder builder)
    {
        var enabled = builder.Configuration.GetValue($"{McpAuthOptions.SectionName}:Enabled", false);
        if (!enabled)
            return false;

        var services = builder.Services;

        services.AddOptions<McpAuthOptions>()
            .Bind(builder.Configuration.GetSection(McpAuthOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<McpAuthOptions>, McpAuthOptionsValidator>();

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        services.AddSingleton<IConfigureOptions<McpAuthenticationOptions>, ConfigureMcpAuthenticationOptions>();

        services
            .AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer()
            .AddMcp(_ => { });

        // Schemes deliberately unpinned here (no AddAuthenticationSchemes call) — pinning
        // "Bearer" would route a 401 to JwtBearer instead of the MCP scheme, and the client would
        // never receive resource_metadata, silently breaking MCP discovery.
        services.AddAuthorizationBuilder()
            .AddPolicy(OAuthScopes.ReadPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => ScopeClaimHelper.HasScope(context.User, OAuthScopes.Read)));

        return true;
    }
}
