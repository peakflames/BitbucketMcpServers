namespace BitbucketRemoteMcpServer.Broker;

/// <summary>
/// Unifies the two places a client can be registered: statically in <c>Broker:StaticClients</c>
/// config (the only kind while DCR is disabled), and the <c>registered_clients</c> table (DCR,
/// checked only when <see cref="BrokerOptions.DcrEnabled"/> is true). Static clients are checked
/// first and are always public (no secret) — see <see cref="BrokerOptions.StaticClients"/>.
/// </summary>
public sealed class ClientLookup(IOptions<BrokerOptions> brokerOptions, RegisteredClientStore registeredClientStore)
{
    public ClientRecord? TryGet(string clientId)
    {
        var broker = brokerOptions.Value;

        var staticClient = broker.StaticClients
            .FirstOrDefault(c => string.Equals(c.ClientId, clientId, StringComparison.Ordinal));
        if (staticClient is not null)
            return new ClientRecord(staticClient.ClientId, staticClient.RedirectUris, ClientSecretHash: null);

        if (!broker.DcrEnabled)
            return null;

        var registered = registeredClientStore.TryGet(clientId);
        return registered is null
            ? null
            : new ClientRecord(registered.ClientId, registered.RedirectUris, registered.ClientSecretHash);
    }
}

public sealed record ClientRecord(string ClientId, IReadOnlyList<string> RedirectUris, string? ClientSecretHash);
