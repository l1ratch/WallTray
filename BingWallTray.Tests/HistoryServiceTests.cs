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

        private class MockSettingsService : ISettingsService
        {
            public AppSettings CurrentSettings { get; }
            public MockSettingsService(string downloadFolder, bool deleteOldImages = true, int keepLastImages = 60)
            {
                CurrentSettings = new AppSettings
                {
                    DownloadFolder = downloadFolder,
                    DeleteOldImages = deleteOldImages,
                    KeepLastImages = keepLastImages
                };
            }
            public Task<AppSettings> LoadAsync() => Task.FromResult(CurrentSettings);
            public Task SaveAsync(AppSettings settings) => Task.CompletedTask;
#pragma warning disable 0067
            public event EventHandler<string>? SettingsCorrupted;
#pragma warning restore 0067
        }

        private class TestHistoryService : HistoryService
        {
            public TestHistoryService(string folder, string path, ILoggingService logger, IDateTimeProvider dateTimeProvider,
                bool deleteOldImages = true, int keepLastImages = 60)
                : base(logger, new MockSettingsService(folder, deleteOldImages, keepLastImages), CreateCacheService(folder, logger, dateTimeProvider))
            {
                var fieldPath = typeof(HistoryService).GetField("_favoritesFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fieldPath?.SetValue(this, Path.Combine(folder, "favorites.json"));
            }

            private static IWallpaperCacheService CreateCacheService(string folder, ILoggingService logger, IDateTimeProvider dateTimeProvider)
            {
                var cacheService = new WallpaperCacheService(logger, dateTimeProvider);

                var fieldPath = typeof(WallpaperCacheService).GetField("_cacheFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fieldPath?.SetValue(cacheService, Path.Combine(folder, "wallpaper_cache.json"));

                var fieldFolder = typeof(WallpaperCacheService).GetField("_appDataFolder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fieldFolder?.SetValue(cacheService, folder);

                return cacheService;
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
            // DisplayPath falls back to the remote URL when the local file doesn't exist on disk
            Assert.Equal(item.RemoteUrl, all[0].DisplayPath);
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
            // Arrange: KeepLastImages=0 forces removal of any non-protected file
            var service = new TestHistoryService(_testFolder, _favoritesPath, _logger, _dateTimeProvider, keepLastImages: 0);

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

        [Fact]
        public async Task CleanOldNonFavoriteImagesAsync_DoesNotDeleteWhenDisabled()
        {
            // Arrange: DeleteOldImages=false — ничего не должно удаляться
            var service = new TestHistoryService(_testFolder, _favoritesPath, _logger, _dateTimeProvider,
                deleteOldImages: false, keepLastImages: 0);

            string file1 = Path.Combine(_testFolder, "file1.jpg");
            string file2 = Path.Combine(_testFolder, "file2.jpg");
            await File.WriteAllTextAsync(file1, "dummy");
            await File.WriteAllTextAsync(file2, "dummy");

            // Act
            await service.CleanOldNonFavoriteImagesAsync(_testFolder, file1);

            // Assert: оба файла остались
            Assert.True(File.Exists(file1));
            Assert.True(File.Exists(file2));
        }

        [Fact]
        public async Task CleanOldNonFavoriteImagesAsync_RespectsKeepLastImages()
        {
            // Arrange: KeepLastImages=2, 4 non-favorite files → 2 oldest deleted
            var service = new TestHistoryService(_testFolder, _favoritesPath, _logger, _dateTimeProvider,
                deleteOldImages: true, keepLastImages: 2);

            // Create files with distinct write times (oldest first)
            string[] files = Enumerable.Range(1, 4)
                .Select(i => Path.Combine(_testFolder, $"img{i}.jpg"))
                .ToArray();

            for (int i = 0; i < files.Length; i++)
            {
                await File.WriteAllTextAsync(files[i], "dummy");
                File.SetLastWriteTime(files[i], DateTime.Now.AddMinutes(-10 + i)); // i=0 oldest
            }

            // Act: no current applied path, no favorites
            await service.CleanOldNonFavoriteImagesAsync(_testFolder, string.Empty);

            // Assert: 2 newest kept, 2 oldest deleted
            Assert.True(File.Exists(files[3]));  // newest
            Assert.True(File.Exists(files[2]));  // second newest
            Assert.False(File.Exists(files[1])); // deleted
            Assert.False(File.Exists(files[0])); // deleted (oldest)
        }
    }
}
