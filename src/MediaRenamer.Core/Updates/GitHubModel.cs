using System.Text.Json.Serialization;

namespace MediaRenamer.Core.Updates;

// Data transfer objects of the GitHub REST API, only the fields that are used.
// See https://docs.github.com/en/rest/releases/releases#get-the-latest-release

/// <summary>Response of <c>GET /repos/{owner}/{repo}/releases/latest</c>.</summary>
public sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("html_url")] string? HtmlUrl,
    [property: JsonPropertyName("assets")] IReadOnlyList<GitHubAsset>? Assets);

/// <summary>A file of a release.</summary>
public sealed record GitHubAsset(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("digest")] string? Digest,
    [property: JsonPropertyName("browser_download_url")] string? DownloadUrl);

/// <summary>Source generated JSON metadata, so deserialization works in the trimmed executable.</summary>
[JsonSerializable(typeof(GitHubRelease))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
