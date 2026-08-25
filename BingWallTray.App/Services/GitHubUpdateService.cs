using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using Velopack;
using Velopack.Sources;

namespace BingWallTray.App.Services
{
    public enum UpdateStatus
    {
        Idle,
        Checking,
        UpdateAvailable,
        Downloading,
        ReadyToRestart,
        UpToDate,
        Error
    }

    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; } = false;
        public string NewVersion { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public interface IGitHubUpdateService
    {
        UpdateStatus Status { get; }
        string? NewVersion { get; }
        double DownloadProgress { get; }
        string StatusMessage { get; }
        bool IsUpdateDownloaded { get; }

        event EventHandler<UpdateStatus>? StatusChanged;
        event EventHandler<double>? ProgressChanged;

        Task<UpdateCheckResult> CheckForUpdatesAsync(string repoOwner = "l1ratch", string repoName = "WallTray", bool includePrereleases = false);
        Task<bool> DownloadUpdateAsync(Action<double>? progressCallback = null);
        void ApplyUpdateAndRestart();
    }

    public class GitHubUpdateService : IGitHubUpdateService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly ILoggingService _logger;
        
        private UpdateManager? _updateManager;
        private UpdateInfo? _latestUpdateInfo;

        public UpdateStatus Status { get; private set; } = UpdateStatus.Idle;
        public string? NewVersion { get; private set; }
        public double DownloadProgress { get; private set; } = 0.0;
        public string StatusMessage { get; private set; } = "Готов к проверке";
        public bool IsUpdateDownloaded { get; private set; } = false;

        public event EventHandler<UpdateStatus>? StatusChanged;
        public event EventHandler<double>? ProgressChanged;

        public GitHubUpdateService(ILoggingService logger)
        {
            _logger = logger;
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "26.8.0";
                _httpClient.DefaultRequestHeaders.Add("User-Agent", $"WallTray-Updater/{version}");
            }
        }

        private void SetStatus(UpdateStatus newStatus, string message)
        {
            Status = newStatus;
            StatusMessage = message;
            StatusChanged?.Invoke(this, newStatus);
        }

        private void SetProgress(double progress)
        {
            DownloadProgress = progress;
            ProgressChanged?.Invoke(this, progress);
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(string repoOwner = "l1ratch", string repoName = "WallTray", bool includePrereleases = false)
        {
            var result = new UpdateCheckResult();
            var currentVer = Assembly.GetExecutingAssembly().GetName().Version;
            result.CurrentVersion = currentVer?.ToString(3) ?? "26.8.0";

            SetStatus(UpdateStatus.Checking, "Проверка обновлений...");
            _logger.LogInfo($"Проверка обновлений (Velopack/GitHub) для {repoOwner}/{repoName} (Prereleases: {includePrereleases})");

            try
            {
                var source = new GithubSource($"https://github.com/{repoOwner}/{repoName}", null, includePrereleases);

                try
                {
                    _updateManager = new UpdateManager(source);
                }
                catch (Exception veloInitEx)
                {
                    _logger.LogWarning($"[Velopack] Локатор не найден ({veloInitEx.Message}). Приложение запущено в автономном режиме Portable/Dev.");
                    _updateManager = null;
                }

                // Если приложение установлено и управляется через Velopack
                if (_updateManager != null && _updateManager.IsInstalled)
                {
                    _latestUpdateInfo = await _updateManager.CheckForUpdatesAsync();

                    if (_latestUpdateInfo != null)
                    {
                        result.IsUpdateAvailable = true;
                        result.NewVersion = _latestUpdateInfo.TargetFullRelease.Version.ToString();
                        NewVersion = result.NewVersion;
                        SetStatus(UpdateStatus.UpdateAvailable, $"Доступна версия {NewVersion}");
                        _logger.LogInfo($"[Velopack] Доступно обновление: {result.NewVersion}");
                        return result;
                    }
                    else
                    {
                        result.NewVersion = result.CurrentVersion;
                        NewVersion = result.CurrentVersion;
                        SetStatus(UpdateStatus.UpToDate, "У вас установлена последняя версия");
                        _logger.LogInfo($"[Velopack] Установлена актуальная версия {result.CurrentVersion}.");
                        return result;
                    }
                }
                else
                {
                    // Режим разработки или запуск standalone без установщика Velopack -> фоллбэк на прямой манифест и GitHub Releases API
                    _logger.LogInfo("[Velopack] Приложение запущено в автономном режиме (Dev/Portable), используем прямой поиск обновлений.");
                    return await CheckViaGitHubApiAsync(repoOwner, repoName, includePrereleases, result);
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Ошибка проверки обновлений: {ex.Message}";
                SetStatus(UpdateStatus.Error, result.ErrorMessage);
                _logger.LogError("Ошибка при проверке обновлений", ex);
                return result;
            }
        }

        public async Task<bool> DownloadUpdateAsync(Action<double>? progressCallback = null)
        {
            if (_updateManager != null && _latestUpdateInfo != null)
            {
                try
                {
                    SetStatus(UpdateStatus.Downloading, "Скачивание обновления...");
                    _logger.LogInfo($"[Velopack] Начало загрузки обновления {_latestUpdateInfo.TargetFullRelease.Version}");

                    await _updateManager.DownloadUpdatesAsync(_latestUpdateInfo, progress =>
                    {
                        double p = progress / 100.0;
                        SetProgress(p);
                        progressCallback?.Invoke(p);
                    });

                    IsUpdateDownloaded = true;
                    SetStatus(UpdateStatus.ReadyToRestart, "Обновление готово к установке");
                    _logger.LogInfo("[Velopack] Обновление успешно скачано и распаковано в фоне.");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError("[Velopack] Ошибка при скачивании дельта-обновления", ex);
                    SetStatus(UpdateStatus.Error, $"Ошибка скачивания: {ex.Message}");
                    return false;
                }
            }
            else
            {
                _logger.LogWarning("[Velopack] UpdateManager или UpdateInfo не инициализированы.");
                return false;
            }
        }

        public void ApplyUpdateAndRestart()
        {
            if (_updateManager != null && _latestUpdateInfo != null && IsUpdateDownloaded)
            {
                _logger.LogInfo("[Velopack] Применение обновления и перезапуск приложения.");
                _updateManager.ApplyUpdatesAndRestart(_latestUpdateInfo);
            }
        }

        private async Task<UpdateCheckResult> CheckViaGitHubApiAsync(string repoOwner, string repoName, bool includePrereleases, UpdateCheckResult result)
        {
            var currentVer = Assembly.GetExecutingAssembly().GetName().Version;

            // 1. Быстрая прямая проверка releases.win.json через GitHub Releases CDN (без ограничений API)
            if (!includePrereleases)
            {
                try
                {
                    string directManifestUrl = $"https://github.com/{repoOwner}/{repoName}/releases/latest/download/releases.win.json";
                    using var manifestRequest = new HttpRequestMessage(HttpMethod.Get, directManifestUrl);
                    manifestRequest.Headers.Add("Cache-Control", "no-cache");
                    var manifestResponse = await _httpClient.SendAsync(manifestRequest);

                    if (manifestResponse.IsSuccessStatusCode)
                    {
                        string manifestJson = await manifestResponse.Content.ReadAsStringAsync();
                        var manifest = JsonSerializer.Deserialize<VelopackFeed>(manifestJson);
                        var latestAsset = manifest?.Assets?.FirstOrDefault();
                        if (latestAsset != null && !string.IsNullOrWhiteSpace(latestAsset.Version))
                        {
                            string verStr = latestAsset.Version.TrimStart('v', 'V');
                            if (Version.TryParse(verStr.Split('-')[0], out Version? parsedVer))
                            {
                                result.NewVersion = parsedVer.ToString(3);
                                result.ReleaseUrl = $"https://github.com/{repoOwner}/{repoName}/releases/tag/{latestAsset.Version}";
                                result.DownloadUrl = $"https://github.com/{repoOwner}/{repoName}/releases/download/{latestAsset.Version}/WallTray-win-Setup.exe";
                                NewVersion = result.NewVersion;

                                if (currentVer != null && parsedVer > currentVer)
                                {
                                    result.IsUpdateAvailable = true;
                                    SetStatus(UpdateStatus.UpdateAvailable, $"Доступна версия {NewVersion}");
                                    _logger.LogInfo($"[CDN] Найдено обновление: {result.NewVersion} (Текущая: {currentVer.ToString(3)})");
                                }
                                else
                                {
                                    SetStatus(UpdateStatus.UpToDate, "У вас установлена последняя версия");
                                    _logger.LogInfo($"[CDN] Установлена актуальная версия {currentVer?.ToString(3)}.");
                                }

                                return result;
                            }
                        }
                    }
                }
                catch (Exception cdnEx)
                {
                    _logger.LogWarning($"Проверка через прямой CDN-манифест: {cdnEx.Message}. Переход к GitHub API.");
                }
            }

            // 2. Фоллбэк на официальный GitHub REST API
            try
            {
                string url = includePrereleases 
                    ? $"https://api.github.com/repos/{repoOwner}/{repoName}/releases"
                    : $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "application/vnd.github.v3+json");
                request.Headers.Add("Cache-Control", "no-cache");

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    result.ErrorMessage = "Репозиторий или релизы не найдены.";
                    SetStatus(UpdateStatus.UpToDate, result.ErrorMessage);
                    return result;
                }

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                GitHubReleaseInfo? releaseInfo = null;
                if (includePrereleases)
                {
                    var releases = JsonSerializer.Deserialize<List<GitHubReleaseInfo>>(json);
                    releaseInfo = releases?.FirstOrDefault();
                }
                else
                {
                    releaseInfo = JsonSerializer.Deserialize<GitHubReleaseInfo>(json);
                }

                if (releaseInfo == null || string.IsNullOrEmpty(releaseInfo.TagName))
                {
                    result.ErrorMessage = "Не удалось получить информацию о релизе.";
                    SetStatus(UpdateStatus.Error, result.ErrorMessage);
                    return result;
                }

                string tag = releaseInfo.TagName.TrimStart('v', 'V');
                string versionPart = tag.Contains('-') ? tag.Split('-')[0] : tag;
                if (Version.TryParse(versionPart, out Version? latestVer))
                {
                    result.NewVersion = latestVer.ToString(3);
                    result.ReleaseUrl = releaseInfo.HtmlUrl;
                    var setupAsset = releaseInfo.Assets?.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                    result.DownloadUrl = setupAsset?.BrowserDownloadUrl ?? releaseInfo.HtmlUrl;
                    NewVersion = result.NewVersion;

                    if (currentVer != null && latestVer > currentVer)
                    {
                        result.IsUpdateAvailable = true;
                        SetStatus(UpdateStatus.UpdateAvailable, $"Доступна версия {NewVersion}");
                        _logger.LogInfo($"[GitHub API] Найдено обновление: {result.NewVersion} (Текущая: {currentVer.ToString(3)})");
                    }
                    else
                    {
                        SetStatus(UpdateStatus.UpToDate, "У вас установлена последняя версия");
                        _logger.LogInfo($"[GitHub API] Установлена актуальная версия {currentVer?.ToString(3)}.");
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Ошибка проверки через GitHub API: {ex.Message}";
                SetStatus(UpdateStatus.Error, result.ErrorMessage);
                _logger.LogError("Ошибка GitHub Releases API", ex);
            }

            return result;
        }

        private class VelopackFeed
        {
            [JsonPropertyName("Assets")]
            public List<VelopackFeedAsset>? Assets { get; set; }
        }

        private class VelopackFeedAsset
        {
            [JsonPropertyName("PackageId")]
            public string? PackageId { get; set; }

            [JsonPropertyName("Version")]
            public string? Version { get; set; }

            [JsonPropertyName("FileName")]
            public string? FileName { get; set; }
        }

        private class GitHubReleaseInfo
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [JsonPropertyName("html_url")]
            public string HtmlUrl { get; set; } = string.Empty;

            [JsonPropertyName("prerelease")]
            public bool Prerelease { get; set; } = false;

            [JsonPropertyName("assets")]
            public List<GitHubReleaseAsset> Assets { get; set; } = new List<GitHubReleaseAsset>();
        }

        private class GitHubReleaseAsset
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; } = string.Empty;
        }
    }
}
