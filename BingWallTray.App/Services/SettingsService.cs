using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BingWallTray.App.Models;
using BingWallTray.App.Utils;

namespace BingWallTray.App.Services
{
    public interface ISettingsService
    {
        AppSettings CurrentSettings { get; }
        Task<AppSettings> LoadAsync();
        Task SaveAsync(AppSettings settings);
        event EventHandler<string>? SettingsCorrupted;
    }

    public class SettingsService : ISettingsService
    {
        private readonly ILoggingService _logger;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly string _settingsFilePath;
        private readonly string _appDataFolder;

        public AppSettings CurrentSettings { get; private set; }

        public event EventHandler<string>? SettingsCorrupted;

        public SettingsService(ILoggingService logger, IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dateTimeProvider = dateTimeProvider;
            
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _appDataFolder = Path.Combine(appData, "BingWallTray");
            _settingsFilePath = Path.Combine(_appDataFolder, "settings.json");

            CurrentSettings = new AppSettings();
            UpdateLoggerSettings(CurrentSettings);
        }

        private void UpdateLoggerSettings(AppSettings settings)
        {
            if (_logger != null)
            {
                _logger.LoggingEnabled = settings.LoggingEnabled;
                _logger.LogLevel = settings.LogLevel;
            }
        }

        public async Task<AppSettings> LoadAsync()
        {
            if (!Directory.Exists(_appDataFolder))
            {
                try
                {
                    Directory.CreateDirectory(_appDataFolder);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Не удалось создать папку настроек приложения", ex);
                    return CurrentSettings;
                }
            }

            string backupPath = _settingsFilePath + ".bak";
            string filePathToLoad = _settingsFilePath;

            // Если основной файл отсутствует, но есть бэкап, пробуем бэкап
            if (!File.Exists(_settingsFilePath) && File.Exists(backupPath))
            {
                filePathToLoad = backupPath;
                _logger.LogWarning("Основной файл настроек отсутствует, используется резервная копия.");
            }
            else if (!File.Exists(_settingsFilePath))
            {
                _logger.LogInfo("Файл настроек не найден, создаются настройки по умолчанию.");
                CurrentSettings = new AppSettings();
                UpdateLoggerSettings(CurrentSettings);
                await SaveAsync(CurrentSettings);
                return CurrentSettings;
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

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var settings = JsonSerializer.Deserialize<AppSettings>(json, options);
                    if (settings == null)
                    {
                        throw new JsonException("Десериализация настроек вернула null.");
                    }

                    CurrentSettings = settings;
                    UpdateLoggerSettings(CurrentSettings);
                    return CurrentSettings;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError($"Файл настроек {filePathToLoad} поврежден (JSON).", jsonEx);

                    // Если основной файл поврежден, пробуем бэкап
                    if (filePathToLoad == _settingsFilePath && File.Exists(backupPath))
                    {
                        _logger.LogWarning("Попытка загрузки резервной копии настроек...");
                        filePathToLoad = backupPath;
                        retryCount = 0; // Сбрасываем попытки для бэкапа
                        continue;
                    }

                    // Если бэкап тоже поврежден или отсутствует
                    await HandleCorruptedSettingsAsync();
                    CurrentSettings = new AppSettings();
                    UpdateLoggerSettings(CurrentSettings);
                    await SaveAsync(CurrentSettings);
                    return CurrentSettings;
                }
                catch (IOException ioEx)
                {
                    retryCount++;
                    if (retryCount >= 5)
                    {
                        _logger.LogError($"Не удалось получить доступ к файлу настроек {filePathToLoad} после {retryCount} попыток.", ioEx);
                        return CurrentSettings;
                    }
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Критическая ошибка чтения настроек {filePathToLoad}", ex);
                    return CurrentSettings;
                }
            }

            return CurrentSettings;
        }

        public async Task SaveAsync(AppSettings settings)
        {
            string tempPath = _settingsFilePath + ".tmp";
            string backupPath = _settingsFilePath + ".bak";

            try
            {
                if (!Directory.Exists(_appDataFolder))
                {
                    Directory.CreateDirectory(_appDataFolder);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(settings, options);
                using (var writer = new StreamWriter(tempPath, false))
                {
                    await writer.WriteAsync(json);
                }

                // Создаем бэкап перед заменой (если старый файл существует)
                if (File.Exists(_settingsFilePath))
                {
                    try
                    {
                        File.Copy(_settingsFilePath, backupPath, true);
                    }
                    catch (Exception ex)
                      {
                          _logger.LogWarning($"Не удалось создать резервную копию настроек перед записью: {ex.Message}");
                      }
                }

                // Атомарно перемещаем временный файл на место основного
                if (File.Exists(_settingsFilePath))
                {
                    File.Delete(_settingsFilePath);
                }
                File.Move(tempPath, _settingsFilePath);

                CurrentSettings = settings;
                UpdateLoggerSettings(CurrentSettings);
                _logger.LogInfo("Настройки успешно сохранены.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при сохранении настроек", ex);
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        private async Task HandleCorruptedSettingsAsync()
        {
            try
            {
                string dateStr = _dateTimeProvider.Today.ToString("yyyyMMdd");
                string brokenFileName = $"settings.broken.{dateStr}.json";
                string brokenFilePath = Path.Combine(_appDataFolder, brokenFileName);

                if (File.Exists(_settingsFilePath))
                {
                    if (File.Exists(brokenFilePath))
                    {
                        File.Delete(brokenFilePath);
                    }
                    File.Move(_settingsFilePath, brokenFilePath);
                    _logger.LogInfo($"Резервная копия поврежденного файла настроек сохранена как {brokenFileName}");
                    
                    SettingsCorrupted?.Invoke(this, brokenFileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Не удалось обработать поврежденные настройки", ex);
            }
            await Task.CompletedTask;
        }
    }
}
