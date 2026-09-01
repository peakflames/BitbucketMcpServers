namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// Generic key/value access to <c>app_meta</c>, beyond the <c>schema_version</c> row
/// <see cref="SchemaMigrator"/> owns directly. A later phase uses this for the authorization
/// server's signing key: generated once, persisted here, so a restarted pod does not silently
/// invalidate every JWT it has issued.
/// </summary>
public sealed class AppMetaStore(BrokerDbConnectionFactory connectionFactory)
{
    public string? TryGet(string key)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_meta WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    public void Set(string key, string value)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO app_meta (key, value) VALUES ($key, $value) " +
            "ON CONFLICT(key) DO UPDATE SET value = $value;";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }
}
