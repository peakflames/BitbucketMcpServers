namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// CRUD for <c>oauth_transactions</c>. A transaction is read at most twice in its life — once at
/// <c>/oauth/callback</c> to validate it, once more to delete it — so this store does not attempt
/// the delete-on-read atomicity that <see cref="ClientCodeStore"/> needs; the broker logic decides
/// when a transaction is done with.
/// </summary>
public sealed class OAuthTransactionStore(BrokerDbConnectionFactory connectionFactory)
{
    public void Insert(OAuthTransaction transaction)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO oauth_transactions
                (txn_id, client_id, client_redirect_uri, client_state, client_code_challenge,
                 upstream_code_verifier, scopes, resource, consent_token_hash, created_at, expires_at)
            VALUES
                ($txn_id, $client_id, $client_redirect_uri, $client_state, $client_code_challenge,
                 $upstream_code_verifier, $scopes, $resource, $consent_token_hash, $created_at, $expires_at);
            """;
        command.Parameters.AddWithValue("$txn_id", transaction.TxnId);
        command.Parameters.AddWithValue("$client_id", transaction.ClientId);
        command.Parameters.AddWithValue("$client_redirect_uri", transaction.ClientRedirectUri);
        command.Parameters.AddWithValue("$client_state", transaction.ClientState);
        command.Parameters.AddWithValue("$client_code_challenge", transaction.ClientCodeChallenge);
        command.Parameters.AddWithValue("$upstream_code_verifier", transaction.UpstreamCodeVerifier);
        command.Parameters.AddWithValue("$scopes", transaction.Scopes);
        command.Parameters.AddWithValue("$resource", transaction.Resource ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$consent_token_hash", transaction.ConsentTokenHash);
        command.Parameters.AddWithValue("$created_at", transaction.CreatedAt.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$expires_at", transaction.ExpiresAt.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
    }

    /// <summary>Returns null if the transaction does not exist or has expired. An expired row is
    /// not distinguished from a missing one — the caller (the callback handler) treats both as
    /// "this authorization attempt is no longer valid" and neither should hint at which.</summary>
    public OAuthTransaction? TryGet(string txnId)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT txn_id, client_id, client_redirect_uri, client_state, client_code_challenge,
                   upstream_code_verifier, scopes, resource, consent_token_hash, created_at, expires_at
            FROM oauth_transactions
            WHERE txn_id = $txn_id AND expires_at > $now;
            """;
        command.Parameters.AddWithValue("$txn_id", txnId);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTransaction(reader) : null;
    }

    public void Delete(string txnId)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM oauth_transactions WHERE txn_id = $txn_id;";
        command.Parameters.AddWithValue("$txn_id", txnId);
        command.ExecuteNonQuery();
    }

    /// <summary>Deletes every row whose expiry has already passed. Called by the janitor; exposed
    /// publicly so tests can assert sweep behavior without waiting on the janitor's timer.</summary>
    public int DeleteExpired()
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM oauth_transactions WHERE expires_at <= $now;";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return command.ExecuteNonQuery();
    }

    private static OAuthTransaction ReadTransaction(SqliteDataReader reader) => new(
        TxnId: reader.GetString(0),
        ClientId: reader.GetString(1),
        ClientRedirectUri: reader.GetString(2),
        ClientState: reader.GetString(3),
        ClientCodeChallenge: reader.GetString(4),
        UpstreamCodeVerifier: reader.GetString(5),
        Scopes: reader.GetString(6),
        Resource: reader.IsDBNull(7) ? null : reader.GetString(7),
        ConsentTokenHash: reader.GetString(8),
        CreatedAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(9)),
        ExpiresAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(10)));
}
