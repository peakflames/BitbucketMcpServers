namespace BitbucketRemoteMcpServer.Broker.Endpoints;

/// <summary>
/// <c>POST /register</c> — Dynamic Client Registration (RFC 7591). Built, shipped disabled
/// (<see cref="BrokerOptions.DcrEnabled"/>), and only ever mapped by
/// <see cref="BrokerEndpointsExtensions.MapBrokerEndpoints"/> when that flag is on. Mirrors
/// FastMCP's <c>HardenedOAuthProxy.register_client</c>: force <c>response_types</c> to
/// <c>["code"]</c>, allowlist <c>grant_types</c>, and accept-but-ignore <c>application_type</c>.
/// </summary>
public static class RegisterEndpoint
{
    private static readonly string[] AllowedGrantTypes = ["authorization_code", "refresh_token"];
    private static readonly List<string> DefaultGrantTypes = ["authorization_code", "refresh_token"];

    public static async Task<IResult> HandleAsync(
        HttpRequest request, RegisteredClientStore registeredClientStore, CancellationToken cancellationToken)
    {
        DcrRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<DcrRequest>(request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_client_metadata", error_description = "Malformed JSON." });
        }

        if (body?.RedirectUris is null || body.RedirectUris.Count == 0)
        {
            return Results.BadRequest(new
            {
                error = "invalid_redirect_uri",
                error_description = "redirect_uris is required and must be non-empty.",
            });
        }

        foreach (var redirectUri in body.RedirectUris)
        {
            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
            {
                return Results.BadRequest(new
                {
                    error = "invalid_redirect_uri",
                    error_description = $"'{redirectUri}' is not an absolute URI.",
                });
            }
        }

        var requestedGrantTypes = body.GrantTypes is { Count: > 0 } ? body.GrantTypes : DefaultGrantTypes;
        if (requestedGrantTypes.Except(AllowedGrantTypes).Any())
        {
            return Results.BadRequest(new
            {
                error = "invalid_client_metadata",
                error_description = "Only authorization_code and refresh_token grants are supported.",
            });
        }

        var authMethod = string.IsNullOrWhiteSpace(body.TokenEndpointAuthMethod) ? "none" : body.TokenEndpointAuthMethod;

        string? clientSecret = null;
        string? clientSecretHash = null;
        if (!string.Equals(authMethod, "none", StringComparison.Ordinal))
        {
            clientSecret = PkceHelper.GenerateOpaqueSecret();
            clientSecretHash = TokenHashing.Hash(clientSecret);
        }

        var clientId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        registeredClientStore.Insert(new RegisteredClient(
            ClientId: clientId,
            ClientSecretHash: clientSecretHash,
            RedirectUris: body.RedirectUris,
            ClientName: body.ClientName,
            CreatedAt: now));

        return Results.Json(
            new
            {
                client_id = clientId,
                client_secret = clientSecret,
                client_id_issued_at = now.ToUnixTimeSeconds(),
                redirect_uris = body.RedirectUris,
                token_endpoint_auth_method = authMethod,
                grant_types = requestedGrantTypes,
                response_types = new[] { "code" },
                client_name = body.ClientName,
            },
            statusCode: StatusCodes.Status201Created);
    }
}

public sealed class DcrRequest
{
    [JsonPropertyName("redirect_uris")]
    public List<string>? RedirectUris { get; set; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; set; }

    [JsonPropertyName("grant_types")]
    public List<string>? GrantTypes { get; set; }

    [JsonPropertyName("response_types")]
    public List<string>? ResponseTypes { get; set; }

    [JsonPropertyName("application_type")]
    public string? ApplicationType { get; set; }
}
