using System;
using System.IO;
using BingWallTray.App.Models;
using BingWallTray.App.Utils;
using Xunit;

namespace BingWallTray.Tests
{
    public class AppPathsTests
    {
        [Fact]
        public void AppPaths_PointsToLocalAppData()
        {
            // Assert
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Assert.StartsWith(localAppData, AppPaths.AppDataFolder, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("WallTray", AppPaths.AppDataFolder, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("Wallpapers", AppPaths.DefaultWallpapersFolder, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AppSettings_DefaultDownloadFolder_IsInsideLocalAppData()
        {
            // Act
            var settings = new AppSettings();

            // Assert
            Assert.False(string.IsNullOrEmpty(settings.DownloadFolder));
            Assert.Contains("AppData", settings.DownloadFolder, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WallTray", settings.DownloadFolder, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Pictures", settings.DownloadFolder, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("OneDrive", settings.DownloadFolder, StringComparison.OrdinalIgnoreCase);
        }
    }
}
