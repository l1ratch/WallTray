using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using BingWallTray.App.Models;

namespace BingWallTray.App.Services
{
    public interface IWallhavenService
    {
        Task<List<BingImage>> GetWallhavenImagesAsync(string query, string categories, string resolutions);
    }

    public class WallhavenService : IWallhavenService
    {
        private readonly ILoggingService _logger;
        private readonly HttpClient _httpClient;

        public WallhavenService(ILoggingService logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            // Настройка заголовков, чтобы избежать блокировок User-Agent
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<List<BingImage>> GetWallhavenImagesAsync(string query, string categories, string resolutions)
        {
            var list = new List<BingImage>();
            try
            {
                // Категории: по умолчанию "110" (General=1, Anime=1, People=0)
                string cats = string.IsNullOrEmpty(categories) ? "110" : categories;
                
                // Разрешения: если пусто, используем дефолтные
                string res = string.IsNullOrEmpty(resolutions) ? "1920x1080,2560x1440,3840x2160" : resolutions;
                
                // sorting=random для бесконечной ленты новых картинок
                string url = $"https://wallhaven.cc/api/v1/search?sorting=random&resolutions={res}&categories={cats}&purity=100";
                if (!string.IsNullOrEmpty(query))
                {
                    url += $"&q={Uri.EscapeDataString(query)}";
                }

                _logger.LogInfo($"Запрос к Wallhaven API: {url}");
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                string json = await response.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in dataArray.EnumerateArray())
                        {
                            try
                            {
                                string id = item.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N").Substring(0, 6);
                                string path = item.GetProperty("path").GetString() ?? string.Empty;
                                
                                // Пробуем взять превью, если нет - то само изображение
                                string thumb = path;
                                if (item.TryGetProperty("thumbs", out var thumbsObj))
                                {
                                    if (thumbsObj.TryGetProperty("large", out var largeThumb))
                                    {
                                        thumb = largeThumb.GetString() ?? path;
                                    }
                                }

                                if (!string.IsNullOrEmpty(path))
                                {
                                    list.Add(new BingImage
                                    {
                                        StartDate = DateTime.Today.ToString("yyyyMMdd"),
                                        Title = $"Wallhaven {id}",
                                        Copyright = $"Изображение Wallhaven (ID: {id})",
                                        CopyrightLink = $"https://wallhaven.cc/w/{id}",
                                        Url = path,
                                        ThumbnailUrl = thumb,
                                        PreviewUrl = thumb,
                                        IsFavorite = false,
                                        IsApplied = false
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Ошибка при разборе элемента Wallhaven: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при получении изображений из Wallhaven", ex);
            }
            return list;
        }
    }
}
