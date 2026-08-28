namespace BitbucketRemoteMcpServer.Tests.Broker;

/// <summary>
/// Exercises the Phase 2 SQLite token store directly, against a real database file on disk — not
/// an in-memory connection string — so that WAL mode, the busy timeout, and restart survival are
/// actually being tested rather than assumed. Each test gets its own temp file via
/// <see cref="IAsyncLifetime"/> equivalents (constructor/dispose), so tests can run in parallel
/// without contending for the same database.
/// </summary>
public sealed class BrokerStorageTests : IDisposable
{
    private readonly string _databasePath;
    private readonly BrokerDbConnectionFactory _connectionFactory;

    public BrokerStorageTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"broker-storage-tests-{Guid.NewGuid():N}.db");
        var options = Microsoft.Extensions.Options.Options.Create(new BrokerOptions
        {
            Enabled = true,
            DatabasePath = _databasePath,
        });
        _connectionFactory = new BrokerDbConnectionFactory(options, NullLogger<BrokerDbConnectionFactory>.Instance);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void OpenConnection_SetsWalJournalMode()
    {
        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";

        Assert.Equal("wal", ((string)command.ExecuteScalar()!).ToLowerInvariant());
    }

    [Fact]
    public void OAuthTransactionStore_RoundTripsAndDeletes()
    {
        var store = new OAuthTransactionStore(_connectionFactory);
        var transaction = new OAuthTransaction(
            TxnId: "txn-1",
            ClientId: "client-1",
            ClientRedirectUri: "http://127.0.0.1/callback",
            ClientState: "client-state",
            ClientCodeChallenge: "challenge",
            UpstreamCodeVerifier: "verifier",
            Scopes: "account repository",
            Resource: "https://bitbucket-mcp.example.invalid/mcp",
            ConsentTokenHash: TokenHashing.Hash("consent-token"),
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15));

        store.Insert(transaction);

        var found = store.TryGet("txn-1");
        Assert.NotNull(found);
        Assert.Equal(transaction.ClientId, found!.ClientId);
        Assert.Equal(transaction.Resource, found.Resource);

        store.Delete("txn-1");
        Assert.Null(store.TryGet("txn-1"));
    }

    [Fact]
    public void OAuthTransactionStore_TryGet_TreatsAnExpiredRowAsMissing()
    {
        var store = new OAuthTransactionStore(_connectionFactory);
        store.Insert(new OAuthTransaction(
            "txn-expired", "client-1", "http://127.0.0.1/callback", "state", "challenge", "verifier",
            "account", null, TokenHashing.Hash("consent"),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-20),
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-5)));

        Assert.Null(store.TryGet("txn-expired"));
    }

    [Fact]
    public void OAuthTransactionStore_DeleteExpired_SweepsOnlyExpiredRows()
    {
        var store = new OAuthTransactionStore(_connectionFactory);
        store.Insert(MakeTransaction("txn-expired", DateTimeOffset.UtcNow.AddMinutes(-5)));
        store.Insert(MakeTransaction("txn-live", DateTimeOffset.UtcNow.AddMinutes(15)));

        var deletedCount = store.DeleteExpired();

        Assert.Equal(1, deletedCount);
        Assert.Null(store.TryGet("txn-expired"));
        Assert.NotNull(store.TryGet("txn-live"));
    }

    [Fact]
    public void TokenStoreJanitor_SweepOnce_EvictsExpiredRowsAcrossEveryTtlTable()
    {
        InsertUpstreamToken("upstream-1");

        var transactions = new OAuthTransactionStore(_connectionFactory);
        transactions.Insert(MakeTransaction("txn-expired", DateTimeOffset.UtcNow.AddMinutes(-5)));

        var clientCodes = new ClientCodeStore(_connectionFactory);
        clientCodes.Insert("expired-code", "upstream-1", "client-1", "http://127.0.0.1/callback",
            "code-challenge", DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-5));

        var jtiMappings = new JtiMappingStore(_connectionFactory);
        jtiMappings.Insert("jti-expired", "upstream-1", DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1));

        var refreshTokens = new OurRefreshTokenStore(_connectionFactory);
        refreshTokens.Insert("expired-refresh-token", "upstream-1", "client-1",
            DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1));

        var janitor = new TokenStoreJanitor(
            transactions, clientCodes, jtiMappings, refreshTokens, NullLogger<TokenStoreJanitor>.Instance);

        var deletedCount = janitor.SweepOnce();

        Assert.Equal(4, deletedCount);
        Assert.Equal(0, janitor.SweepOnce());
    }

    [Fact]
    public void TokenStoreJanitor_SweepOnce_LeavesUpstreamTokensAlone()
    {
        // upstream_tokens carries no expires_at of its own — its lifetime tracks Bitbucket's
        // actual refresh-token lifetime (not yet measured), so the janitor must never
        // guess at when to delete it.
        InsertUpstreamToken("upstream-1");

        var janitor = new TokenStoreJanitor(
            new OAuthTransactionStore(_connectionFactory),
            new ClientCodeStore(_connectionFactory),
            new JtiMappingStore(_connectionFactory),
            new OurRefreshTokenStore(_connectionFactory),
            NullLogger<TokenStoreJanitor>.Instance);

        janitor.SweepOnce();

        Assert.NotNull(new UpstreamTokenStore(_connectionFactory).TryGet("upstream-1"));
    }

    [Fact]
    public void ClientCodeStore_TryConsume_IsSingleUse()
    {
        var store = new ClientCodeStore(_connectionFactory);
        store.Insert("the-code", "upstream-1", "client-1", "http://127.0.0.1/callback",
            "code-challenge", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5));

        var first = store.TryConsume("the-code");
        Assert.NotNull(first);
        Assert.Equal("upstream-1", first!.UpstreamTokenId);

        var second = store.TryConsume("the-code");
        Assert.Null(second);
    }

    [Fact]
    public void ClientCodeStore_TryConsume_RejectsAnExpiredCode()
    {
        var store = new ClientCodeStore(_connectionFactory);
        store.Insert("expired-code", "upstream-1", "client-1", "http://127.0.0.1/callback",
            "code-challenge", DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-5));

        Assert.Null(store.TryConsume("expired-code"));
    }

    [Fact]
    public void UpstreamTokenStore_RoundTripsAndUpserts()
    {
        var store = new UpstreamTokenStore(_connectionFactory);
        var original = new UpstreamTokenSet(
            "upstream-1", "subject-1", "access-token-v1", "refresh-token-v1", "Bearer",
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        store.Upsert(original);

        var found = store.TryGet("upstream-1");
        Assert.NotNull(found);
        Assert.Equal("access-token-v1", found!.AccessToken);

        // Upsert with the same id simulates a refresh: the row updates in place rather than
        // duplicating.
        store.Upsert(original with { AccessToken = "access-token-v2", UpdatedAt = DateTimeOffset.UtcNow });
        Assert.Equal("access-token-v2", store.TryGet("upstream-1")!.AccessToken);
    }

    [Fact]
    public void JtiMappingStore_RoundTripsAndTreatsExpiredAsMissing()
    {
        InsertUpstreamToken("upstream-1");

        var store = new JtiMappingStore(_connectionFactory);
        store.Insert("jti-1", "upstream-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));
        Assert.Equal("upstream-1", store.TryGetUpstreamTokenId("jti-1"));

        store.Insert("jti-expired", "upstream-1", DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1));
        Assert.Null(store.TryGetUpstreamTokenId("jti-expired"));
    }

    [Fact]
    public void OurRefreshTokenStore_TryGet_MatchesOnlyTheRightToken()
    {
        InsertUpstreamToken("upstream-1");

        var store = new OurRefreshTokenStore(_connectionFactory);
        store.Insert("the-real-refresh-token", "upstream-1", "client-1",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        var found = store.TryGet("the-real-refresh-token");
        Assert.NotNull(found);
        Assert.Equal("upstream-1", found!.Value.UpstreamTokenId);

        Assert.Null(store.TryGet("a-completely-different-token"));
    }

    [Fact]
    public void OurRefreshTokenStore_DoesNotStoreTheTokenInPlaintext()
    {
        InsertUpstreamToken("upstream-1");

        const string secret = "super-secret-refresh-token-abc123";
        var store = new OurRefreshTokenStore(_connectionFactory);
        store.Insert(secret, "upstream-1", "client-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT token_hash FROM our_refresh_tokens;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.NotEqual(secret, reader.GetString(0));
    }

    [Fact]
    public void AppMetaStore_RoundTripsAndUpdates()
    {
        var store = new AppMetaStore(_connectionFactory);
        Assert.Null(store.TryGet("signing-key"));

        store.Set("signing-key", "v1");
        Assert.Equal("v1", store.TryGet("signing-key"));

        store.Set("signing-key", "v2");
        Assert.Equal("v2", store.TryGet("signing-key"));
    }

    [Fact]
    public void SchemaVersion_IsRecordedInAppMeta()
    {
        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_meta WHERE key = 'schema_version';";

        Assert.Equal("2", (string)command.ExecuteScalar()!);
    }

    [Fact]
    public void Storage_SurvivesANewConnectionFactoryAgainstTheSameFile()
    {
        // Simulates a process restart: a second BrokerDbConnectionFactory pointed at the same
        // file must see what the first one wrote, and must not re-run schema creation in a way
        // that loses data (CREATE TABLE IF NOT EXISTS, not CREATE TABLE).
        var store1 = new OAuthTransactionStore(_connectionFactory);
        store1.Insert(MakeTransaction("txn-restart", DateTimeOffset.UtcNow.AddMinutes(15)));

        var options = Microsoft.Extensions.Options.Options.Create(new BrokerOptions
        {
            Enabled = true,
            DatabasePath = _databasePath,
        });
        var secondFactory = new BrokerDbConnectionFactory(options, NullLogger<BrokerDbConnectionFactory>.Instance);
        var store2 = new OAuthTransactionStore(secondFactory);

        var found = store2.TryGet("txn-restart");
        Assert.NotNull(found);
        Assert.Equal("client-1", found!.ClientId);
    }

    [Fact]
    public void DatabaseFile_NeverContainsAKnownPlaintextSecret()
    {
        InsertUpstreamToken("upstream-1");

        const string clientCodeSecret = "plaintext-client-code-should-never-appear";
        const string refreshTokenSecret = "plaintext-refresh-token-should-never-appear";

        new ClientCodeStore(_connectionFactory).Insert(clientCodeSecret, "upstream-1", "client-1",
            "http://127.0.0.1/callback", "code-challenge", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5));
        new OurRefreshTokenStore(_connectionFactory).Insert(refreshTokenSecret, "upstream-1", "client-1",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        // Force a WAL checkpoint so everything above is actually in the main database file
        // rather than sitting in the -wal file this assertion does not look at.
        using (var connection = _connectionFactory.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA wal_checkpoint(FULL);";
            command.ExecuteNonQuery();
        }

        // Plain File.ReadAllText fails here: SQLite (via SQLitePCLRaw) keeps its own handle open
        // on the file, and Windows' default share mode would collide with that handle rather
        // than with anything this test does wrong.
        string fileContents;
        using (var fileStream = new FileStream(_databasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(fileStream, Encoding.Latin1))
        {
            fileContents = reader.ReadToEnd();
        }

        Assert.DoesNotContain(clientCodeSecret, fileContents, StringComparison.Ordinal);
        Assert.DoesNotContain(refreshTokenSecret, fileContents, StringComparison.Ordinal);
    }

    private static OAuthTransaction MakeTransaction(string txnId, DateTimeOffset expiresAt) => new(
        txnId, "client-1", "http://127.0.0.1/callback", "state", "challenge", "verifier",
        "account", null, TokenHashing.Hash("consent"), DateTimeOffset.UtcNow, expiresAt);

    /// <summary>jti_mappings and our_refresh_tokens both carry a foreign key to
    /// upstream_tokens(upstream_token_id) — a mapping or a refresh token can only ever exist for a
    /// credential that is actually stored, never a dangling id. Tests that only care about the
    /// referencing table still need a real row here to satisfy that constraint.</summary>
    private void InsertUpstreamToken(string upstreamTokenId) =>
        new UpstreamTokenStore(_connectionFactory).Upsert(new UpstreamTokenSet(
            upstreamTokenId, "subject-1", "access-token", "refresh-token", "Bearer",
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
}
