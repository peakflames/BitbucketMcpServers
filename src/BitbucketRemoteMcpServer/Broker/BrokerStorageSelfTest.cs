namespace BitbucketRemoteMcpServer.Broker;

/// <summary>
/// Round-trips every store in <see cref="Storage"/> against a real SQLite file. Exists to be run
/// against the trimmed, single-file <c>dotnet publish</c> output specifically — invoked as
/// <c>BitbucketRemoteMcpServer --self-test-broker-storage &lt;path-to.db&gt;</c> — because
/// building and running from source proves nothing about whether
/// <c>Microsoft.Data.Sqlite</c>/<c>SQLitePCLRaw</c>'s native asset and reflection-based provider
/// lookup survive <c>PublishTrimmed</c> + <c>PublishSingleFile</c>.
///
/// Run twice against the same path to also cover restart survival: the first run creates a
/// long-lived marker row and reports <c>CREATED</c>; the second run finds it and reports
/// <c>RESTART-OK</c>, proving the data is actually on disk and not, say, silently redirected to
/// an in-memory connection string.
/// </summary>
public static class BrokerStorageSelfTest
{
    private const string RestartMarkerTxnId = "self-test-restart-marker";

    public static int Run(string databasePath)
    {
        try
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var options = Microsoft.Extensions.Options.Options.Create(new BrokerOptions
            {
                Enabled = true,
                DatabasePath = databasePath,
                IssuerUri = "https://self-test.example.invalid",
            });
            var connectionFactory = new BrokerDbConnectionFactory(
                options, loggerFactory.CreateLogger<BrokerDbConnectionFactory>());

            RunRestartMarkerCheck(connectionFactory);
            RunOAuthTransactionRoundTrip(connectionFactory);
            RunClientCodeSingleUseCheck(connectionFactory);
            RunUpstreamTokenRoundTrip(connectionFactory);
            RunJtiMappingRoundTrip(connectionFactory);
            RunOurRefreshTokenRoundTrip(connectionFactory, out var refreshTokenSecret);
            RunAppMetaRoundTrip(connectionFactory);
            RunJwtIssuanceAndValidationCheck(connectionFactory, options);
            RunSecretAbsenceCheck(connectionFactory, databasePath, refreshTokenSecret);

            Console.WriteLine("SELF-TEST: PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SELF-TEST: FAIL - {ex}");
            return 1;
        }
    }

    private static void RunRestartMarkerCheck(BrokerDbConnectionFactory connectionFactory)
    {
        var store = new OAuthTransactionStore(connectionFactory);
        var existing = store.TryGet(RestartMarkerTxnId);

        if (existing is null)
        {
            store.Insert(new OAuthTransaction(
                TxnId: RestartMarkerTxnId,
                ClientId: "self-test-client",
                ClientRedirectUri: "http://127.0.0.1/callback",
                ClientState: "self-test-state",
                ClientCodeChallenge: "self-test-challenge",
                UpstreamCodeVerifier: "self-test-verifier",
                Scopes: "account",
                Resource: null,
                ConsentTokenHash: TokenHashing.Hash("self-test-consent"),
                CreatedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)));
            Console.WriteLine("SELF-TEST: CREATED restart marker");
        }
        else if (existing.ClientId == "self-test-client")
        {
            Console.WriteLine("SELF-TEST: RESTART-OK restart marker found from a previous run");
        }
        else
        {
            throw new InvalidOperationException("Restart marker row exists but has unexpected content.");
        }
    }

    private static void RunOAuthTransactionRoundTrip(BrokerDbConnectionFactory connectionFactory)
    {
        var store = new OAuthTransactionStore(connectionFactory);
        var txnId = $"self-test-txn-{Guid.NewGuid():N}";
        var expected = new OAuthTransaction(
            TxnId: txnId,
            ClientId: "self-test-client",
            ClientRedirectUri: "http://127.0.0.1/callback",
            ClientState: "self-test-state",
            ClientCodeChallenge: "self-test-challenge",
            UpstreamCodeVerifier: "self-test-verifier",
            Scopes: "account repository",
            Resource: "https://bitbucket-mcp.example.invalid/mcp",
            ConsentTokenHash: TokenHashing.Hash("self-test-consent"),
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15));

        store.Insert(expected);
        var actual = store.TryGet(txnId) ?? throw new InvalidOperationException("Transaction round trip returned null.");
        if (actual.ClientId != expected.ClientId || actual.Resource != expected.Resource)
            throw new InvalidOperationException("Transaction round trip returned mismatched content.");

        store.Delete(txnId);
        if (store.TryGet(txnId) is not null)
            throw new InvalidOperationException("Transaction still readable after delete.");
    }

    private static void RunClientCodeSingleUseCheck(BrokerDbConnectionFactory connectionFactory)
    {
        var store = new ClientCodeStore(connectionFactory);
        var code = $"self-test-code-{Guid.NewGuid():N}";
        store.Insert(code, "self-test-upstream-token-id", "self-test-client", "http://127.0.0.1/callback",
            "self-test-code-challenge", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5));

        var firstConsume = store.TryConsume(code)
            ?? throw new InvalidOperationException("First consume of a fresh code returned null.");
        if (firstConsume.UpstreamTokenId != "self-test-upstream-token-id")
            throw new InvalidOperationException("Consumed code returned mismatched content.");

        if (store.TryConsume(code) is not null)
            throw new InvalidOperationException("Client code was consumed twice - single-use guarantee broken.");
    }

    private static void RunUpstreamTokenRoundTrip(BrokerDbConnectionFactory connectionFactory)
    {
        var store = new UpstreamTokenStore(connectionFactory);
        var id = $"self-test-upstream-{Guid.NewGuid():N}";
        store.Upsert(new UpstreamTokenSet(
            UpstreamTokenId: id,
            Subject: "self-test-subject",
            AccessToken: "self-test-access-token",
            RefreshToken: "self-test-refresh-token",
            TokenType: "Bearer",
            AccessExpiresAt: DateTimeOffset.UtcNow.AddHours(2),
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow));

        var actual = store.TryGet(id) ?? throw new InvalidOperationException("Upstream token round trip returned null.");
        if (actual.AccessToken != "self-test-access-token")
            throw new InvalidOperationException("Upstream token round trip returned mismatched content.");

        store.Delete(id);
    }

    private static void RunJtiMappingRoundTrip(BrokerDbConnectionFactory connectionFactory)
    {
        var upstreamStore = new UpstreamTokenStore(connectionFactory);
        var upstreamId = $"self-test-upstream-for-jti-{Guid.NewGuid():N}";
        upstreamStore.Upsert(new UpstreamTokenSet(
            upstreamId, "self-test-subject", "self-test-access-token", null, "Bearer",
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var store = new JtiMappingStore(connectionFactory);
        var jti = $"self-test-jti-{Guid.NewGuid():N}";
        store.Insert(jti, upstreamId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));

        if (store.TryGetUpstreamTokenId(jti) != upstreamId)
            throw new InvalidOperationException("JTI mapping round trip returned mismatched content.");

        // Deliberately not deleted: jti_mappings carries a foreign key to upstream_tokens, so
        // deleting the referenced row here (before also deleting the mapping) would only prove
        // this self-test's own cleanup order, not the storage layer. The row is a harmless,
        // clearly-named leftover in a self-test database.
    }

    private static void RunOurRefreshTokenRoundTrip(BrokerDbConnectionFactory connectionFactory, out string refreshTokenSecret)
    {
        refreshTokenSecret = $"self-test-super-secret-refresh-token-{Guid.NewGuid():N}";
        var upstreamId = $"self-test-upstream-for-refresh-{Guid.NewGuid():N}";

        // our_refresh_tokens carries a foreign key to upstream_tokens; a refresh token can only
        // ever exist for a credential that is actually stored.
        new UpstreamTokenStore(connectionFactory).Upsert(new UpstreamTokenSet(
            upstreamId, "self-test-subject", "self-test-access-token", null, "Bearer",
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var store = new OurRefreshTokenStore(connectionFactory);
        store.Insert(refreshTokenSecret, upstreamId, "self-test-client",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        var found = store.TryGet(refreshTokenSecret)
            ?? throw new InvalidOperationException("Refresh token round trip returned null.");
        if (found.UpstreamTokenId != upstreamId)
            throw new InvalidOperationException("Refresh token round trip returned mismatched content.");

        if (store.TryGet("not-the-right-token") is not null)
            throw new InvalidOperationException("Refresh token lookup matched an unrelated token.");
    }

    private static void RunAppMetaRoundTrip(BrokerDbConnectionFactory connectionFactory)
    {
        var store = new AppMetaStore(connectionFactory);
        var key = $"self-test-key-{Guid.NewGuid():N}";
        store.Set(key, "self-test-value");
        if (store.TryGet(key) != "self-test-value")
            throw new InvalidOperationException("App meta round trip returned mismatched content.");

        store.Set(key, "self-test-value-updated");
        if (store.TryGet(key) != "self-test-value-updated")
            throw new InvalidOperationException("App meta update did not take effect.");
    }

    /// <summary>
    /// A second trim risk, distinct from the SQLite one above: <c>Microsoft.IdentityModel.*</c>
    /// is reflection-heavy and only exercised when a token is actually signed/validated, so a trim
    /// bug here would not surface from the storage round trips above. Persists a signing key via
    /// <see cref="AppMetaStore"/> exactly as the real broker does, issues a JWT with it, and
    /// validates that JWT the same way the resource-server gate would.
    /// </summary>
    private static void RunJwtIssuanceAndValidationCheck(
        BrokerDbConnectionFactory connectionFactory, IOptions<BrokerOptions> options)
    {
        var signingKeyProvider = new SigningKeyProvider(new AppMetaStore(connectionFactory));
        var jwtIssuer = new JwtIssuer(signingKeyProvider, options);

        const string audience = "https://self-test-audience.example.invalid/mcp";
        var jwt = jwtIssuer.IssueAccessToken(
            subject: "self-test-subject", jti: "self-test-jti", scope: "bitbucket:read",
            audience: audience, now: DateTimeOffset.UtcNow);

        var handler = new JsonWebTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = options.Value.IssuerUri,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidAlgorithms = ["RS256"],
            IssuerSigningKey = new RsaSecurityKey(signingKeyProvider.Rsa) { KeyId = signingKeyProvider.KeyId },
        };

        var result = handler.ValidateTokenAsync(jwt, validationParameters).GetAwaiter().GetResult();
        if (!result.IsValid)
            throw new InvalidOperationException("Self-issued JWT failed validation.", result.Exception);
    }

    /// <summary>Reads the raw database file and asserts the plaintext refresh-token secret this
    /// run inserted does not appear anywhere in it — the hashed-column half of the schema rule,
    /// checked against the real file rather than against the store's own round trip.</summary>
    private static void RunSecretAbsenceCheck(
        BrokerDbConnectionFactory connectionFactory, string databasePath, string refreshTokenSecret)
    {
        // Force a WAL checkpoint so everything written above is actually in the main database
        // file rather than sitting in the -wal file this check does not look at.
        using (var connection = connectionFactory.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA wal_checkpoint(FULL);";
            command.ExecuteNonQuery();
        }

        // Plain File.ReadAllBytes fails here: SQLite (via SQLitePCLRaw) keeps its own handle open
        // on the file, and Windows' default share mode would collide with that handle.
        byte[] bytes;
        using (var fileStream = new FileStream(databasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var memoryStream = new MemoryStream())
        {
            fileStream.CopyTo(memoryStream);
            bytes = memoryStream.ToArray();
        }

        var needle = Encoding.UTF8.GetBytes(refreshTokenSecret);

        if (Contains(bytes, needle))
            throw new InvalidOperationException(
                "The plaintext refresh-token secret was found in the database file - it must be hashed.");
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return true;
        }

        return false;
    }
}
