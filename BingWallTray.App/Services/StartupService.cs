using System;
using Microsoft.Win32;

namespace BingWallTray.App.Services
{
    public interface IStartupService
    {
        bool IsStartupEnabled();
        void SetStartup(bool enable);
    }

    public class StartupService : IStartupService
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "BingWallTray";
        private readonly ILoggingService _logger;

        public StartupService(ILoggingService logger)
        {
            _logger = logger;
        }

        public bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    if (key == null) return false;
                    
                    object? value = key.GetValue(AppName);
                    if (value == null) return false;

                    string runString = value.ToString() ?? string.Empty;
                    string currentPath = Environment.ProcessPath ?? string.Empty;

                    // Проверяем, содержит ли строка автозапуска путь к текущему EXE
                    if (!string.IsNullOrEmpty(currentPath) && !runString.Contains(currentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning($"Обнаружено несоответствие пути в реестре: {runString} vs текущий {currentPath}. Автозапуск требует обновления.");
                        // Обновляем автозапуск, чтобы записать новый путь
                        SetStartup(true);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при проверке статуса автозапуска в реестре", ex);
                return false;
            }
        }

        public void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key == null)
                    {
                        _logger.LogError($"Не удалось открыть раздел реестра для записи автозапуска: {RunRegistryKey}");
                        return;
                    }

                    if (enable)
                    {
                        string currentPath = Environment.ProcessPath ?? string.Empty;
                        if (string.IsNullOrEmpty(currentPath))
                        {
                            _logger.LogError("Не удалось определить путь к текущему исполняемому файлу для автозапуска.");
                            return;
                        }

                        // Форматируем значение: "C:\Path\To\App.exe" --minimized
                        string value = $"\"{currentPath}\" --minimized";
                        key.SetValue(AppName, value);
                        _logger.LogInfo($"Автозапуск включен. Записано значение в реестр: {value}");
                    }
                    else
                    {
                        if (key.GetValue(AppName) != null)
                        {
                            key.DeleteValue(AppName);
                            _logger.LogInfo("Автозапуск выключен. Запись удалена из реестра.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при изменении статуса автозапуска на: {enable}", ex);
            }
        }
    }
}
