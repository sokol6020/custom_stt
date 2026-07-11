using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace customSTT.Services;

public sealed class UpdateInfo
{
    public required Version Version { get; init; }
    public required string TagName { get; init; }
    public required string DownloadUrl { get; init; }
    public string Notes { get; init; } = "";
    public string HtmlUrl { get; init; } = "";
}

public class UpdateService : IDisposable
{
    private const string DefaultRepo = "sokol6020/custom_stt";

    private readonly HttpClient _httpClient;
    private readonly string _repo;

    public UpdateService(string? repo = null)
    {
        _repo = string.IsNullOrWhiteSpace(repo) ? DefaultRepo : repo;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("customSTT", AppVersion.Current));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    /// <summary>
    /// Проверяет последний релиз на GitHub. Обновление предлагается, если дата публикации
    /// latest-релиза новее даты публикации релиза текущей версии.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var latestRelease = await GetLatestReleaseAsync(cancellationToken);
        if (latestRelease == null || !IsPublishableRelease(latestRelease))
            return null;

        var currentTag = NormalizeTag($"v{AppVersion.Current}");
        var latestTag = NormalizeTag(latestRelease.TagName);
        if (TagsEqual(latestTag, currentTag))
            return null;

        var latestPublishedAt = ParsePublishedAt(latestRelease.PublishedAt);
        if (latestPublishedAt == null)
            return null;

        var currentRelease = await GetReleaseByTagAsync(currentTag, cancellationToken);
        if (currentRelease == null)
            return CreateUpdateInfo(latestRelease);

        var currentPublishedAt = ParsePublishedAt(currentRelease.PublishedAt);
        if (currentPublishedAt == null)
            return CreateUpdateInfo(latestRelease);

        if (latestPublishedAt > currentPublishedAt)
            return CreateUpdateInfo(latestRelease);

        return null;
    }

    /// <summary>
    /// Скачивает архив обновления во временную папку. Возвращает путь к zip.
    /// </summary>
    public async Task<string> DownloadUpdateAsync(
        UpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var updateRoot = GetUpdateRoot();
        Directory.CreateDirectory(updateRoot);

        var zipPath = Path.Combine(updateRoot, $"customSTT-{update.Version}.zip");
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        using var response = await _httpClient.GetAsync(
            update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(zipPath);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;

            if (totalBytes > 0)
                progress?.Report(Math.Min(99.0, totalRead * 100.0 / totalBytes));
        }

        progress?.Report(100);
        return zipPath;
    }

    /// <summary>
    /// Распаковывает архив, запускает внешний скрипт обновления и завершает приложение.
    /// </summary>
    public void ApplyUpdateAndRestart(string zipPath)
    {
        var updateRoot = GetUpdateRoot();
        var extractDir = Path.Combine(updateRoot, "extracted");

        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, true);
        Directory.CreateDirectory(extractDir);

        ZipFile.ExtractToDirectory(zipPath, extractDir, true);

        var appDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var exePath = Process.GetCurrentProcess().MainModule?.FileName
                      ?? Path.Combine(appDir, "customSTT.exe");
        var pid = Environment.ProcessId;

        var scriptPath = Path.Combine(updateRoot, "apply-update.bat");
        var script = BuildUpdaterScript(pid, extractDir, appDir, exePath, updateRoot);
        File.WriteAllText(scriptPath, script, new UTF8Encoding(false));

        var startInfo = new ProcessStartInfo
        {
            FileName = scriptPath,
            WorkingDirectory = updateRoot,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process.Start(startInfo);
    }

    private async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{_repo}/releases/latest";
        return await FetchReleaseAsync(url, cancellationToken);
    }

    private async Task<GitHubRelease?> GetReleaseByTagAsync(string tag, CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{_repo}/releases/tags/{Uri.EscapeDataString(tag)}";
        return await FetchReleaseAsync(url, cancellationToken, notFoundIsOk: true);
    }

    private async Task<GitHubRelease?> FetchReleaseAsync(
        string url,
        CancellationToken cancellationToken,
        bool notFoundIsOk = false)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (notFoundIsOk && response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<GitHubRelease>(json);
    }

    private UpdateInfo? CreateUpdateInfo(GitHubRelease release)
    {
        var version = ParseVersion(release.TagName ?? string.Empty);
        if (version == null)
            return null;

        var asset = release.Assets?.FirstOrDefault(a =>
            a.Name != null &&
            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
            a.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase));

        asset ??= release.Assets?.FirstOrDefault(a =>
            a.Name != null && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        if (asset?.BrowserDownloadUrl == null)
            return null;

        return new UpdateInfo
        {
            Version = version,
            TagName = release.TagName ?? "",
            DownloadUrl = asset.BrowserDownloadUrl,
            Notes = release.Body ?? "",
            HtmlUrl = release.HtmlUrl ?? ""
        };
    }

    private static bool IsPublishableRelease(GitHubRelease release) =>
        !release.Draft
        && !release.Prerelease
        && !string.IsNullOrWhiteSpace(release.TagName);

    private static DateTimeOffset? ParsePublishedAt(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private static string NormalizeTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return string.Empty;

        var trimmed = tag.Trim();
        return trimmed.StartsWith('v') || trimmed.StartsWith('V')
            ? $"v{trimmed[1..]}"
            : $"v{trimmed}";
    }

    private static bool TagsEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string BuildUpdaterScript(int pid, string extractDir, string appDir, string exePath, string updateRoot)
    {
        var logPath = Path.Combine(updateRoot, "update.log");

        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("chcp 65001 >nul");
        sb.AppendLine("setlocal");
        sb.AppendLine($"set \"LOG={logPath}\"");
        sb.AppendLine("echo [%date% %time%] Updater started > \"%LOG%\"");

        sb.AppendLine("set /a WAITED=0");
        sb.AppendLine(":waitloop");
        sb.AppendLine($"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul");
        sb.AppendLine("if not errorlevel 1 (");
        sb.AppendLine("  timeout /t 1 /nobreak >nul");
        sb.AppendLine("  set /a WAITED+=1");
        sb.AppendLine("  if %WAITED% lss 30 goto waitloop");
        sb.AppendLine(")");

        sb.AppendLine("timeout /t 2 /nobreak >nul");
        sb.AppendLine($"echo [%date% %time%] Copying files to \"{appDir}\" >> \"%LOG%\"");

        sb.AppendLine($"robocopy \"{extractDir}\" \"{appDir}\" /E /IS /IT /R:5 /W:2 /NP >> \"%LOG%\" 2>&1");
        sb.AppendLine("set \"RC=%ERRORLEVEL%\"");
        sb.AppendLine($"echo [%date% %time%] robocopy exit code %RC% >> \"%LOG%\"");

        sb.AppendLine("if %RC% GEQ 8 (");
        sb.AppendLine($"  echo [%date% %time%] Copy failed, aborting restart >> \"%LOG%\"");
        sb.AppendLine("  goto cleanup");
        sb.AppendLine(")");

        sb.AppendLine($"echo [%date% %time%] Starting \"{exePath}\" >> \"%LOG%\"");
        sb.AppendLine($"start \"\" /D \"{appDir}\" \"{exePath}\"");

        sb.AppendLine(":cleanup");
        sb.AppendLine($"rmdir /S /Q \"{extractDir}\" >nul 2>&1");
        sb.AppendLine("(goto) 2>nul & del \"%~f0\"");
        return sb.ToString();
    }

    private static string GetUpdateRoot() =>
        Path.Combine(Path.GetTempPath(), "customSTT-update");

    private static Version? ParseVersion(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var cleaned = raw.Trim().TrimStart('v', 'V');
        var plusIndex = cleaned.IndexOfAny(new[] { '-', '+' });
        if (plusIndex > 0)
            cleaned = cleaned[..plusIndex];

        return Version.TryParse(cleaned, out var version) ? version : null;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("published_at")]
        public string? PublishedAt { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
