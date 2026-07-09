using System.Text.Json;
using System.Text.Json.Serialization;

namespace BitbucketMcpTools.Models;

/// <summary>
/// Represents a page of the paginated Bitbucket pull requests list response, requested
/// with a restricted `fields` query so only id and draft status are returned.
/// </summary>
public class PullRequestDraftPage
{
    /// <summary>
    /// The pull request id/draft entries for the current page.
    /// </summary>
    [JsonPropertyName("values")]
    public List<PullRequestDraftItem>? Values { get; set; }

    /// <summary>
    /// URL to the next page of results, or null if this is the last page.
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; set; }
}

/// <summary>
/// A single pull request's id and draft status, as returned by a restricted `fields` query.
/// </summary>
public class PullRequestDraftItem
{
    /// <summary>
    /// The pull request id.
    /// </summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>
    /// Whether the pull request is a draft.
    /// </summary>
    [JsonPropertyName("draft")]
    public bool? Draft { get; set; }
}

/// <summary>
/// The draft status of a single pull request, as returned by a restricted `fields` query.
/// </summary>
public class PullRequestDraftInfo
{
    /// <summary>
    /// Whether the pull request is a draft.
    /// </summary>
    [JsonPropertyName("draft")]
    public bool? Draft { get; set; }
}

/// <summary>
/// JSON source generation context for AOT compatibility with pull request draft-status models.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(PullRequestDraftPage))]
[JsonSerializable(typeof(PullRequestDraftItem))]
[JsonSerializable(typeof(PullRequestDraftInfo))]
public partial class PullRequestJsonContext : JsonSerializerContext
{
}
