using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Brickwork.Core.Settings;
using NuGet.Versioning;

namespace Brickwork.App;

public sealed class UpdateCheckResult
{
    public required string CurrentVersion { get; init; }

    public bool CheckFailed { get; init; }

    public bool UpdateAvailable { get; init; }

    public bool AlreadySkipped { get; init; }

    public string? LatestVersion { get; init; }

    public string? LatestTag { get; init; }

    public string? ReleaseUrl { get; init; }

    public string? ReleaseNotes { get; init; }
}

/// <summary>Notify-only update check against the GitHub Releases "latest" endpoint.</summary>
public sealed class UpdateCheckService
{
    private static readonly HttpClient Http = CreateClient();

    private readonly AppSettingsStore _store;
    private readonly Func<string> _getCurrentVersion;

    public UpdateCheckService(AppSettingsStore? store = null, Func<string>? getCurrentVersion = null)
    {
        _store = store ?? new AppSettingsStore();
        _getCurrentVersion = getCurrentVersion ?? GitHubIssueReporter.GetAppVersion;
    }

    public async Task<UpdateCheckResult> CheckAsync(
        bool respectSkipped = true,
        CancellationToken cancellationToken = default)
    {
        var currentVersion = NormalizeVersionLabel(_getCurrentVersion());
        try
        {
            using var response = await Http
                .GetAsync(GitHubIssueReporter.ReleasesLatestApiUrl, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Failed(currentVersion);
            }

            var release = await response.Content
                .ReadFromJsonAsync<GitHubLatestRelease>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return Failed(currentVersion);
            }

            var latestTag = release.TagName.Trim();
            var latestVersion = StripLeadingV(latestTag);
            var releaseUrl = string.IsNullOrWhiteSpace(release.HtmlUrl)
                ? GitHubIssueReporter.ReleasesUrl
                : release.HtmlUrl.Trim();
            var releaseNotes = string.IsNullOrWhiteSpace(release.Body)
                ? null
                : release.Body.Trim();

            if (!IsRemoteNewer(currentVersion, latestVersion))
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion,
                    LatestTag = latestTag,
                    ReleaseUrl = releaseUrl,
                    ReleaseNotes = releaseNotes,
                };
            }

            var skipped = respectSkipped && IsSkipped(latestVersion);

            return new UpdateCheckResult
            {
                CurrentVersion = currentVersion,
                UpdateAvailable = true,
                AlreadySkipped = skipped,
                LatestVersion = latestVersion,
                LatestTag = latestTag,
                ReleaseUrl = releaseUrl,
                ReleaseNotes = releaseNotes,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Failed(currentVersion);
        }
    }

    public void SkipRelease(string? version)
    {
        var normalized = NormalizeVersionLabel(version ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized) ||
            !NuGetVersion.TryParse(normalized, out var incoming))
        {
            return;
        }

        var settings = _store.Load();
        if (NuGetVersion.TryParse(NormalizeVersionLabel(settings.SkippedUpdateVersion ?? string.Empty), out var existing) &&
            existing >= incoming)
        {
            return;
        }

        settings.SkippedUpdateVersion = normalized;
        _store.Save(settings);
    }

    internal static bool IsRemoteNewer(string currentVersion, string latestVersion)
    {
        if (!NuGetVersion.TryParse(NormalizeVersionLabel(currentVersion), out var current) ||
            !NuGetVersion.TryParse(NormalizeVersionLabel(latestVersion), out var latest))
        {
            return false;
        }

        return latest > current;
    }

    private bool IsSkipped(string latestVersion)
    {
        var skipped = _store.Load().SkippedUpdateVersion;
        if (string.IsNullOrWhiteSpace(skipped) ||
            !NuGetVersion.TryParse(NormalizeVersionLabel(skipped), out var skippedVersion) ||
            !NuGetVersion.TryParse(NormalizeVersionLabel(latestVersion), out var latest))
        {
            return false;
        }

        return latest <= skippedVersion;
    }

    private static UpdateCheckResult Failed(string currentVersion) => new()
    {
        CurrentVersion = currentVersion,
        CheckFailed = true,
    };

    private static string StripLeadingV(string tag) =>
        tag.Length > 0 && (tag[0] is 'v' or 'V') ? tag[1..] : tag;

    private static string NormalizeVersionLabel(string version)
    {
        var trimmed = version.Trim();
        var plus = trimmed.IndexOf('+');
        if (plus >= 0)
        {
            trimmed = trimmed[..plus];
        }

        return StripLeadingV(trimmed);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Brickwork");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private sealed class GitHubLatestRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }
    }
}
