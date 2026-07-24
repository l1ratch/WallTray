using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BingWallTray.App.Services
{
    public enum WallpaperStyle
    {
        Fill,
        Fit,
        Stretch,
        Center,
        Tile,
        Span
    }

    public interface IWallpaperService
    {
        bool SetWallpaper(string imagePath, WallpaperStyle style);
    }

    public class WallpaperService : IWallpaperService
    {
        private readonly ILoggingService _logger;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfoW(
            uint uiAction,
            uint uiParam,
            string pvParam,
            uint fWinIni
        );

        private const uint SPI_SETDESKWALLPAPER = 0x0014;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        public WallpaperService(ILoggingService logger)
        {
            _logger = logger;
        }

        public bool SetWallpaper(string imagePath, WallpaperStyle style)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                _logger.LogError($"Файл обоев не найден для установки: {imagePath}");
                return false;
            }

            _logger.LogInfo($"Установка обоев: {imagePath}, стиль: {style}");

            try
            {
                // Настраиваем стиль в реестре
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                {
                    if (key != null)
                    {
                        string styleVal = "10"; // Fill по умолчанию
                        string tileVal = "0";

                        switch (style)
                        {
                            case WallpaperStyle.Fill:
                                styleVal = "10";
                                tileVal = "0";
                                break;
                            case WallpaperStyle.Fit:
                                styleVal = "6";
                                tileVal = "0";
                                break;
                            case WallpaperStyle.Stretch:
                                styleVal = "2";
                                tileVal = "0";
                                break;
                            case WallpaperStyle.Center:
                                styleVal = "0";
                                tileVal = "0";
                                break;
                            case WallpaperStyle.Tile:
                                styleVal = "0";
                                tileVal = "1";
                                break;
                            case WallpaperStyle.Span:
                                styleVal = "22";
                                tileVal = "0";
                                break;
                        }

                        key.SetValue("WallpaperStyle", styleVal);
                        key.SetValue("TileWallpaper", tileVal);
                    }
                }

                // Вызываем API Windows для установки обоев
                bool result = SystemParametersInfoW(
                    SPI_SETDESKWALLPAPER,
                    0,
                    imagePath,
                    SPIF_UPDATEINIFILE | SPIF_SENDCHANGE
                );

                if (result)
                {
                    _logger.LogInfo("Обои рабочего стола успешно изменены.");
                    return true;
                }
                else
                {
                    int lastError = Marshal.GetLastWin32Error();
                    _logger.LogError($"Не удалось установить обои через SystemParametersInfoW. Win32 Error Code: {lastError}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка в процессе установки обоев", ex);
                return false;
            }
        }
    }
}
