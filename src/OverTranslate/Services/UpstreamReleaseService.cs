using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Velopack;

namespace OverTranslate.Services;

/// <summary>Release metadata only: there is deliberately no package or update manager here.</summary>
public sealed record UpstreamReleaseInfo(string LatestVersion, Uri ReleaseNotesUri);

internal static class UpstreamReleaseService
{
    internal const string LatestReleaseApi = "https://api.github.com/repos/Hon-Lu/OverTranslate/releases/latest";
    internal static string BaselineVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        MaxResponseContentBufferSize = 1024 * 1024,
    };

    internal static Task<UpstreamReleaseInfo?> CheckAsync() => FetchLatestAsync(Client, BaselineVersion);

    // A separate client parameter lets the protocol be tested without network access or credentials.
    internal static async Task<UpstreamReleaseInfo?> FetchLatestAsync(
        HttpClient client, string baselineVersion, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.UserAgent.ParseAdd("OverTranslate-Thai-Dev/" + BaselineVersion);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        // No settings, tokens, release assets, installers or repository overrides are consulted.
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var release = json.RootElement;
        if (release.ValueKind != JsonValueKind.Object) return null;
        if (!release.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False
            || !release.TryGetProperty("prerelease", out var prerelease) || prerelease.ValueKind != JsonValueKind.False)
            return null;
        if (!release.TryGetProperty("tag_name", out var tagValue) || tagValue.ValueKind != JsonValueKind.String)
            return null;

        var tag = tagValue.GetString()?.Trim();
        if (string.IsNullOrEmpty(tag)) return null;
        var versionText = tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
        if (versionText.Contains('-') || !SemanticVersion.TryParse(versionText, out var latest)
            || !SemanticVersion.TryParse(baselineVersion, out var baseline) || latest <= baseline)
            return null;

        // Construct a known official page rather than opening an arbitrary URL from the response.
        var notes = new Uri("https://github.com/Hon-Lu/OverTranslate/releases/tag/" + Uri.EscapeDataString(tag));
        return new UpstreamReleaseInfo(latest.ToString(), notes);
    }
}
