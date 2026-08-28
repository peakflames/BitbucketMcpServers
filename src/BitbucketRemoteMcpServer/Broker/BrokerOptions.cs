namespace BitbucketRemoteMcpServer.Broker;

// Deliberately `sealed class`, never `record` — same rationale as `McpAuthOptions` and
// `AccessOptions`: a record's generated ToString() prints every property.

/// <summary>
/// Configuration for the token-broker storage layer. This section only carries what storage
/// itself needs (where the database lives); TTL policy belongs to the broker logic that a later
/// phase adds on top, not to the storage mechanics here — stores take an explicit expiry from
/// their caller rather than reading one out of options.
/// </summary>
public sealed class BrokerOptions
{
    public const string SectionName = "Broker";

    public bool Enabled { get; set; }

    /// <summary>
    /// Path to the SQLite database file. Relative paths resolve against the current working
    /// directory (the deployed container's mounted volume in production). If the containing
    /// directory does not exist and cannot be created, <see cref="Storage.BrokerDbConnectionFactory"/>
    /// falls back to a temp-directory path and logs a loud warning rather than crash-looping —
    /// data does not survive a restart in that fallback, which is the point of the warning.
    /// </summary>
    public string DatabasePath { get; set; } = "data/broker.db";
}
