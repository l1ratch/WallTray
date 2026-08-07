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
    }

    public class HistoryService : IHistoryService
    {
        private readonly ILoggingService _logger;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly string _favoritesFilePath;
        private readonly string _appDataFolder;
        private readonly object _lock = new object();
        private List<WallpaperHistoryItem> _favorites = new List<WallpaperHistoryItem>();

        public HistoryService(ILoggingService logger, IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dateTimeProvider = dateTimeProvider;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _appDataFolder = Path.Combine(appData, "WallTray");
            _favoritesFilePath = Path.Combine(_appDataFolder, "favorites.json");
        }

        private async Task LoadFavoritesAsync()
        {
            if (!Directory.Exists(_appDataFolder))
            {
                try
                {
                    Directory.CreateDirectory(_appDataFolder);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Не удалось создать папку настроек/избранного", ex);
                    return;
                }
            }

            string backupPath = _favoritesFilePath + ".bak";
            string filePathToLoad = _favoritesFilePath;

            // Если основной файл отсутствует, но есть бэкап, пробуем бэкап
            if (!File.Exists(_favoritesFilePath) && File.Exists(backupPath))
            {
                filePathToLoad = backupPath;
                _logger.LogWarning("Основной файл избранного отсутствует, используется резервная копия.");
            }
            else if (!File.Exists(_favoritesFilePath))
            {
                lock (_lock)
                {
                    _favorites = new List<WallpaperHistoryItem>();
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

                    var items = JsonSerializer.Deserialize<List<WallpaperHistoryItem>>(json);
                    lock (_lock)
                    {
                        _favorites = items ?? new List<WallpaperHistoryItem>();
                    }
                    return; // Успешно прочитано!
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError($"Файл избранного {filePathToLoad} поврежден (JSON).", jsonEx);

                    // Если основной файл поврежден, пробуем бэкап
                    if (filePathToLoad == _favoritesFilePath && File.Exists(backupPath))
                    {
                        _logger.LogWarning("Попытка загрузки резервной копии избранного...");
                        filePathToLoad = backupPath;
                        retryCount = 0; // Сбрасываем попытки для бэкапа
                        continue;
                    }

                    // Если бэкап тоже поврежден или отсутствует
                    HandleCorruptedFavorites();
                    break;
                }
                catch (IOException ioEx)
                {
                    retryCount++;
                    if (retryCount >= 5)
                    {
                        _logger.LogError($"Не удалось получить доступ к файлу избранного {filePathToLoad} после {retryCount} попыток.", ioEx);
                        throw; // Пробрасываем ошибку дальше, не сбрасывая список на пустой!
                    }
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Критическая ошибка чтения файла избранного {filePathToLoad}", ex);
                    throw;
                }
            }
        }

        private void HandleCorruptedFavorites()
        {
            try
            {
                string dateStr = _dateTimeProvider.Today.ToString("yyyyMMdd_HHmmss");
                string brokenFileName = $"favorites.broken.{dateStr}.json";
                string brokenFilePath = Path.Combine(_appDataFolder, brokenFileName);

                if (File.Exists(_favoritesFilePath))
                {
                    if (File.Exists(brokenFilePath)) File.Delete(brokenFilePath);
                    File.Move(_favoritesFilePath, brokenFilePath);
                    _logger.LogInfo($"Резервная копия поврежденного файла избранного сохранена как {brokenFileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Не удалось обработать поврежденный файл избранного", ex);
            }

            lock (_lock)
            {
                _favorites = new List<WallpaperHistoryItem>();
            }
        }

        private async Task SaveFavoritesAsync()
        {
            string tempPath = _favoritesFilePath + ".tmp";
            string backupPath = _favoritesFilePath + ".bak";

            try
            {
                if (!Directory.Exists(_appDataFolder))
                {
                    Directory.CreateDirectory(_appDataFolder);
                }

                string json;
                lock (_lock)
                {
                    _favorites = _favorites.OrderByDescending(f => f.Date).ToList();
                    json = JsonSerializer.Serialize(_favorites, new JsonSerializerOptions { WriteIndented = true });
                }

                // Пишем во временный файл
                using (var writer = new StreamWriter(tempPath, false))
                {
                    await writer.WriteAsync(json);
                }

                // Создаем бэкап перед заменой (если старый файл существует)
                if (File.Exists(_favoritesFilePath))
                {
                    try
                    {
                        File.Copy(_favoritesFilePath, backupPath, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Не удалось создать резервную копию перед записью: {ex.Message}");
                    }
                }

                // Атомарно перемещаем временный файл на место основного
                if (File.Exists(_favoritesFilePath))
                {
                    File.Delete(_favoritesFilePath);
                }
                File.Move(tempPath, _favoritesFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при сохранении списка избранного", ex);

                // Пытаемся прибраться
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        public async Task AddOrUpdateFavoriteAsync(WallpaperHistoryItem item)
        {
            await LoadFavoritesAsync();

            lock (_lock)
            {
                var existing = _favorites.FirstOrDefault(f => f.Id == item.Id);
                if (existing != null)
                {
                    existing.Title = item.Title;
                    existing.Copyright = item.Copyright;
                    existing.CopyrightLink = item.CopyrightLink;
                    existing.RemoteUrl = item.RemoteUrl;
                    existing.LocalPath = item.LocalPath;
                    existing.IsFavorite = true;
                }
                else
                {
                    item.IsFavorite = true;
                    _favorites.Add(item);
                }
            }

            await SaveFavoritesAsync();
            _logger.LogInfo($"Обои добавлены в избранное: {item.Id}");
        }

        public async Task RemoveFavoriteAsync(string id)
        {
            await LoadFavoritesAsync();

            string? fileToDelete = null;
            lock (_lock)
            {
                var existing = _favorites.FirstOrDefault(f => f.Id == id);
                if (existing != null)
                {
                    fileToDelete = existing.LocalPath;
                    _favorites.Remove(existing);
                }
            }

            await SaveFavoritesAsync();
            _logger.LogInfo($"Обои удалены из избранного: {id}");

            if (!string.IsNullOrEmpty(fileToDelete))
            {
                try
                {
                    if (File.Exists(fileToDelete))
                    {
                        File.Delete(fileToDelete);
                        _logger.LogInfo($"Файл избранного удален с диска: {fileToDelete}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Не удалось удалить файл избранного {fileToDelete}: {ex.Message}");
                }
            }
        }

        public async Task<IReadOnlyList<WallpaperHistoryItem>> GetFavoritesAsync()
        {
            await LoadFavoritesAsync();
            lock (_lock)
            {
                return _favorites.ToList().AsReadOnly();
            }
        }

        public async Task CleanOldNonFavoriteImagesAsync(string downloadFolder, string currentAppliedPath)
        {
            if (!Directory.Exists(downloadFolder)) return;

            await LoadFavoritesAsync();

            HashSet<string> protectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Защищаем файлы избранного
            lock (_lock)
            {
                foreach (var fav in _favorites)
                {
                    if (!string.IsNullOrEmpty(fav.LocalPath))
                    {
                        protectedFiles.Add(Path.GetFullPath(fav.LocalPath));
                    }
                }
            }

            // Защищаем текущие установленные обои
            if (!string.IsNullOrEmpty(currentAppliedPath))
            {
                protectedFiles.Add(Path.GetFullPath(currentAppliedPath));
            }

            try
            {
                var files = Directory.GetFiles(downloadFolder, "*.jpg");
                foreach (var file in files)
                {
                    string fullPath = Path.GetFullPath(file);
                    if (!protectedFiles.Contains(fullPath))
                    {
                        try
                        {
                            File.Delete(file);
                            _logger.LogInfo($"Автоочистка: удален неизбранный файл обоев: {file}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Автоочистка: не удалось удалить файл {file}. Ошибка: {ex.Message}");
                        }
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
            await LoadFavoritesAsync();

            // Копируем список для удаления файлов
            List<WallpaperHistoryItem> toDelete;
            lock (_lock)
            {
                toDelete = _favorites.ToList();
                _favorites.Clear();
            }

            await SaveFavoritesAsync();

            // Пытаемся удалить все файлы избранного с диска
            foreach (var item in toDelete)
            {
                try
                {
                    if (File.Exists(item.LocalPath))
                    {
                        File.Delete(item.LocalPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Ошибка очистки кэша для файла {item.LocalPath}: {ex.Message}");
                }
            }

            _logger.LogInfo("Все файлы избранного и список очищены.");
        }
    }
}
