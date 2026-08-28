namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>CRUD for <c>upstream_tokens</c>. See <see cref="UpstreamTokenSet"/> for why the
/// access/refresh tokens are stored in plaintext.</summary>
public sealed class UpstreamTokenStore(BrokerDbConnectionFactory connectionFactory)
{
    public void Upsert(UpstreamTokenSet tokenSet)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO upstream_tokens
                (upstream_token_id, subject, access_token, refresh_token, token_type,
                 access_expires_at, created_at, updated_at)
            VALUES
                ($id, $subject, $access_token, $refresh_token, $token_type,
                 $access_expires_at, $created_at, $updated_at)
            ON CONFLICT(upstream_token_id) DO UPDATE SET
                access_token      = excluded.access_token,
                refresh_token     = excluded.refresh_token,
                token_type        = excluded.token_type,
                access_expires_at = excluded.access_expires_at,
                updated_at        = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$id", tokenSet.UpstreamTokenId);
        command.Parameters.AddWithValue("$subject", tokenSet.Subject);
        command.Parameters.AddWithValue("$access_token", tokenSet.AccessToken);
        command.Parameters.AddWithValue("$refresh_token", tokenSet.RefreshToken ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$token_type", tokenSet.TokenType);
        command.Parameters.AddWithValue("$access_expires_at", tokenSet.AccessExpiresAt.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$created_at", tokenSet.CreatedAt.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$updated_at", tokenSet.UpdatedAt.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
    }

    public UpstreamTokenSet? TryGet(string upstreamTokenId)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT upstream_token_id, subject, access_token, refresh_token, token_type,
                   access_expires_at, created_at, updated_at
            FROM upstream_tokens
            WHERE upstream_token_id = $id;
            """;
        command.Parameters.AddWithValue("$id", upstreamTokenId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTokenSet(reader) : null;
    }

    public void Delete(string upstreamTokenId)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM upstream_tokens WHERE upstream_token_id = $id;";
        command.Parameters.AddWithValue("$id", upstreamTokenId);
        command.ExecuteNonQuery();
    }

    private static UpstreamTokenSet ReadTokenSet(SqliteDataReader reader) => new(
        UpstreamTokenId: reader.GetString(0),
        Subject: reader.GetString(1),
        AccessToken: reader.GetString(2),
        RefreshToken: reader.IsDBNull(3) ? null : reader.GetString(3),
        TokenType: reader.GetString(4),
        AccessExpiresAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5)),
        CreatedAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)),
        UpdatedAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(7)));
}
