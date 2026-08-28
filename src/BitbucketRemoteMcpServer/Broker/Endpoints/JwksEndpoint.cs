namespace BitbucketRemoteMcpServer.Broker.Endpoints;

/// <summary>
/// <c>GET /.well-known/jwks.json</c> — publishes only the public half of
/// <see cref="SigningKeyProvider"/>'s key. This server's own resource-server gate never fetches
/// this over the network (see <see cref="ConfigureJwtBearerOptions"/>'s self-hosted-AS branch,
/// which feeds the key in-process instead); this endpoint exists for external verifiers.
/// </summary>
public static class JwksEndpoint
{
    public static IResult Handle(SigningKeyProvider signingKeyProvider)
    {
        var parameters = signingKeyProvider.Rsa.ExportParameters(includePrivateParameters: false);

        var jwk = new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["alg"] = "RS256",
            ["kid"] = signingKeyProvider.KeyId,
            ["n"] = Base64UrlEncoder.Encode(parameters.Modulus!),
            ["e"] = Base64UrlEncoder.Encode(parameters.Exponent!),
        };

        return Results.Json(new Dictionary<string, object?> { ["keys"] = new[] { jwk } });
    }
}
