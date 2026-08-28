namespace BitbucketRemoteMcpServer.Broker.Endpoints;

// Concrete, source-generation-friendly response shapes for every JSON body the broker endpoints
// emit. `PublishTrimmed` disables System.Text.Json's reflection fallback at runtime (not just at
// publish time — the runtimeconfig.json flag applies to `dotnet run` too), so an anonymous type or
// `Dictionary<string, object>` throws NotSupportedException the first time it is actually hit,
// even though a build and the test suite (which runs under a different host without that
// runtimeconfig flag) both stay green. These types exist so `BrokerJsonContext` can cover them.

public sealed record OAuthErrorResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string? ErrorDescription = null);

public sealed class AuthorizationServerMetadataResponse
{
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("authorization_endpoint")]
    public required string AuthorizationEndpoint { get; init; }

    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; init; }

    [JsonPropertyName("jwks_uri")]
    public required string JwksUri { get; init; }

    [JsonPropertyName("response_types_supported")]
    public required IReadOnlyList<string> ResponseTypesSupported { get; init; }

    [JsonPropertyName("grant_types_supported")]
    public required IReadOnlyList<string> GrantTypesSupported { get; init; }

    [JsonPropertyName("code_challenge_methods_supported")]
    public required IReadOnlyList<string> CodeChallengeMethodsSupported { get; init; }

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public required IReadOnlyList<string> TokenEndpointAuthMethodsSupported { get; init; }

    [JsonPropertyName("scopes_supported")]
    public required IReadOnlyList<string> ScopesSupported { get; init; }

    [JsonPropertyName("authorization_response_iss_parameter_supported")]
    public required bool AuthorizationResponseIssParameterSupported { get; init; }

    // Omitted entirely (not written as null) when DCR is disabled — RegisterEndpointTests asserts
    // TryGetProperty returns false in that case, not that the value is JSON null.
    [JsonPropertyName("registration_endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RegistrationEndpoint { get; init; }
}

public sealed class BrokerJwk
{
    [JsonPropertyName("kty")]
    public required string Kty { get; init; }

    [JsonPropertyName("use")]
    public required string Use { get; init; }

    [JsonPropertyName("alg")]
    public required string Alg { get; init; }

    [JsonPropertyName("kid")]
    public required string Kid { get; init; }

    [JsonPropertyName("n")]
    public required string N { get; init; }

    [JsonPropertyName("e")]
    public required string E { get; init; }
}

public sealed class BrokerJwks
{
    [JsonPropertyName("keys")]
    public required IReadOnlyList<BrokerJwk> Keys { get; init; }
}

public sealed class DcrResponse
{
    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    // Always written, including a JSON null for public clients — RegisterEndpointTests asserts
    // the property is present with ValueKind.Null, not absent.
    [JsonPropertyName("client_secret")]
    public string? ClientSecret { get; init; }

    [JsonPropertyName("client_id_issued_at")]
    public required long ClientIdIssuedAt { get; init; }

    [JsonPropertyName("redirect_uris")]
    public required IReadOnlyList<string> RedirectUris { get; init; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public required string TokenEndpointAuthMethod { get; init; }

    [JsonPropertyName("grant_types")]
    public required IReadOnlyList<string> GrantTypes { get; init; }

    [JsonPropertyName("response_types")]
    public required IReadOnlyList<string> ResponseTypes { get; init; }

    [JsonPropertyName("client_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientName { get; init; }
}

public sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public required string TokenType { get; init; }

    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("scope")]
    public required string Scope { get; init; }
}

[JsonSerializable(typeof(OAuthErrorResponse))]
[JsonSerializable(typeof(AuthorizationServerMetadataResponse))]
[JsonSerializable(typeof(BrokerJwks))]
[JsonSerializable(typeof(DcrRequest))]
[JsonSerializable(typeof(DcrResponse))]
[JsonSerializable(typeof(TokenResponse))]
internal sealed partial class BrokerJsonContext : JsonSerializerContext
{
}
