using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace EnturSystray;

public static class UpdateChecker
{
    private static readonly HttpClient _httpClient = new();
    private const string ReleasesUrl = "https://api.github.com/repos/oal/entur-systray/releases/latest";

    static UpdateChecker()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EnturSystray");
    }

    public static async Task<(bool hasUpdate, string? latestVersion, string? releaseUrl)> CheckForUpdateAsync()
    {
        try
        {
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(ReleasesUrl);
            if (release?.TagName == null)
                return (false, null, null);

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (currentVersion == null)
                return (false, null, null);

            var latestVersionString = release.TagName.TrimStart('v');
            if (!Version.TryParse(latestVersionString, out var latestVersion))
                return (false, null, null);

            var hasUpdate = latestVersion > currentVersion;
            return (hasUpdate, release.TagName, release.HtmlUrl);
        }
        catch
        {
            // Fail silently - network errors shouldn't block settings
            return (false, null, null);
        }
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
    }
}
