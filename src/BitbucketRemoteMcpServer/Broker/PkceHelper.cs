namespace BitbucketRemoteMcpServer.Broker;

/// <summary>
/// PKCE (RFC 7636) primitives, used twice per flow — once for the client's pair (verified at
/// POST /token against the code_challenge captured at /authorize) and once for this server's own
/// pair on the upstream leg to Bitbucket (see the "two PKCE pairs, always" rule). Also doubles as
/// the source of opaque secrets (client codes, our own refresh tokens, the consent-cookie value)
/// since both are "generate N random bytes, base64url-encode them."
/// </summary>
public static class PkceHelper
{
    public static string GenerateCodeVerifier() => GenerateOpaqueSecret();

    public static string ComputeS256Challenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncoder.Encode(hash);
    }

    public static bool VerifyS256(string codeVerifier, string expectedChallenge)
    {
        var actualChallenge = Encoding.ASCII.GetBytes(ComputeS256Challenge(codeVerifier));
        var expected = Encoding.ASCII.GetBytes(expectedChallenge);

        return actualChallenge.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(actualChallenge, expected);
    }

    /// <summary>32 random bytes, base64url-encoded — long enough to be an unguessable bearer
    /// secret and also a valid PKCE code_verifier (43-128 chars; this yields 43).</summary>
    public static string GenerateOpaqueSecret() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
}
