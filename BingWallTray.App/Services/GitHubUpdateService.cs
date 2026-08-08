using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BingWallTray.App.Services
{
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
        Task<UpdateCheckResult> CheckForUpdatesAsync(string repoOwner, string repoName);
        Task<string?> DownloadUpdateAsync(string downloadUrl, Action<double>? progressCallback = null);
    }

    public class GitHubUpdateService : IGitHubUpdateService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly ILoggingService _logger;

        public GitHubUpdateService(ILoggingService logger)
        {
            _logger = logger;
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2026.8.0";
                _httpClient.DefaultRequestHeaders.Add("User-Agent", $"BingWallTray-Updater/{version}");
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(string repoOwner, string repoName)
        {
            var result = new UpdateCheckResult();
            
            // Получаем текущую версию сборки
            var currentVer = Assembly.GetExecutingAssembly().GetName().Version;
            result.CurrentVersion = currentVer?.ToString(3) ?? "2026.8.0";

            string url = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";
            _logger.LogInfo($"Проверка обновлений на GitHub по адресу: {url}");

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    result.ErrorMessage = "Репозиторий не найден или нет опубликованных релизов.";
                    _logger.LogWarning($"Проверка обновлений: {result.ErrorMessage}");
                    return result;
                }

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                var releaseInfo = JsonSerializer.Deserialize<GitHubReleaseInfo>(json);
                if (releaseInfo == null || string.IsNullOrEmpty(releaseInfo.TagName))
                {
                    result.ErrorMessage = "GitHub API вернул некорректные данные о релизе.";
                    _logger.LogWarning($"Проверка обновлений: {result.ErrorMessage}");
                    return result;
                }

                string tag = releaseInfo.TagName.TrimStart('v', 'V');
                string versionPart = tag.Contains('-') ? tag.Split('-')[0] : tag;
                if (Version.TryParse(versionPart, out Version? latestVer))
                {
                    result.NewVersion = latestVer.ToString(3);
                    result.ReleaseUrl = releaseInfo.HtmlUrl;

                    // Ищем установщик в прикрепленных ассетах релиза
                    if (releaseInfo.Assets != null)
                    {
                        var setupAsset = releaseInfo.Assets.FirstOrDefault(a => a.Name.Equals("WallTraySetup.exe", StringComparison.OrdinalIgnoreCase));
                        if (setupAsset != null)
                        {
                            result.DownloadUrl = setupAsset.BrowserDownloadUrl;
                        }
                    }

                    if (currentVer != null && latestVer > currentVer)
                    {
                        result.IsUpdateAvailable = true;
                        _logger.LogInfo($"Доступно обновление: {result.NewVersion} (текущая версия: {result.CurrentVersion})");
                    }
                    else
                    {
                        _logger.LogInfo($"Обновления отсутствуют. Текущая версия: {result.CurrentVersion}, последняя на GitHub: {result.NewVersion}");
                    }
                }
                else
                {
                    result.ErrorMessage = $"Не удалось распознать версию тега релиза: {releaseInfo.TagName}";
                    _logger.LogWarning($"Проверка обновлений: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Ошибка сети при проверке обновлений: {ex.Message}";
                _logger.LogError("Ошибка при проверке обновлений на GitHub", ex);
            }

            return result;
        }

        public async Task<string?> DownloadUpdateAsync(string downloadUrl, Action<double>? progressCallback = null)
        {
            try
            {
                _logger.LogInfo($"Начало скачивания обновления: {downloadUrl}");
                
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    long? totalBytes = response.Content.Headers.ContentLength;
                    string tempPath = Path.Combine(Path.GetTempPath(), "WallTraySetup.exe");

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int read;

                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            if (totalBytes.HasValue && progressCallback != null)
                            {
                                double progress = (double)totalRead / totalBytes.Value;
                                progressCallback(progress);
                            }
                        }
                    }

                    _logger.LogInfo($"Обновление успешно скачано в {tempPath}");
                    return tempPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при скачивании обновления", ex);
                return null;
            }
        }

        private class GitHubReleaseInfo
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [JsonPropertyName("html_url")]
            public string HtmlUrl { get; set; } = string.Empty;

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
