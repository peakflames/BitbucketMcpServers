namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// CRUD for <c>client_codes</c> — the code we hand the client at the end of
/// <c>/oauth/callback</c>, redeemed once at <c>POST /token</c>. The code itself is never stored;
/// only its hash is (it is a bearer secret we only ever need to verify, not replay). Consuming a
/// code is a single atomic delete-and-return: <see cref="TryConsume"/> makes the code unusable
/// the instant it is read, closing the window a separate "check then delete" would leave open for
/// the same code to be redeemed twice.
/// </summary>
public sealed class ClientCodeStore(BrokerDbConnectionFactory connectionFactory)
{
    public void Insert(
        string code,
        string upstreamTokenId,
        string clientId,
        string clientRedirectUri,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO client_codes
                (code_hash, upstream_token_id, client_id, client_redirect_uri, created_at, expires_at)
            VALUES
                ($code_hash, $upstream_token_id, $client_id, $client_redirect_uri, $created_at, $expires_at);
            """;
        command.Parameters.AddWithValue("$code_hash", TokenHashing.Hash(code));
        command.Parameters.AddWithValue("$upstream_token_id", upstreamTokenId);
        command.Parameters.AddWithValue("$client_id", clientId);
        command.Parameters.AddWithValue("$client_redirect_uri", clientRedirectUri);
        command.Parameters.AddWithValue("$created_at", createdAt.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$expires_at", expiresAt.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes the row for <paramref name="code"/> and returns what it pointed to, in one
    /// statement — an unconditional <c>DELETE ... RETURNING</c> keyed by hash, filtered
    /// server-side by expiry. Returns null both when the code was never issued and when it has
    /// already been consumed or has expired; callers must not distinguish those cases in their
    /// response, to avoid telling an attacker which one applies.
    /// </summary>
    public (string UpstreamTokenId, string ClientId, string ClientRedirectUri)? TryConsume(string code)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM client_codes
            WHERE code_hash = $code_hash AND expires_at > $now
            RETURNING upstream_token_id, client_id, client_redirect_uri;
            """;
        command.Parameters.AddWithValue("$code_hash", TokenHashing.Hash(code));
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? (reader.GetString(0), reader.GetString(1), reader.GetString(2))
            : null;
    }

    public int DeleteExpired()
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM client_codes WHERE expires_at <= $now;";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return command.ExecuteNonQuery();
    }
}
