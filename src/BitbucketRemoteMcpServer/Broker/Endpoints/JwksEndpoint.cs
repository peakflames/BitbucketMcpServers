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

        var jwk = new BrokerJwk
        {
            Kty = "RSA",
            Use = "sig",
            Alg = "RS256",
            Kid = signingKeyProvider.KeyId,
            N = Base64UrlEncoder.Encode(parameters.Modulus!),
            E = Base64UrlEncoder.Encode(parameters.Exponent!),
        };

        return Results.Json(new BrokerJwks { Keys = [jwk] }, BrokerJsonContext.Default.BrokerJwks);
    }
}
