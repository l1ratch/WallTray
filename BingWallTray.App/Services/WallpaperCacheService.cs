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
    public interface IWallpaperCacheService
    {
        Task<IReadOnlyList<WallpaperCacheItem>> GetAllAsync();
        Task<WallpaperCacheItem?> GetByIdAsync(string id);
        Task AddOrUpdateAsync(WallpaperCacheItem item);
        Task RemoveAsync(string id);
        Task ClearAllAsync();
        Task SaveAsync();
    }

    public class WallpaperCacheService : IWallpaperCacheService
    {
        private readonly ILoggingService _logger;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly string _cacheFilePath;
        private readonly string _appDataFolder;
        private readonly object _lock = new object();
        private List<WallpaperCacheItem> _cache = new List<WallpaperCacheItem>();
        private bool _isLoaded;

        public WallpaperCacheService(ILoggingService logger, IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dateTimeProvider = dateTimeProvider;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _appDataFolder = Path.Combine(appData, "WallTray");
            _cacheFilePath = Path.Combine(_appDataFolder, "wallpaper_cache.json");
        }

        private async Task EnsureLoadedAsync()
        {
            if (_isLoaded) return;

            if (!Directory.Exists(_appDataFolder))
            {
                try
                {
                    Directory.CreateDirectory(_appDataFolder);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Не удалось создать папку кэша", ex);
                    return;
                }
            }

            string backupPath = _cacheFilePath + ".bak";
            string filePathToLoad = _cacheFilePath;

            if (!File.Exists(_cacheFilePath) && File.Exists(backupPath))
            {
                filePathToLoad = backupPath;
                _logger.LogWarning("Основной файл кэша обоев отсутствует, используется резервная копия.");
            }
            else if (!File.Exists(_cacheFilePath))
            {
                lock (_lock)
                {
                    _cache = new List<WallpaperCacheItem>();
                    _isLoaded = true;
                }
                return;
            }

            int retryCount = 0;
            while (retryCount < 5)
            {
                try
                {
                    string json;
                    using (var reader = new StreamReader(filePathToLoad))
                    {
                        json = await reader.ReadToEndAsync();
                    }

                    var items = JsonSerializer.Deserialize<List<WallpaperCacheItem>>(json);
                    lock (_lock)
                    {
                        _cache = items ?? new List<WallpaperCacheItem>();
                        _isLoaded = true;
                    }
                    return;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError($"Файл кэша обоев {filePathToLoad} поврежден (JSON).", jsonEx);

                    if (filePathToLoad == _cacheFilePath && File.Exists(backupPath))
                    {
                        filePathToLoad = backupPath;
                        retryCount = 0;
                        continue;
                    }

                    HandleCorruptedCache();
                    break;
                }
                catch (IOException ioEx)
                {
                    retryCount++;
                    if (retryCount >= 5)
                    {
                        _logger.LogError($"Не удалось получить доступ к файлу кэша {filePathToLoad} после {retryCount} попыток.", ioEx);
                        throw;
                    }
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Критическая ошибка чтения файла кэша {filePathToLoad}", ex);
                    throw;
                }
            }
        }

        private void HandleCorruptedCache()
        {
            try
            {
                string dateStr = _dateTimeProvider.Today.ToString("yyyyMMdd_HHmmss");
                string brokenFileName = $"wallpaper_cache.broken.{dateStr}.json";
                string brokenFilePath = Path.Combine(_appDataFolder, brokenFileName);

                if (File.Exists(_cacheFilePath))
                {
                    if (File.Exists(brokenFilePath)) File.Delete(brokenFilePath);
                    File.Move(_cacheFilePath, brokenFilePath);
                    _logger.LogInfo($"Резервная копия поврежденного файла кэша сохранена как {brokenFileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Не удалось обработать поврежденный файл кэша", ex);
            }

            lock (_lock)
            {
                _cache = new List<WallpaperCacheItem>();
                _isLoaded = true;
            }
        }

        public async Task<IReadOnlyList<WallpaperCacheItem>> GetAllAsync()
        {
            await EnsureLoadedAsync();
            lock (_lock)
            {
                return _cache.ToList();
            }
        }

        public async Task<WallpaperCacheItem?> GetByIdAsync(string id)
        {
            await EnsureLoadedAsync();
            lock (_lock)
            {
                return _cache.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            }
        }

        public async Task AddOrUpdateAsync(WallpaperCacheItem item)
        {
            await EnsureLoadedAsync();
            lock (_lock)
            {
                var existing = _cache.FirstOrDefault(x => x.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Title = item.Title;
                    existing.Copyright = item.Copyright;
                    existing.CopyrightLink = item.CopyrightLink;
                    existing.Url = item.Url;
                    existing.UrlBase = item.UrlBase;
                    if (!string.IsNullOrEmpty(item.LocalPath)) existing.LocalPath = item.LocalPath;
                    if (!string.IsNullOrEmpty(item.Source)) existing.Source = item.Source;
                    if (!string.IsNullOrEmpty(item.StartDate)) existing.StartDate = item.StartDate;
                    if (!string.IsNullOrEmpty(item.Market)) existing.Market = item.Market;
                    if (item.DownloadDate.HasValue) existing.DownloadDate = item.DownloadDate;
                    if (item.LastAppliedDate.HasValue) existing.LastAppliedDate = item.LastAppliedDate;
                    if (item.ApplyCount > 0) existing.ApplyCount = item.ApplyCount;
                    if (item.FileSize > 0) existing.FileSize = item.FileSize;
                    if (!string.IsNullOrEmpty(item.Resolution)) existing.Resolution = item.Resolution;
                    existing.IsFavorite = item.IsFavorite;
                }
                else
                {
                    _cache.Add(item);
                }
            }
            await SaveAsync();
        }

        public async Task RemoveAsync(string id)
        {
            await EnsureLoadedAsync();
            bool removed = false;
            lock (_lock)
            {
                var existing = _cache.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    _cache.Remove(existing);
                    removed = true;
                }
            }
            if (removed)
            {
                await SaveAsync();
            }
        }

        public async Task ClearAllAsync()
        {
            lock (_lock)
            {
                _cache.Clear();
                _isLoaded = true;
            }
            await SaveAsync();
        }

        public async Task SaveAsync()
        {
            string tempPath = _cacheFilePath + ".tmp";
            string backupPath = _cacheFilePath + ".bak";

            string json;
            lock (_lock)
            {
                json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            }

            int retryCount = 0;
            while (retryCount < 5)
            {
                try
                {
                    using (var writer = new StreamWriter(tempPath, false))
                    {
                        await writer.WriteAsync(json);
                    }

                    if (File.Exists(_cacheFilePath))
                    {
                        if (File.Exists(backupPath)) File.Delete(backupPath);
                        File.Move(_cacheFilePath, backupPath);
                    }

                    File.Move(tempPath, _cacheFilePath);
                    return;
                }
                catch (IOException ioEx)
                {
                    retryCount++;
                    if (retryCount >= 5)
                    {
                        _logger.LogError($"Не удалось записать кэш обоев в {_cacheFilePath} после {retryCount} попыток.", ioEx);
                        throw;
                    }
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Критическая ошибка сохранения кэша обоев", ex);
                    throw;
                }
            }
        }
    }
}
