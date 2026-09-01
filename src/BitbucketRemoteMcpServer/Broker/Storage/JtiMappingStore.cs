namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// CRUD for <c>jti_mappings</c> — maps the <c>jti</c> claim of an access token we issued back to
/// the <see cref="UpstreamTokenSet"/> it was minted from. The <c>jti</c> is not a secret (it is a
/// public claim inside a token whose signature is what actually protects it), so unlike
/// <see cref="ClientCodeStore"/> and <see cref="OurRefreshTokenStore"/> it is stored as-is, not
/// hashed.
/// </summary>
public sealed class JtiMappingStore(BrokerDbConnectionFactory connectionFactory)
{
    public void Insert(string jti, string upstreamTokenId, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO jti_mappings (jti, upstream_token_id, created_at, expires_at)
            VALUES ($jti, $upstream_token_id, $created_at, $expires_at);
            """;
        command.Parameters.AddWithValue("$jti", jti);
        command.Parameters.AddWithValue("$upstream_token_id", upstreamTokenId);
        command.Parameters.AddWithValue("$created_at", createdAt.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$expires_at", expiresAt.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
    }

    public string? TryGetUpstreamTokenId(string jti)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT upstream_token_id FROM jti_mappings WHERE jti = $jti AND expires_at > $now;";
        command.Parameters.AddWithValue("$jti", jti);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        return command.ExecuteScalar() as string;
    }

    public int DeleteExpired()
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM jti_mappings WHERE expires_at <= $now;";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return command.ExecuteNonQuery();
    }
}
