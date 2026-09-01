namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// CRUD for <c>registered_clients</c> — clients created via DCR (<c>POST /register</c>), shipped
/// disabled by default. Statically pre-registered clients (<see cref="BrokerOptions.StaticClients"/>)
/// never appear here; they live in config instead. redirect_uris is stored as a single
/// newline-joined TEXT column rather than a child table — DCR clients are expected to be few, and
/// this keeps the schema flat.
/// </summary>
public sealed class RegisteredClientStore(BrokerDbConnectionFactory connectionFactory)
{
    public void Insert(RegisteredClient client)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO registered_clients (client_id, client_secret_hash, redirect_uris, client_name, created_at)
            VALUES ($client_id, $client_secret_hash, $redirect_uris, $client_name, $created_at);
            """;
        command.Parameters.AddWithValue("$client_id", client.ClientId);
        command.Parameters.AddWithValue("$client_secret_hash", client.ClientSecretHash ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$redirect_uris", string.Join('\n', client.RedirectUris));
        command.Parameters.AddWithValue("$client_name", client.ClientName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$created_at", client.CreatedAt.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
    }

    public RegisteredClient? TryGet(string clientId)
    {
        using var connection = connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT client_id, client_secret_hash, redirect_uris, client_name, created_at
            FROM registered_clients
            WHERE client_id = $client_id;
            """;
        command.Parameters.AddWithValue("$client_id", clientId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return new RegisteredClient(
            ClientId: reader.GetString(0),
            ClientSecretHash: reader.IsDBNull(1) ? null : reader.GetString(1),
            RedirectUris: reader.GetString(2).Split('\n', StringSplitOptions.RemoveEmptyEntries),
            ClientName: reader.IsDBNull(3) ? null : reader.GetString(3),
            CreatedAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4)));
    }
}

public sealed record RegisteredClient(
    string ClientId,
    string? ClientSecretHash,
    IReadOnlyList<string> RedirectUris,
    string? ClientName,
    DateTimeOffset CreatedAt);
