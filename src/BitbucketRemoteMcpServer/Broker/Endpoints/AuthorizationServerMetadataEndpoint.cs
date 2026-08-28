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

        var document = new Dictionary<string, object?>
        {
            ["issuer"] = issuer,
            ["authorization_endpoint"] = $"{issuer}/authorize",
            ["token_endpoint"] = $"{issuer}/token",
            ["jwks_uri"] = $"{issuer}/.well-known/jwks.json",
            ["response_types_supported"] = new[] { "code" },
            ["grant_types_supported"] = new[] { "authorization_code", "refresh_token" },
            ["code_challenge_methods_supported"] = new[] { "S256" },
            ["token_endpoint_auth_methods_supported"] = new[] { "none", "client_secret_post" },
            ["scopes_supported"] = mcpAuthOptions.Value.ScopesSupported,
            ["authorization_response_iss_parameter_supported"] = true,
        };

        if (broker.DcrEnabled)
        {
            document["registration_endpoint"] = $"{issuer}/register";
        }

        return Results.Json(document);
    }
}
