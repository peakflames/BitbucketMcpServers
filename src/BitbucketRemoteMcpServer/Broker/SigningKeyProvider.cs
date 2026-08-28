namespace BitbucketRemoteMcpServer.Broker;

/// <summary>
/// The RSA key this server signs its own issued JWTs with. Generated once and persisted in
/// <c>app_meta</c> — if it lived only in memory, a pod restart would silently invalidate every
/// access token in flight, the exact failure the vibepages precedent's key-persistence design
/// exists to prevent. Loaded lazily so constructing this type (at DI registration time) never
/// touches the database.
/// </summary>
public sealed class SigningKeyProvider
{
    private const string KeyMaterialMetaKey = "broker_signing_key_pkcs8_base64";
    private const string KeyIdMetaKey = "broker_signing_key_id";

    private readonly Lazy<(RSA Rsa, string KeyId)> _key;

    public SigningKeyProvider(AppMetaStore appMetaStore)
    {
        _key = new Lazy<(RSA, string)>(() => LoadOrCreate(appMetaStore));
    }

    public RSA Rsa => _key.Value.Rsa;

    public string KeyId => _key.Value.KeyId;

    private static (RSA Rsa, string KeyId) LoadOrCreate(AppMetaStore appMetaStore)
    {
        var existingKeyMaterial = appMetaStore.TryGet(KeyMaterialMetaKey);
        var existingKeyId = appMetaStore.TryGet(KeyIdMetaKey);

        if (existingKeyMaterial is not null && existingKeyId is not null)
        {
            var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(existingKeyMaterial), out _);
            return (rsa, existingKeyId);
        }

        var newRsa = RSA.Create(2048);
        var newKeyId = Guid.NewGuid().ToString("N");
        appMetaStore.Set(KeyMaterialMetaKey, Convert.ToBase64String(newRsa.ExportPkcs8PrivateKey()));
        appMetaStore.Set(KeyIdMetaKey, newKeyId);
        return (newRsa, newKeyId);
    }
}
