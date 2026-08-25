using System;
using System.IO;

namespace BingWallTray.App.Utils
{
    public static class AppPaths
    {
        public static string AppDataFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallTray");

        public static string DefaultWallpapersFolder =>
            Path.Combine(AppDataFolder, "Wallpapers");

        public static string SettingsFilePath =>
            Path.Combine(AppDataFolder, "settings.json");

        public static string CacheFilePath =>
            Path.Combine(AppDataFolder, "wallpaper_cache.json");

        public static string FavoritesFilePath =>
            Path.Combine(AppDataFolder, "favorites.json");

        public static string TodayCacheFilePath =>
            Path.Combine(AppDataFolder, "today_cache.json");

        public static string LogFolder =>
            Path.Combine(AppDataFolder, "Logs");

        public static string LegacyRoamingAppDataFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WallTray");

        public static void EnsureDirectoryExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch
                {
                    // Игнорируем или обрабатываем в вызывающем сервисе
                }
            }
        }
    }
}
