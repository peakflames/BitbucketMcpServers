namespace BitbucketRemoteMcpServer.Broker;

/// <summary>
/// Mints the RS256 JWTs this server hands back to MCP clients from <c>POST /token</c>, signed
/// with <see cref="SigningKeyProvider"/>'s persisted key. Uses
/// <see cref="JsonWebTokenHandler"/> (already in the dependency graph via JwtBearer) rather than
/// hand-rolling JWS the way <c>tests/StubAuthorizationServer</c> deliberately does — that project
/// hand-rolls specifically to make fault injection trivial; production code has no such need and
/// benefits from the well-tested claim serialization instead.
/// </summary>
public sealed class JwtIssuer(SigningKeyProvider signingKeyProvider, IOptions<BrokerOptions> brokerOptions)
{
    private static readonly JsonWebTokenHandler Handler = new();

    public string IssueAccessToken(string subject, string jti, string scope, string audience, DateTimeOffset now)
    {
        var broker = brokerOptions.Value;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = broker.IssuerUri,
            Audience = audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(broker.IssuedAccessTokenLifetimeMinutes).UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = subject,
                ["jti"] = jti,
                ["scope"] = scope,
            },
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(signingKeyProvider.Rsa) { KeyId = signingKeyProvider.KeyId },
                SecurityAlgorithms.RsaSha256),
        };

        return Handler.CreateToken(descriptor);
    }
}
