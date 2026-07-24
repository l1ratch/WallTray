using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BingWallTray.App.Models;
using BingWallTray.App.Services;
using BingWallTray.App.Utils;
using Xunit;

namespace BingWallTray.Tests
{
    public class HistoryServiceTests : IDisposable
    {
        private readonly string _testFolder;
        private readonly string _favoritesPath;
        private readonly MockLoggingService _logger;
        private readonly MockDateTimeProvider _dateTimeProvider;

        public HistoryServiceTests()
        {
            _testFolder = Path.Combine(Path.GetTempPath(), "BingWallTrayHistoryTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testFolder);
            _favoritesPath = Path.Combine(_testFolder, "favorites.json");

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
            catch { }
        }

        private class TestHistoryService : HistoryService
        {
            public TestHistoryService(string folder, string path, ILoggingService logger, IDateTimeProvider dateTimeProvider)
                : base(logger, dateTimeProvider)
            {
                var fieldPath = typeof(HistoryService).GetField("_favoritesFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fieldPath?.SetValue(this, path);

                var fieldFolder = typeof(HistoryService).GetField("_appDataFolder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fieldFolder?.SetValue(this, folder);
            }
        }

        [Fact]
        public async Task AddOrUpdateFavoriteAsync_AddsNewFavoriteCorrectly()
        {
            // Arrange
            var service = new TestHistoryService(_testFolder, _favoritesPath, _logger, _dateTimeProvider);
            var item = new WallpaperHistoryItem
            {
                Id = "20260709_ru-RU",
                Date = "20260709",
                Title = "Тестовые обои",
                LocalPath = "C:\\test.jpg"
            };

            // Act
            await service.AddOrUpdateFavoriteAsync(item);
            var all = await service.GetFavoritesAsync();

            // Assert
            Assert.Single(all);
            Assert.Equal("Тестовые обои", all[0].Title);
            Assert.Equal("20260709_ru-RU", all[0].Id);
            Assert.True(all[0].IsFavorite);
        }

        [Fact]
        public async Task RemoveFavoriteAsync_RemovesItemCorrectly()
        {
            // Arrange
            var service = new TestHistoryService(_testFolder, _favoritesPath, _logger, _dateTimeProvider);
            var item1 = new WallpaperHistoryItem { Id = "1", Title = "Обои 1" };
            var item2 = new WallpaperHistoryItem { Id = "2", Title = "Обои 2" };

            await service.AddOrUpdateFavoriteAsync(item1);
            await service.AddOrUpdateFavoriteAsync(item2);

            // Act
            await service.RemoveFavoriteAsync("1");
            var all = await service.GetFavoritesAsync();

            // Assert
            Assert.Single(all);
            Assert.Equal("2", all[0].Id);
        }

        [Fact]
        public async Task CleanOldNonFavoriteImagesAsync_DeletesOnlyNonFavoritesAndNotCurrent()
        {
            // Arrange
            var service = new TestHistoryService(_testFolder, _favoritesPath, _logger, _dateTimeProvider);

            // Создаем файлы на диске
            string file1 = Path.Combine(_testFolder, "file1.jpg");
            string file2 = Path.Combine(_testFolder, "file2.jpg");
            string file3 = Path.Combine(_testFolder, "file3.jpg");

            await File.WriteAllTextAsync(file1, "dummy image content");
            await File.WriteAllTextAsync(file2, "dummy image content");
            await File.WriteAllTextAsync(file3, "dummy image content");

            // Записи: 
            // file1 - избранный
            // file2 - обычный (будет удален)
            // file3 - текущие установленные обои (не избранный, но активный - должен сохраниться)
            var item1 = new WallpaperHistoryItem { Id = "1", LocalPath = file1, IsFavorite = true };
            await service.AddOrUpdateFavoriteAsync(item1);

            // Act
            await service.CleanOldNonFavoriteImagesAsync(_testFolder, file3);

            // Assert
            Assert.True(File.Exists(file1)); // Сохранен как избранный
            Assert.True(File.Exists(file3)); // Сохранен как текущие обои
            Assert.False(File.Exists(file2)); // Удален как лишний неизбранный
        }
    }
}
