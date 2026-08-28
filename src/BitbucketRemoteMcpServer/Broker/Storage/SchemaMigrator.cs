namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// Hand-rolled schema creation and versioning — no EF Core, per the decision recorded in the
/// research doc's build-vs-buy section. <c>app_meta</c> holds <c>schema_version</c> as a row
/// rather than a dedicated column, so it can also hold the AS signing key a later phase adds,
/// matching the table's role in the precedent this was modeled on.
/// </summary>
internal static class SchemaMigrator
{
    private const int LatestVersion = 2;

    public static void EnsureLatest(SqliteConnection connection)
    {
        using var createAppMeta = connection.CreateCommand();
        createAppMeta.CommandText =
            """
            CREATE TABLE IF NOT EXISTS app_meta (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        createAppMeta.ExecuteNonQuery();

        var currentVersion = ReadSchemaVersion(connection);
        if (currentVersion >= LatestVersion)
            return;

        // Only one version exists so far; this switch is the seam for the next one; add a case,
        // not a rewrite of this method.
        using var transaction = connection.BeginTransaction();

        if (currentVersion < 1)
            CreateVersion1Schema(connection, transaction);

        if (currentVersion < 2)
            CreateVersion2Schema(connection, transaction);

        WriteSchemaVersion(connection, transaction, LatestVersion);
        transaction.Commit();
    }

    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_meta WHERE key = 'schema_version';";
        var value = command.ExecuteScalar() as string;
        return value is null ? 0 : int.Parse(value);
    }

    private static void WriteSchemaVersion(SqliteConnection connection, SqliteTransaction transaction, int version)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO app_meta (key, value) VALUES ('schema_version', $version) " +
            "ON CONFLICT(key) DO UPDATE SET value = $version;";
        command.Parameters.AddWithValue("$version", version.ToString());
        command.ExecuteNonQuery();
    }

    private static void CreateVersion1Schema(SqliteConnection connection, SqliteTransaction transaction)
    {
        // TTLs are enforced by callers filtering on expires_at, and swept by the janitor — SQLite
        // has no built-in row expiry. expires_at/created_at are Unix seconds (INTEGER), not TEXT,
        // so comparisons against strftime('%s','now') stay numeric.
        var statements = new[]
        {
            """
            CREATE TABLE oauth_transactions (
                txn_id                  TEXT PRIMARY KEY,
                client_id                TEXT NOT NULL,
                client_redirect_uri      TEXT NOT NULL,
                client_state             TEXT NOT NULL,
                client_code_challenge    TEXT NOT NULL,
                upstream_code_verifier   TEXT NOT NULL,
                scopes                   TEXT NOT NULL,
                resource                 TEXT NULL,
                consent_token_hash       TEXT NOT NULL,
                created_at               INTEGER NOT NULL,
                expires_at               INTEGER NOT NULL
            );
            """,
            "CREATE INDEX idx_oauth_transactions_expires_at ON oauth_transactions (expires_at);",

            """
            CREATE TABLE client_codes (
                code_hash          TEXT PRIMARY KEY,
                upstream_token_id  TEXT NOT NULL,
                client_id          TEXT NOT NULL,
                client_redirect_uri TEXT NOT NULL,
                created_at         INTEGER NOT NULL,
                expires_at         INTEGER NOT NULL
            );
            """,
            "CREATE INDEX idx_client_codes_expires_at ON client_codes (expires_at);",

            """
            CREATE TABLE upstream_tokens (
                upstream_token_id  TEXT PRIMARY KEY,
                subject            TEXT NOT NULL,
                access_token       TEXT NOT NULL,
                refresh_token      TEXT NULL,
                token_type         TEXT NOT NULL,
                access_expires_at  INTEGER NOT NULL,
                created_at         INTEGER NOT NULL,
                updated_at         INTEGER NOT NULL
            );
            """,
            "CREATE INDEX idx_upstream_tokens_subject ON upstream_tokens (subject);",

            """
            CREATE TABLE jti_mappings (
                jti                TEXT PRIMARY KEY,
                upstream_token_id  TEXT NOT NULL REFERENCES upstream_tokens (upstream_token_id),
                created_at         INTEGER NOT NULL,
                expires_at         INTEGER NOT NULL
            );
            """,
            "CREATE INDEX idx_jti_mappings_expires_at ON jti_mappings (expires_at);",

            """
            CREATE TABLE our_refresh_tokens (
                token_hash         TEXT PRIMARY KEY,
                upstream_token_id  TEXT NOT NULL REFERENCES upstream_tokens (upstream_token_id),
                client_id          TEXT NOT NULL,
                created_at         INTEGER NOT NULL,
                expires_at         INTEGER NOT NULL
            );
            """,
            "CREATE INDEX idx_our_refresh_tokens_expires_at ON our_refresh_tokens (expires_at);",

            """
            CREATE TABLE registered_clients (
                client_id           TEXT PRIMARY KEY,
                client_secret_hash  TEXT NULL,
                redirect_uris       TEXT NOT NULL,
                client_name         TEXT NULL,
                created_at          INTEGER NOT NULL
            );
            """,
        };

        foreach (var sql in statements)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    // Phase 3: POST /token verifies the *client's* PKCE code_verifier against the code_challenge
    // it presented at /authorize — but oauth_transactions (which held that challenge) is deleted
    // once /oauth/callback issues a client code, so the challenge has to survive on the
    // client_codes row itself for /token to check it later. DEFAULT '' lets this run against a
    // database that already has (short-lived, by-then-expired-in-practice) rows from version 1.
    private static void CreateVersion2Schema(SqliteConnection connection, SqliteTransaction transaction)
    {
        var statements = new[]
        {
            "ALTER TABLE client_codes ADD COLUMN client_code_challenge TEXT NOT NULL DEFAULT '';",
        };

        foreach (var sql in statements)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
