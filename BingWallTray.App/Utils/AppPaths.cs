using System;
using System.IO;

namespace BingWallTray.App.Utils
{
    public static class AppPaths
    {
        private static string? _appDataFolder;

        public static string AppDataFolder
        {
            get
            {
                if (_appDataFolder != null) return _appDataFolder;

                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string localDataDir = Path.Combine(exeDir, "data");
                string portableMarker = Path.Combine(exeDir, "portable.dat");

                // Если рядом с исполняемым файлом создан маркер portable.dat или папка data (и это не системная папка Velopack)
                if (!exeDir.Contains(@"AppData\Local\WallTray", StringComparison.OrdinalIgnoreCase) && 
                    (Directory.Exists(localDataDir) || File.Exists(portableMarker)))
                {
                    _appDataFolder = localDataDir;
                    EnsureDirectoryExists(_appDataFolder);
                    return _appDataFolder;
                }

                // Единое стандартное расположение данных для установленной и портативной версий
                _appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallTray");
                EnsureDirectoryExists(_appDataFolder);
                return _appDataFolder;
            }
        }

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

        public static string LegacyRoamingBingWallFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BingWallTray");

        public static string LegacyLocalBingWallFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BingWallTray");

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
