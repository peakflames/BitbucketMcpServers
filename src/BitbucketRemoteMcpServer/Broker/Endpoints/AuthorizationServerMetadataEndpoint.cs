namespace BitbucketRemoteMcpServer.Broker.Endpoints;

/// <summary>
/// <c>GET /.well-known/oauth-authorization-server</c> (RFC 8414). Distinct from the RFC 9728
/// protected-*resource* metadata the MCP SDK already serves at
/// <c>/.well-known/oauth-protected-resource/mcp</c> — this document describes this server acting
/// as the *authorization* server, which it only does when the broker is enabled.
/// <c>code_challenge_methods_supported</c> must list "S256" or the MCP C# SDK client hard-fails,
/// and <c>authorization_response_iss_parameter_supported</c> must be true since every redirect
/// back to the client carries an <c>iss</c> parameter (see <see cref="CallbackEndpoint"/>).
/// </summary>
public static class AuthorizationServerMetadataEndpoint
{
    public static IResult Handle(IOptions<BrokerOptions> brokerOptions, IOptions<McpAuthOptions> mcpAuthOptions)
    {
        var broker = brokerOptions.Value;
        var issuer = broker.IssuerUri;

        var document = new AuthorizationServerMetadataResponse
        {
            Issuer = issuer,
            AuthorizationEndpoint = $"{issuer}/authorize",
            TokenEndpoint = $"{issuer}/token",
            JwksUri = $"{issuer}/.well-known/jwks.json",
            ResponseTypesSupported = ["code"],
            GrantTypesSupported = ["authorization_code", "refresh_token"],
            CodeChallengeMethodsSupported = ["S256"],
            TokenEndpointAuthMethodsSupported = ["none", "client_secret_post"],
            ScopesSupported = mcpAuthOptions.Value.ScopesSupported,
            AuthorizationResponseIssParameterSupported = true,
            RegistrationEndpoint = broker.DcrEnabled ? $"{issuer}/register" : null,
        };

        return Results.Json(document, BrokerJsonContext.Default.AuthorizationServerMetadataResponse);
    }
}
