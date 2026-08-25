using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BingWallTray.App.Models;
using BingWallTray.App.Utils;

namespace BingWallTray.App.Services
{
    public interface IHistoryService
    {
        Task AddOrUpdateFavoriteAsync(WallpaperHistoryItem item);
        Task RemoveFavoriteAsync(string id);
        Task<IReadOnlyList<WallpaperHistoryItem>> GetFavoritesAsync();
        Task CleanOldNonFavoriteImagesAsync(string downloadFolder, string currentAppliedPath);
        Task ClearCacheAsync();
        Task<int> GetTotalCacheCountAsync();
        Task<int> GetDownloadedCacheCountAsync();
        Task<long> GetDownloadedCacheSizeAsync();
        Task AddToCacheAsync(BingImage image, string source, bool isApplied = false);
    }

    public class HistoryService : IHistoryService
    {
        private readonly ILoggingService _logger;
        private readonly ISettingsService _settingsService;
        private readonly IWallpaperCacheService _cacheService;
        private readonly string _favoritesFilePath;

        public HistoryService(ILoggingService logger, ISettingsService settingsService, IWallpaperCacheService cacheService)
        {
            _logger = logger;
            _settingsService = settingsService;
            _cacheService = cacheService;

            _favoritesFilePath = AppPaths.FavoritesFilePath;
        }

        private async Task EnsureMigratedAsync()
        {
            if (File.Exists(_favoritesFilePath))
            {
                _logger.LogInfo("Обнаружен старый файл favorites.json. Запущена миграция в новую базу кэша...");
                try
                {
                    string json;
                    using (var reader = new StreamReader(_favoritesFilePath))
                    {
                        json = await reader.ReadToEndAsync();
                    }

                    var items = JsonSerializer.Deserialize<List<WallpaperHistoryItem>>(json);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            var cacheItem = MapToCacheItem(item);
                            cacheItem.IsFavorite = true;
                            await _cacheService.AddOrUpdateAsync(cacheItem);
                        }
                    }

                    string migratedPath = _favoritesFilePath + ".migrated";
                    if (File.Exists(migratedPath)) File.Delete(migratedPath);
                    File.Move(_favoritesFilePath, migratedPath);

                    string backupPath = _favoritesFilePath + ".bak";
                    if (File.Exists(backupPath)) File.Delete(backupPath);

                    _logger.LogInfo("Миграция избранного успешно завершена!");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Ошибка при миграции старого списка избранного", ex);
                }
            }
        }

        private WallpaperHistoryItem MapToHistoryItem(WallpaperCacheItem cacheItem)
        {
            string localPath = cacheItem.LocalPath;
            if (!string.IsNullOrEmpty(localPath) &&
                (localPath.Contains(@"\OneDrive\", StringComparison.OrdinalIgnoreCase) ||
                 localPath.Contains(@"\Pictures\", StringComparison.OrdinalIgnoreCase) ||
                 localPath.Contains(@"\Изображения\", StringComparison.OrdinalIgnoreCase)))
            {
                string fileName = Path.GetFileName(localPath);
                string newPath = Path.Combine(AppPaths.DefaultWallpapersFolder, fileName);
                if (File.Exists(newPath))
                {
                    localPath = newPath;
                }
                else if (File.Exists(localPath))
                {
                    try
                    {
                        AppPaths.EnsureDirectoryExists(AppPaths.DefaultWallpapersFolder);
                        File.Copy(localPath, newPath, true);
                        localPath = newPath;
                    }
                    catch
                    {
                        localPath = string.Empty;
                    }
                }
                else
                {
                    localPath = string.Empty;
                }
            }

            return new WallpaperHistoryItem
            {
                Id = cacheItem.Id,
                Title = cacheItem.Title,
                Copyright = cacheItem.Copyright,
                CopyrightLink = cacheItem.CopyrightLink,
                RemoteUrl = cacheItem.Url,
                LocalPath = localPath,
                Date = cacheItem.StartDate,
                IsFavorite = cacheItem.IsFavorite,
                DisplayPath = (!string.IsNullOrEmpty(localPath) && File.Exists(localPath)) ? localPath : cacheItem.Url
            };
        }

        private WallpaperCacheItem MapToCacheItem(WallpaperHistoryItem item)
        {
            return new WallpaperCacheItem
            {
                Id = item.Id,
                Title = item.Title,
                Copyright = item.Copyright,
                CopyrightLink = item.CopyrightLink,
                Url = item.RemoteUrl,
                LocalPath = item.LocalPath,
                StartDate = item.Date,
                IsFavorite = item.IsFavorite,
                Source = "Favorites"
            };
        }

        public async Task AddOrUpdateFavoriteAsync(WallpaperHistoryItem item)
        {
            await EnsureMigratedAsync();
            var cacheItem = MapToCacheItem(item);
            cacheItem.IsFavorite = true;
            await _cacheService.AddOrUpdateAsync(cacheItem);
            _logger.LogInfo($"Обои добавлены в избранное через кэш-сервис: {item.Id}");
        }

        public async Task RemoveFavoriteAsync(string id)
        {
            await EnsureMigratedAsync();
            var existing = await _cacheService.GetByIdAsync(id);
            if (existing != null)
            {
                existing.IsFavorite = false;
                await _cacheService.AddOrUpdateAsync(existing);
                _logger.LogInfo($"Обои удалены из избранного в кэш-сервисе: {id}");

                // Удаляем локальный файл, если он не нужен для других целей
                if (!string.IsNullOrEmpty(existing.LocalPath) && File.Exists(existing.LocalPath))
                {
                    try
                    {
                        File.Delete(existing.LocalPath);
                        _logger.LogInfo($"Файл обоев удален с диска: {existing.LocalPath}");
                        existing.LocalPath = string.Empty;
                        await _cacheService.AddOrUpdateAsync(existing);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Не удалось удалить файл обоев {existing.LocalPath}: {ex.Message}");
                    }
                }
            }
        }

        public async Task<IReadOnlyList<WallpaperHistoryItem>> GetFavoritesAsync()
        {
            await EnsureMigratedAsync();
            var all = await _cacheService.GetAllAsync();
            return all.Where(x => x.IsFavorite)
                      .Select(MapToHistoryItem)
                      .OrderByDescending(f => f.Date)
                      .ToList()
                      .AsReadOnly();
        }

        public async Task CleanOldNonFavoriteImagesAsync(string downloadFolder, string currentAppliedPath)
        {
            if (!Directory.Exists(downloadFolder)) return;

            var settings = _settingsService.CurrentSettings;
            if (!settings.DeleteOldImages) return;

            await EnsureMigratedAsync();
            var all = await _cacheService.GetAllAsync();

            var protectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in all.Where(x => x.IsFavorite && !string.IsNullOrEmpty(x.LocalPath)))
                protectedPaths.Add(Path.GetFullPath(item.LocalPath));

            if (!string.IsNullOrEmpty(currentAppliedPath))
                protectedPaths.Add(Path.GetFullPath(currentAppliedPath));

            try
            {
                var files = Directory.GetFiles(downloadFolder, "*.*")
                    .Where(s => s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                s.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                // Первые KeepLastImages файлов (не-избранных) оставляем, остальные удаляем
                int kept = 0;
                foreach (var fi in files)
                {
                    string fullPath = Path.GetFullPath(fi.FullName);
                    if (protectedPaths.Contains(fullPath))
                        continue;

                    if (kept < settings.KeepLastImages)
                    {
                        kept++;
                        continue;
                    }

                    try
                    {
                        fi.Delete();
                        _logger.LogInfo($"Автоочистка: удален старый файл обоев: {fi.FullName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Автоочистка: не удалось удалить файл {fi.FullName}. Ошибка: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при выполнении автоочистки файлов обоев", ex);
            }
        }

        public async Task ClearCacheAsync()
        {
            await EnsureMigratedAsync();
            var all = await _cacheService.GetAllAsync();

            // Сохраняем пути избранных обоев и текущих активных обоев от удаления
            var protectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fav in all.Where(x => x.IsFavorite && !string.IsNullOrEmpty(x.LocalPath)))
            {
                try
                {
                    protectedPaths.Add(Path.GetFullPath(fav.LocalPath));
                }
                catch { }
            }

            var settings = _settingsService.CurrentSettings;
            if (!string.IsNullOrEmpty(settings.LastAppliedImageId))
            {
                var applied = all.FirstOrDefault(x => x.Id == settings.LastAppliedImageId);
                if (applied != null && !string.IsNullOrEmpty(applied.LocalPath))
                {
                    try
                    {
                        protectedPaths.Add(Path.GetFullPath(applied.LocalPath));
                    }
                    catch { }
                }
            }

            // Удаляем только файлы и записи, которые НЕ находятся в избранном
            foreach (var item in all)
            {
                if (item.IsFavorite)
                {
                    // Избранные записи строго сохраняются!
                    continue;
                }

                if (!string.IsNullOrEmpty(item.LocalPath) && File.Exists(item.LocalPath))
                {
                    try
                    {
                        string full = Path.GetFullPath(item.LocalPath);
                        if (!protectedPaths.Contains(full))
                        {
                            File.Delete(item.LocalPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Ошибка очистки кэша для файла {item.LocalPath}: {ex.Message}");
                    }
                }

                await _cacheService.RemoveAsync(item.Id);
            }

            // Очищаем временный файл кэша дня
            try
            {
                if (File.Exists(AppPaths.TodayCacheFilePath))
                {
                    File.Delete(AppPaths.TodayCacheFilePath);
                }
            }
            catch { }

            _logger.LogInfo("Кэш обоев успешно очищен. Все элементы избранного сохранены.");
        }

        public async Task<int> GetTotalCacheCountAsync()
        {
            var all = await _cacheService.GetAllAsync();
            return all.Count;
        }

        public async Task<int> GetDownloadedCacheCountAsync()
        {
            var all = await _cacheService.GetAllAsync();
            return all.Count(x => !string.IsNullOrEmpty(x.LocalPath) && File.Exists(x.LocalPath));
        }

        public async Task<long> GetDownloadedCacheSizeAsync()
        {
            var all = await _cacheService.GetAllAsync();
            long total = 0;
            foreach (var x in all)
            {
                if (!string.IsNullOrEmpty(x.LocalPath) && File.Exists(x.LocalPath))
                {
                    try
                    {
                        var fi = new FileInfo(x.LocalPath);
                        total += fi.Length;
                    }
                    catch { }
                }
            }
            return total;
        }

        public async Task AddToCacheAsync(BingImage image, string source, bool isApplied = false)
        {
            await EnsureMigratedAsync();
            string id = source == "Wallhaven"
                ? "Wallhaven_" + Path.GetFileNameWithoutExtension(image.Url)
                : $"{image.StartDate}_{image.Market}";
            var item = await _cacheService.GetByIdAsync(id);

            if (item == null)
            {
                item = new WallpaperCacheItem
                {
                    Id = id,
                    Title = image.Title,
                    Copyright = image.Copyright,
                    CopyrightLink = image.CopyrightLink,
                    Url = image.Url,
                    UrlBase = image.UrlBase,
                    StartDate = image.StartDate,
                    Market = image.Market,
                    Source = source
                };
            }

            if (isApplied)
            {
                item.LastAppliedDate = DateTime.Now;
                item.ApplyCount++;
            }

            // Проверяем, скачан ли файл локально
            string targetFolder = _settingsService.CurrentSettings.DownloadFolder;
            if (string.IsNullOrWhiteSpace(targetFolder) ||
                targetFolder.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                targetFolder.Contains("Pictures", StringComparison.OrdinalIgnoreCase) ||
                targetFolder.Contains("Изображения", StringComparison.OrdinalIgnoreCase))
            {
                targetFolder = AppPaths.DefaultWallpapersFolder;
            }
            string titleTmp = string.IsNullOrWhiteSpace(image.Title) ? "bing-wallpaper" : image.Title.Trim();
            string sanitizedTitleTmp = Utils.FileNameSanitizer.Sanitize(titleTmp);
            string marketTmp = string.IsNullOrWhiteSpace(image.Market) ? "unknown" : image.Market;
            string expectedPath = Path.Combine(targetFolder, $"{image.StartDate}_{marketTmp}_{sanitizedTitleTmp}.jpg");

            if (File.Exists(expectedPath))
            {
                item.LocalPath = expectedPath;
                try
                {
                    var fi = new FileInfo(expectedPath);
                    item.FileSize = fi.Length;
                }
                catch { }
            }

            await _cacheService.AddOrUpdateAsync(item);
        }
    }
}
