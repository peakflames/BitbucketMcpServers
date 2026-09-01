namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// CRUD for <c>our_refresh_tokens</c> — the refresh token <em>we</em> issue to the MCP client,
/// keyed by hash rather than by the token itself, so that a compromise of the database does not
/// hand out anything directly usable. The FastMCP precedent's docstring states the goal exactly:
/// "we store only metadata (not the token itself) for security — if storage is compromised,
/// attackers get hashes they can't reverse into usable tokens."
/// </summary>
public sealed class OurRefreshTokenStore(BrokerDbConnectionFactory connectionFactory)
{
    public void Insert(
        string refreshToken,
        string upstreamTokenId,
        string clientId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO our_refresh_tokens (token_hash, upstream_token_id, client_id, created_at, expires_at)
            VALUES ($token_hash, $upstream_token_id, $client_id, $created_at, $expires_at);
            """;
        command.Parameters.AddWithValue("$token_hash", TokenHashing.Hash(refreshToken));
        command.Parameters.AddWithValue("$upstream_token_id", upstreamTokenId);
        command.Parameters.AddWithValue("$client_id", clientId);
        command.Parameters.AddWithValue("$created_at", createdAt.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$expires_at", expiresAt.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
    }

    /// <summary>Looks the token up by hash and verifies the match in constant time. Returns null
    /// for a wrong, expired, or unknown token alike — see <see cref="TokenHashing.Verify"/>.</summary>
    public (string UpstreamTokenId, string ClientId)? TryGet(string refreshToken)
    {
        var candidateHash = TokenHashing.Hash(refreshToken);

        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT token_hash, upstream_token_id, client_id
            FROM our_refresh_tokens
            WHERE token_hash = $token_hash AND expires_at > $now;
            """;
        command.Parameters.AddWithValue("$token_hash", candidateHash);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        // The WHERE clause already matched token_hash exactly, so FixedTimeEquals here doesn't
        // change the outcome — it exists so this call site can never regress to a fast-exit `==`
        // if the query above is ever loosened (e.g. to a prefix scan).
        var storedHash = reader.GetString(0);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(candidateHash), Encoding.UTF8.GetBytes(storedHash)))
        {
            return null;
        }

        return (reader.GetString(1), reader.GetString(2));
    }

    public void Delete(string refreshToken)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM our_refresh_tokens WHERE token_hash = $token_hash;";
        command.Parameters.AddWithValue("$token_hash", TokenHashing.Hash(refreshToken));
        command.ExecuteNonQuery();
    }

    public int DeleteExpired()
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM our_refresh_tokens WHERE expires_at <= $now;";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return command.ExecuteNonQuery();
    }
}
