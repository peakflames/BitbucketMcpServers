namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// Opens connections to the broker's SQLite database. SQLite plus WAL supports exactly one
/// writer at a time — see values.yaml's single-replica note wherever this is deployed — so every
/// connection this factory hands out is configured identically rather than trusting each caller
/// to set the same pragmas.
/// </summary>
public sealed class BrokerDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly object _initLock = new();
    private bool _initialized;

    public string DatabasePath { get; }

    public BrokerDbConnectionFactory(IOptions<BrokerOptions> options, ILogger<BrokerDbConnectionFactory> logger)
    {
        DatabasePath = ResolveWritablePath(options.Value.DatabasePath, logger);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString();
    }

    /// <summary>
    /// Opens a new connection with WAL journaling and a busy timeout already applied. Every
    /// caller gets a fresh connection — SQLite connections are cheap and not meant to be shared
    /// across threads, and <see cref="Microsoft.Data.Sqlite"/> pools the underlying native handles
    /// itself.
    /// </summary>
    public SqliteConnection OpenConnection()
    {
        EnsureSchema();

        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragmas = connection.CreateCommand();
        pragmas.CommandText = "PRAGMA journal_mode = 'WAL'; PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;";
        pragmas.ExecuteNonQuery();

        return connection;
    }

    // Schema creation happens once per factory instance (i.e. once per process), on first use,
    // rather than at construction time — constructing the factory must not have side effects that
    // could fail before DI has finished wiring up logging.
    private void EnsureSchema()
    {
        if (_initialized)
            return;

        lock (_initLock)
        {
            if (_initialized)
                return;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            SchemaMigrator.EnsureLatest(connection);

            _initialized = true;
        }
    }

    /// <summary>
    /// Resolves the configured path, creating its directory if needed. Falls back to a path
    /// under the OS temp directory — with a loud warning, since data written there does not
    /// survive a restart — rather than letting the process crash-loop, matching an operational
    /// failure seen in production elsewhere in this ecosystem when a mounted volume was not
    /// writable by the running user (missing `securityContext.fsGroup`).
    /// </summary>
    private static string ResolveWritablePath(string configuredPath, Microsoft.Extensions.Logging.ILogger logger)
    {
        var fullPath = Path.GetFullPath(configuredPath);
        var directory = Path.GetDirectoryName(fullPath);

        if (string.IsNullOrEmpty(directory) || TryEnsureDirectoryIsWritable(directory))
            return fullPath;

        var fallbackPath = Path.Combine(Path.GetTempPath(), "bitbucket-mcp-broker", Path.GetFileName(fullPath));
        Directory.CreateDirectory(Path.GetDirectoryName(fallbackPath)!);

        logger.LogWarning(
            "Broker:DatabasePath directory '{ConfiguredDirectory}' is not writable. Falling back to " +
            "'{FallbackPath}', which does NOT survive a container/process restart. Every stored " +
            "credential and pending authorization will be lost on the next restart. Fix the volume " +
            "mount (commonly a missing securityContext.fsGroup) rather than relying on this fallback.",
            directory, fallbackPath);

        return fallbackPath;
    }

    private static bool TryEnsureDirectoryIsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probePath = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
