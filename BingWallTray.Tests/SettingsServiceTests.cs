using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BingWallTray.App.Models;
using BingWallTray.App.Services;
using BingWallTray.App.Utils;
using Xunit;

namespace BingWallTray.Tests
{
    public class SettingsServiceTests : IDisposable
    {
        private readonly string _testFolder;
        private readonly string _settingsPath;
        private readonly MockLoggingService _logger;
        private readonly MockDateTimeProvider _dateTimeProvider;

        public SettingsServiceTests()
        {
            // Используем уникальную временную папку для тестов во избежание пересечения данных
            _testFolder = Path.Combine(Path.GetTempPath(), "BingWallTrayTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testFolder);
            _settingsPath = Path.Combine(_testFolder, "settings.json");

            _logger = new MockLoggingService();
            _dateTimeProvider = new MockDateTimeProvider
            {
                Today = new DateTime(2026, 7, 9),
                UtcNow = new DateTime(2026, 7, 9, 10, 0, 0, DateTimeKind.Utc)
            };
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testFolder))
                {
                    Directory.Delete(_testFolder, true);
                }
            }
            catch { /* Игнорируем ошибки очистки временных файлов */ }
        }

        // Вспомогательный класс для тестирования, подменяющий пути на тестовые
        private class TestSettingsService : SettingsService
        {
            private readonly string _customPath;
            private readonly string _customFolder;

            public TestSettingsService(string folder, string path, ILoggingService logger, IDateTimeProvider dateTimeProvider)
                : base(logger, dateTimeProvider)
            {
                _customFolder = folder;
                _customPath = path;

                // Переопределяем пути через рефлексию для тестов
                var fieldPath = typeof(SettingsService).GetField("_settingsFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fieldPath?.SetValue(this, _customPath);

                var fieldFolder = typeof(SettingsService).GetField("_appDataFolder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fieldFolder?.SetValue(this, _customFolder);
            }
        }

        [Fact]
        public async Task LoadAsync_WhenFileDoesNotExist_ReturnsDefaultSettingsAndCreatesFile()
        {
            // Arrange
            var service = new TestSettingsService(_testFolder, _settingsPath, _logger, _dateTimeProvider);

            // Act
            var settings = await service.LoadAsync();

            // Assert
            Assert.NotNull(settings);
            Assert.True(settings.AutoChangeEnabled); // Дефолтное значение
            Assert.True(File.Exists(_settingsPath)); // Файл должен быть создан
        }

        [Fact]
        public async Task SaveAsync_SavesValuesCorrectly()
        {
            // Arrange
            var service = new TestSettingsService(_testFolder, _settingsPath, _logger, _dateTimeProvider);
            var settings = new AppSettings
            {
                AutoChangeEnabled = false,
                Market = "en-US",
                KeepLastImages = 42
            };

            // Act
            await service.SaveAsync(settings);
            var loadedSettings = await service.LoadAsync();

            // Assert
            Assert.False(loadedSettings.AutoChangeEnabled);
            Assert.Equal("en-US", loadedSettings.Market);
            Assert.Equal(42, loadedSettings.KeepLastImages);
        }

        [Fact]
        public async Task LoadAsync_WhenJsonIsCorrupt_CreatesBackupAndRestoresDefault()
        {
            // Arrange
            // Записываем битый JSON
            await File.WriteAllTextAsync(_settingsPath, "{ invalid json ... }");

            var service = new TestSettingsService(_testFolder, _settingsPath, _logger, _dateTimeProvider);
            bool corruptedEventFired = false;
            string brokenFileNameResult = string.Empty;

            service.SettingsCorrupted += (sender, brokenFile) =>
            {
                corruptedEventFired = true;
                brokenFileNameResult = brokenFile;
            };

            // Act
            var settings = await service.LoadAsync();

            // Assert
            Assert.NotNull(settings);
            Assert.True(settings.AutoChangeEnabled); // Возвращены дефолтные настройки
            Assert.True(corruptedEventFired);
            Assert.Equal("settings.broken.20260709.json", brokenFileNameResult);

            string brokenFilePath = Path.Combine(_testFolder, "settings.broken.20260709.json");
            Assert.True(File.Exists(brokenFilePath));
            Assert.Equal("{ invalid json ... }", await File.ReadAllTextAsync(brokenFilePath));
        }
    }

    // Простые заглушки для тестов
    public class MockLoggingService : ILoggingService
    {
        public string LogFolder => string.Empty;
        public bool LoggingEnabled { get; set; } = true;
        public string LogLevel { get; set; } = "Info";
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? ex = null) { }
        public void LogDebug(string message) { }
    }

    public class MockDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; }
        public DateTime Today { get; set; }
    }
}
