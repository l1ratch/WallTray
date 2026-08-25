using System;
using System.IO;
using BingWallTray.App.Utils;

namespace BingWallTray.App.Models
{
    public class AppSettings
    {
        public bool AutoChangeEnabled { get; set; } = true;
        public bool AutoCheckBingEnabled { get; set; } = true;
        public bool Paused { get; set; } = false;
        public bool IsFirstRun { get; set; } = true;
        public bool Locked { get; set; } = false;
        public string Market { get; set; } = "ru-RU";
        public bool UseUhd { get; set; } = true;
        public int CheckIntervalHours { get; set; } = 12;
        public string DownloadFolder { get; set; } = string.Empty; // Будет инициализироваться в сервисе
        public int KeepLastImages { get; set; } = 60;
        public bool DeleteOldImages { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimizedToTray { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public bool EnableHistoricalArchive { get; set; } = true;
        public bool EnableWallhaven { get; set; } = false;
        public string WallhavenQuery { get; set; } = "nature";
        public string WallhavenCategories { get; set; } = "110";
        public string WallpaperStyle { get; set; } = "Fill";
        public string LastAppliedImageId { get; set; } = string.Empty;
        public string LastAutoAppliedDate { get; set; } = string.Empty;
        public string LastCheckUtc { get; set; } = string.Empty;
        public string Theme { get; set; } = "System";
        public bool LoggingEnabled { get; set; } = true;
        public string LogLevel { get; set; } = "Info";

        public string AutoChangeSource { get; set; } = "TodayBing"; // TodayBing, RandomBing, Favorites
        public string AutoChangeTrigger { get; set; } = "Interval"; // Interval, Startup, Both
        public string AutoChangeInterval { get; set; } = "12h";
        public string WallhavenResolutions { get; set; } = string.Empty;
        public bool IncludePrereleases { get; set; } = false;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        public AppSettings()
        {
            // Установка дефолтной папки скачивания: %LocalAppData%\WallTray\Wallpapers
            try
            {
                DownloadFolder = AppPaths.DefaultWallpapersFolder;
            }
            catch
            {
                DownloadFolder = string.Empty;
            }

            // Детектируем физическое разрешение основного экрана
            try
            {
                int width = GetSystemMetrics(0);  // SM_CXSCREEN
                int height = GetSystemMetrics(1); // SM_CYSCREEN
                if (width > 0 && height > 0)
                {
                    // Соотношение 16:9?
                    if (width * 9 == height * 16)
                    {
                        WallhavenResolutions = $"{width}x{height},2560x1440,3840x2160";
                    }
                    // Соотношение 16:10?
                    else if (width * 10 == height * 16)
                    {
                        WallhavenResolutions = $"{width}x{height},2560x1600";
                    }
                    else
                    {
                        WallhavenResolutions = $"{width}x{height}";
                    }
                }
                else
                {
                    WallhavenResolutions = "1920x1080,2560x1440,3840x2160";
                }
            }
            catch
            {
                WallhavenResolutions = "1920x1080,2560x1440,3840x2160";
            }
        }
    }
}
