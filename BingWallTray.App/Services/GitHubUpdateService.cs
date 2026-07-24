using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BingWallTray.App.Services
{
    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; } = false;
        public string NewVersion { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public interface IGitHubUpdateService
    {
        Task<UpdateCheckResult> CheckForUpdatesAsync(string repoOwner, string repoName);
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
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "BingWallTray-Updater/1.0.0");
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(string repoOwner, string repoName)
        {
            var result = new UpdateCheckResult();
            
            // Получаем текущую версию сборки
            var currentVer = Assembly.GetExecutingAssembly().GetName().Version;
            result.CurrentVersion = currentVer?.ToString(3) ?? "1.0.0";

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
                if (Version.TryParse(tag, out Version? latestVer))
                {
                    result.NewVersion = latestVer.ToString(3);
                    result.ReleaseUrl = releaseInfo.HtmlUrl;

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

        private class GitHubReleaseInfo
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [JsonPropertyName("html_url")]
            public string HtmlUrl { get; set; } = string.Empty;
        }
    }
}
