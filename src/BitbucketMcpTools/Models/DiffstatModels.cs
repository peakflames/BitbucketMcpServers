using System.Text.Json;
using System.Text.Json.Serialization;

namespace BitbucketMcpTools.Models;

/// <summary>
/// Represents the paginated response from the Bitbucket diffstat API endpoint.
/// </summary>
public class DiffstatResponse
{
    /// <summary>
    /// The list of diffstat entries for the current page.
    /// </summary>
    [JsonPropertyName("values")]
    public List<DiffstatEntry>? Values { get; set; }

    /// <summary>
    /// URL to the next page of results, or null if this is the last page.
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; set; }

    /// <summary>
    /// The current page number.
    /// </summary>
    [JsonPropertyName("page")]
    public int? Page { get; set; }

    /// <summary>
    /// The number of items per page.
    /// </summary>
    [JsonPropertyName("pagelen")]
    public int? PageLen { get; set; }

    /// <summary>
    /// The total number of items across all pages.
    /// </summary>
    [JsonPropertyName("size")]
    public int? Size { get; set; }
}

/// <summary>
/// Represents a single file change entry in the diffstat response.
/// </summary>
public class DiffstatEntry
{
    /// <summary>
    /// The status of the file change (e.g., "added", "modified", "removed", "renamed").
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// The number of lines added in this file.
    /// </summary>
    [JsonPropertyName("lines_added")]
    public int? LinesAdded { get; set; }

    /// <summary>
    /// The number of lines removed in this file.
    /// </summary>
    [JsonPropertyName("lines_removed")]
    public int? LinesRemoved { get; set; }

    /// <summary>
    /// Information about the old (original) file, present for modified, removed, or renamed files.
    /// </summary>
    [JsonPropertyName("old")]
    public DiffstatFile? Old { get; set; }

    /// <summary>
    /// Information about the new file, present for added, modified, or renamed files.
    /// </summary>
    [JsonPropertyName("new")]
    public DiffstatFile? New { get; set; }
}

/// <summary>
/// Represents file path information in a diffstat entry.
/// </summary>
public class DiffstatFile
{
    /// <summary>
    /// The file path relative to the repository root.
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>
    /// The type of the entry (typically "commit_file").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Links associated with this file.
    /// </summary>
    [JsonPropertyName("links")]
    public DiffstatFileLinks? Links { get; set; }
}

/// <summary>
/// Represents links associated with a diffstat file.
/// </summary>
public class DiffstatFileLinks
{
    /// <summary>
    /// Link to the file content.
    /// </summary>
    [JsonPropertyName("self")]
    public DiffstatLink? Self { get; set; }
}

/// <summary>
/// Represents a single link in the diffstat response.
/// </summary>
public class DiffstatLink
{
    /// <summary>
    /// The URL of the link.
    /// </summary>
    [JsonPropertyName("href")]
    public string? Href { get; set; }
}

/// <summary>
/// JSON source generation context for AOT compatibility with diffstat models.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(DiffstatResponse))]
[JsonSerializable(typeof(DiffstatEntry))]
[JsonSerializable(typeof(DiffstatFile))]
[JsonSerializable(typeof(DiffstatFileLinks))]
[JsonSerializable(typeof(DiffstatLink))]
public partial class DiffstatJsonContext : JsonSerializerContext
{
}
