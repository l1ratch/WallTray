using System;
using System.Threading.Tasks;
using BingWallTray.App.Services;
using Xunit;

namespace BingWallTray.Tests
{
    public class GitHubUpdateServiceTests
    {
        [Fact]
        public async Task CheckForUpdatesAsync_ParsesLatestReleaseSuccessfully()
        {
            var logger = new MockLoggingService();
            var service = new GitHubUpdateService(logger);

            // Act
            var result = await service.CheckForUpdatesAsync("l1ratch", "WallTray", includePrereleases: false);

            // Assert
            Assert.NotNull(result);
            Assert.True(string.IsNullOrEmpty(result.ErrorMessage), $"Check updates failed with: {result.ErrorMessage}");
            Assert.False(string.IsNullOrWhiteSpace(result.CurrentVersion));
            Assert.True(service.Status == UpdateStatus.UpToDate || service.Status == UpdateStatus.UpdateAvailable);
        }
    }
}
