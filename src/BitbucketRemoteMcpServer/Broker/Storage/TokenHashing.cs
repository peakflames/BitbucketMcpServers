namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// Hashing for columns that only ever need to be verified, never replayed — client codes, our own
/// issued refresh tokens, DCR client secrets, and the consent-binding cookie. The schema rule:
/// store plaintext only what must be replayed upstream (Bitbucket access/refresh tokens); hash
/// everything we only need to verify. See <see cref="Verify"/> for the comparison half of that
/// rule — the vibepages precedent this was modeled on used a plain `!=` there, which is not
/// constant-time.
/// </summary>
internal static class TokenHashing
{
    public static string Hash(string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Constant-time comparison of a candidate secret against a stored hash. Ordinary string/byte
    /// equality short-circuits on the first differing byte, which leaks timing information about
    /// how much of a guess was correct; <see cref="CryptographicOperations.FixedTimeEquals"/> does
    /// not.
    /// </summary>
    public static bool Verify(string candidateSecret, string storedHash)
    {
        var candidateHash = Encoding.UTF8.GetBytes(Hash(candidateSecret));
        var expectedHash = Encoding.UTF8.GetBytes(storedHash);

        return candidateHash.Length == expectedHash.Length
               && CryptographicOperations.FixedTimeEquals(candidateHash, expectedHash);
    }
}
