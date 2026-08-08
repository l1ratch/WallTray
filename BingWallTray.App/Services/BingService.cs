using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BingWallTray.App.Models;

namespace BingWallTray.App.Services
{
    public interface IBingService
    {
        Task<IReadOnlyList<BingImage>> GetLatestImagesAsync(string market, int count, bool useUhd);
        Task<List<BingImage>> GetHistoricalArchiveImagesAsync(string market, bool useUhd);
    }

    public class BingService : IBingService
    {
        private static readonly HttpClient _staticHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;
        private readonly ILoggingService _logger;

        public BingService(ILoggingService logger, HttpClient? httpClient = null)
        {
            _logger = logger;
            _httpClient = httpClient ?? _staticHttpClient;

            try
            {
                if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2026.8.0";
                    _httpClient.DefaultRequestHeaders.Add("User-Agent", $"BingWallTray/{version} (.NET 8 WPF Wallpaper Utility)");
                }
            }
            catch
            {
                // Игнорируем ошибки при конфигурации заголовков для кастомных клиентов в тестах
            }
        }

        public async Task<IReadOnlyList<BingImage>> GetLatestImagesAsync(string market, int count, bool useUhd)
        {
            if (count < 1) count = 1;
            if (count > 8) count = 8;

            string uhdParam = useUhd ? "&uhd=1" : "";
            string url = $"https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n={count}&mkt={market}{uhdParam}";

            _logger.LogInfo($"Запрос к Bing API: {url}");

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"Ответ Bing API получен.");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var bingResponse = JsonSerializer.Deserialize<BingResponse>(json, options);
                if (bingResponse == null || bingResponse.Images == null || bingResponse.Images.Length == 0)
                {
                    _logger.LogWarning("API Bing вернул пустой список изображений или некорректный формат.");
                    return Array.Empty<BingImage>();
                }

                var result = new List<BingImage>();
                foreach (var img in bingResponse.Images)
                {
                    if (img.Url.StartsWith("/"))
                    {
                        img.Url = "https://www.bing.com" + img.Url;
                    }
                    img.Url = CleanBingUrl(img.Url, useUhd);

                    if (img.UrlBase.StartsWith("/"))
                    {
                        img.UrlBase = "https://www.bing.com" + img.UrlBase;
                    }
                    img.UrlBase = CleanBingUrl(img.UrlBase, useUhd);

                    if (!string.IsNullOrEmpty(img.CopyrightLink) && img.CopyrightLink.StartsWith("/"))
                    {
                        img.CopyrightLink = "https://www.bing.com" + img.CopyrightLink;
                    }
                    if (!string.IsNullOrEmpty(img.Quiz) && img.Quiz.StartsWith("/"))
                    {
                        img.Quiz = "https://www.bing.com" + img.Quiz;
                    }

                    // Если название пустое или равно "Info", извлекаем его из копирайта
                    if (string.IsNullOrWhiteSpace(img.Title) || img.Title.Equals("Info", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(img.Copyright))
                        {
                            int index = img.Copyright.IndexOf(" (©");
                            if (index >= 0)
                            {
                                img.Title = img.Copyright.Substring(0, index).Trim();
                            }
                            else
                            {
                                int idx2 = img.Copyright.IndexOf(" (");
                                if (idx2 >= 0)
                                {
                                    img.Title = img.Copyright.Substring(0, idx2).Trim();
                                }
                                else
                                {
                                    img.Title = img.Copyright;
                                }
                            }
                        }
                    }

                    // Сохраняем регион, по которому производился запрос
                    img.Market = market;

                    result.Add(img);
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Сетевая ошибка при запросе к Bing API: {ex.Message}");
                return Array.Empty<BingImage>();
            }
            catch (JsonException ex)
            {
                _logger.LogError("Ошибка парсинга JSON-ответа от Bing API", ex);
                return Array.Empty<BingImage>();
            }
            catch (Exception ex)
            {
                _logger.LogError("Неизвестная ошибка при получении данных Bing", ex);
                return Array.Empty<BingImage>();
            }
        }

        public async Task<List<BingImage>> GetHistoricalArchiveImagesAsync(string market, bool useUhd)
        {
            var result = new List<BingImage>();
            string url = "https://raw.githubusercontent.com/v5tech/bing-wallpaper/main/README.md";
            _logger.LogInfo("Загрузка архива обоев с GitHub...");

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                
                var regex = new System.Text.RegularExpressions.Regex(
                    @"!\[(?<title>[^\]]*)\]\((?<thumb>[^\)]+)\)\s+(?<date>\d{4}-\d{2}-\d{2})\s+\[download 4k\]\((?<url>[^\)]+)\)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                var matches = regex.Matches(content);
                _logger.LogInfo($"Распарсено {matches.Count} обоев из архива GitHub.");

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    string titleWithCopyright = match.Groups["title"].Value;
                    string thumbUrl = match.Groups["thumb"].Value;
                    string dateStr = match.Groups["date"].Value;
                    string fullUrl = match.Groups["url"].Value;

                    string startDate = dateStr.Replace("-", "");

                    string title = titleWithCopyright;
                    string copyright = titleWithCopyright;
                    int index = titleWithCopyright.IndexOf(" (©");
                    if (index >= 0)
                    {
                        title = titleWithCopyright.Substring(0, index).Trim();
                        copyright = titleWithCopyright.Substring(index + 1).Trim(new char[] { '(', ')', ' ' });
                    }
                    else
                    {
                        int idx2 = titleWithCopyright.IndexOf(" (");
                        if (idx2 >= 0)
                        {
                            title = titleWithCopyright.Substring(0, idx2).Trim();
                            copyright = titleWithCopyright.Substring(idx2 + 1).Trim(new char[] { '(', ')', ' ' });
                        }
                    }

                    if (thumbUrl.StartsWith("/"))
                    {
                        thumbUrl = "https://www.bing.com" + thumbUrl;
                    }
                    if (fullUrl.StartsWith("/"))
                    {
                        fullUrl = "https://www.bing.com" + fullUrl;
                    }

                    thumbUrl = CleanBingUrl(thumbUrl, useUhd);
                    fullUrl = CleanBingUrl(fullUrl, useUhd);

                    result.Add(new BingImage
                    {
                        StartDate = startDate,
                        Title = title,
                        Copyright = copyright,
                        CopyrightLink = fullUrl,
                        Url = fullUrl,
                        UrlBase = fullUrl,
                        Market = market
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при получении или парсинге архива с GitHub", ex);
            }

            return result;
        }

        private string CleanBingUrl(string url, bool useUhd)
        {
            if (string.IsNullOrEmpty(url)) return url;

            if (useUhd)
            {
                url = url.Replace("_1920x1080.jpg", "_UHD.jpg");
            }

            int queryStart = url.IndexOf('?');
            if (queryStart >= 0)
            {
                string basePart = url.Substring(0, queryStart);
                string query = url.Substring(queryStart + 1);
                var parts = query.Split('&');
                var cleanParts = new List<string>();
                foreach (var part in parts)
                {
                    if (part.StartsWith("w=") || part.StartsWith("h=") || part.StartsWith("rs=") || part.StartsWith("c="))
                    {
                        continue;
                    }
                    cleanParts.Add(part);
                }
                return basePart + "?" + string.Join("&", cleanParts);
            }
            return url;
        }
    }
}
