using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace CFSNetworkLauncher;

public enum UpdateStatus
{
    UpToDate,
    UpdateAvailable,
    Error
}

public sealed class UpdateCheckResult
{
    public UpdateStatus Status { get; init; }
    public string? LatestVersion { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ErrorMessage { get; init; }
}

public static class LauncherUpdater
{
    public const string CurrentVersion = "1.4.0";

    private const string AssetName = "CFSNetworkLauncher.exe";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("CFSNetworkLauncher", CurrentVersion));

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return client;
    }

    public static async Task<UpdateCheckResult> CheckAndUpdateAsync(
        string releaseApiUrl,
        string currentVersion)
    {
        try
        {
            using var response = await Http.GetAsync(releaseApiUrl);

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.Error,
                    ErrorMessage = $"GitHub HTTP hiba: {(int)response.StatusCode} {response.ReasonPhrase}"
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync();

            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.Error,
                    ErrorMessage = "A GitHub Release adatai nem értelmezhetők."
                };
            }

            var latestVersionText = NormalizeVersion(release.TagName);

            if (!Version.TryParse(latestVersionText, out var latestVersion))
            {
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.Error,
                    ErrorMessage = $"Érvénytelen GitHub verzió: {release.TagName}"
                };
            }

            if (!Version.TryParse(NormalizeVersion(currentVersion), out var installedVersion))
            {
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.Error,
                    ErrorMessage = $"Érvénytelen jelenlegi verzió: {currentVersion}"
                };
            }

            var asset = release.Assets?
                .FirstOrDefault(x =>
                    string.Equals(
                        x.Name,
                        AssetName,
                        StringComparison.OrdinalIgnoreCase));

            if (latestVersion <= installedVersion)
            {
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.UpToDate,
                    LatestVersion = latestVersion.ToString(3),
                    DownloadUrl = asset?.BrowserDownloadUrl
                };
            }

            if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            {
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.Error,
                    LatestVersion = latestVersion.ToString(3),
                    ErrorMessage =
                        $"A(z) {release.TagName} Release nem tartalmazza a(z) {AssetName} fájlt."
                };
            }

            return new UpdateCheckResult
            {
                Status = UpdateStatus.UpdateAvailable,
                LatestVersion = latestVersion.ToString(3),
                DownloadUrl = asset.BrowserDownloadUrl
            };
        }
        catch (TaskCanceledException)
        {
            return new UpdateCheckResult
            {
                Status = UpdateStatus.Error,
                ErrorMessage = "A GitHub lekérés időtúllépés miatt megszakadt."
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                Status = UpdateStatus.Error,
                ErrorMessage = ex.Message
            };
        }
    }

    public static async Task DownloadAndInstallAsync(string downloadUrl)
    {
        var currentExe = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(currentExe))
        {
            throw new InvalidOperationException(
                "Nem sikerült meghatározni a launcher EXE elérési útját.");
        }

        var currentDirectory = Path.GetDirectoryName(currentExe);

        if (string.IsNullOrWhiteSpace(currentDirectory))
        {
            throw new InvalidOperationException(
                "Nem sikerült meghatározni a launcher mappáját.");
        }

        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "CFSNetworkLauncher",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempDirectory);

        var downloadedExe = Path.Combine(
            tempDirectory,
            AssetName);

        using (var response = await Http.GetAsync(
                   downloadUrl,
                   HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync();
            await using var destination = File.Create(downloadedExe);

            await source.CopyToAsync(destination);
        }

        var updateScript = Path.Combine(
            tempDirectory,
            "update.cmd");

        var script = $"""
@echo off
setlocal

set "NEW_EXE={EscapeCmd(downloadedExe)}"
set "TARGET_EXE={EscapeCmd(currentExe)}"
set "TARGET_DIR={EscapeCmd(currentDirectory)}"

:WAIT
timeout /t 1 /nobreak >nul
tasklist /FI "IMAGENAME eq CFSNetworkLauncher.exe" | find /I "CFSNetworkLauncher.exe" >nul
if not errorlevel 1 goto WAIT

copy /Y "%NEW_EXE%" "%TARGET_EXE%" >nul
if errorlevel 1 goto FAILED

start "" "%TARGET_EXE%"
rmdir /S /Q "{EscapeCmd(tempDirectory)}" >nul 2>&1
exit /b 0

:FAILED
msg * "A CFS Network Launcher frissítése nem sikerült. Indítsd újra a launchert, majd próbáld meg ismét."
exit /b 1
""";

        await File.WriteAllTextAsync(
            updateScript,
            script,
            new System.Text.UTF8Encoding(false));

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{updateScript}\"",
            WorkingDirectory = tempDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process.Start(psi);

        Environment.Exit(0);
    }

    private static string NormalizeVersion(string version)
    {
        version = version.Trim();

        if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            version = version[1..];
        }

        return version;
    }

    private static string EscapeCmd(string value)
    {
        return value.Replace("\"", "\"\"");
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
