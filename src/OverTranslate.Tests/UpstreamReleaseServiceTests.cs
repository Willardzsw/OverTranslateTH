using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using OverTranslate.Services;
using Xunit;

namespace OverTranslate.Tests;

public class UpstreamReleaseServiceTests
{
    private sealed class ReleaseHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Requests++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(UpstreamReleaseService.LatestReleaseApi, request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            Assert.Contains("OverTranslate-Thai-Dev/", request.Headers.UserAgent.ToString());
            Assert.Contains("application/vnd.github+json", request.Headers.Accept.ToString());
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static string Release(string? tag, bool draft = false, bool prerelease = false) =>
        JsonSerializer.Serialize(new
        {
            tag_name = tag, draft, prerelease,
            html_url = "https://untrusted.example/installer.exe",
            assets = new[] { new { browser_download_url = "https://untrusted.example/installer.exe" } },
        });

    [Fact]
    public async Task NewerRelease_RequestsOnlyPublicMetadata_AndUsesAnOfficialNotesPage()
    {
        var handler = new ReleaseHandler(Release("v2.11.0"));
        using var client = new HttpClient(handler);
        var info = await UpstreamReleaseService.FetchLatestAsync(client, "2.10.0");
        Assert.NotNull(info);
        Assert.Equal("2.11.0", info.LatestVersion);
        Assert.Equal("https://github.com/Hon-Lu/OverTranslate/releases/tag/v2.11.0", info.ReleaseNotesUri.AbsoluteUri);
        Assert.Equal(1, handler.Requests);
    }

    [Theory]
    [InlineData("2.10.0", "2.9.9", true)]
    [InlineData("v2.10.0", "2.10.0", false)]
    [InlineData("v2.9.9", "2.10.0", false)]
    [InlineData("V3.0.0", "2.10.0", true)]
    [InlineData("v2.11.0-beta.1", "2.10.0", false)]
    [InlineData("bad-tag", "2.10.0", false)]
    [InlineData(null, "2.10.0", false)]
    public async Task VersionComparison_UsesSemanticVersions_AndIgnoresUnusableTags(string? tag, string baseline, bool expected)
    {
        using var client = new HttpClient(new ReleaseHandler(Release(tag)));
        var info = await UpstreamReleaseService.FetchLatestAsync(client, baseline);
        Assert.Equal(expected, info is not null);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DraftAndPrerelease_DoNotOfferAnUpdate(bool draft, bool prerelease)
    {
        using var client = new HttpClient(new ReleaseHandler(Release("v3.0.0", draft, prerelease)));
        Assert.Null(await UpstreamReleaseService.FetchLatestAsync(client, "2.10.0"));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"tag_name\":\"v3.0.0\",\"draft\":false}")]
    public async Task IncompleteRelease_IsIgnored(string body)
    {
        using var client = new HttpClient(new ReleaseHandler(body));
        Assert.Null(await UpstreamReleaseService.FetchLatestAsync(client, "2.10.0"));
    }

    [Fact]
    public async Task NoPublishedRelease_IsNotAnUpdate()
    {
        using var client = new HttpClient(new ReleaseHandler("{}", HttpStatusCode.NotFound));
        Assert.Null(await UpstreamReleaseService.FetchLatestAsync(client, "2.10.0"));
    }

    [Fact]
    public async Task RateLimitedResponse_IsNotReportedAsAnUpToDateResult()
    {
        using var client = new HttpClient(new ReleaseHandler("{}", HttpStatusCode.Forbidden));
        await Assert.ThrowsAsync<HttpRequestException>(() => UpstreamReleaseService.FetchLatestAsync(client, "2.10.0"));
    }

    [Fact]
    public async Task MalformedResponse_IsRejected()
    {
        using var client = new HttpClient(new ReleaseHandler("invalid-json"));
        await Assert.ThrowsAnyAsync<JsonException>(() => UpstreamReleaseService.FetchLatestAsync(client, "2.10.0"));
    }

    [Fact]
    public async Task CancelledCheck_DoesNotContinue()
    {
        using var client = new HttpClient(new ReleaseHandler(Release("v3.0.0")));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UpstreamReleaseService.FetchLatestAsync(client, "2.10.0", cancel.Token));
    }

    [Theory]
    [InlineData("2.11.0", "2.11.0", true)]
    [InlineData("2.11.0", "2.12.0", true)]
    [InlineData("2.12.0", "2.11.0", false)]
    [InlineData("2.11.0", "", false)]
    [InlineData("2.11.0", "invalid", false)]
    public void Skip_AppliesOnlyUntilANewerVersion(string latest, string skipped, bool expected) =>
        Assert.Equal(expected, UpdateNotifier.IsVersionSkipped(latest, skipped));

    [Fact]
    public async Task LegacyInstallerEndpoints_AreDisabledBeforeTheyCanAccessAnyUpdate()
    {
        Assert.Null(await UpdateService.CheckAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => UpdateService.DownloadAndApplyAsync(null!));
    }
}
