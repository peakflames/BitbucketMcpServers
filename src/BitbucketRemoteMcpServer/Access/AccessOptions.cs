namespace BitbucketRemoteMcpServer.Access;

// Deliberately `sealed class`, never `record` — same rationale as `McpAuthOptions`: a record's
// generated ToString() would print every property, and this section sits next to config that is
// sensitive in later phases.
//
// `FailClosed` is deliberately not a property here: a future per-user credential gate always
// fails closed and is never configurable. This options class only carries what is actually used
// today.

public sealed class AccessOptions
{
    public const string SectionName = "Access";

    public bool Enabled { get; set; }

    /// <summary>The rollout lever: when true, DisabledTools filtering still runs and would still
    /// be logged, but never actually hides a tool or denies a call. Reserved for parity with
    /// Phase 2's audit-first rollout; Phase 1 does not yet emit an audit record.</summary>
    public bool AuditOnly { get; set; }

    /// <summary>Which JWT claim resolves to a caller identity. Unused until Phase 2's credential
    /// resolution and CredentialGateFilter land — reserved here so the config shape does not
    /// change between phases.</summary>
    public string IdentityClaim { get; set; } = "email";

    /// <summary>Tool names hidden from tools/list and denied on tools/call. Holds `search_code`
    /// and `list_repositories` during the transitional window where authentication exists but
    /// per-user credential passthrough does not — a permission-mirror gate was rejected as an
    /// option (Bitbucket already enforces per-repository visibility natively once a caller's own
    /// credential is used), so removing these tools is the only safe interim choice. A later
    /// phase makes both tools correct without needing this list.</summary>
    public List<string> DisabledTools { get; set; } = [];
}
