using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using BingWallTray.App.Models;

namespace BingWallTray.App.Services
{
    public interface ISpotlightService
    {
        Task<List<BingImage>> GetSpotlightImagesAsync();
    }

    public class SpotlightService : ISpotlightService
    {
        private readonly ILoggingService _logger;
        private readonly string _cacheFolder;

        public SpotlightService(ILoggingService logger)
        {
            _logger = logger;
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _cacheFolder = Path.Combine(appData, "BingWallTray", "SpotlightCache");
        }

        public async Task<List<BingImage>> GetSpotlightImagesAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<BingImage>();
                try
                {
                    if (!Directory.Exists(_cacheFolder))
                    {
                        Directory.CreateDirectory(_cacheFolder);
                    }

                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string spotlightFolder = Path.Combine(localAppData, @"Packages\Microsoft.Windows.ContentDeliveryManager_cw5n1h2txyewy\LocalState\Assets");

                    if (Directory.Exists(spotlightFolder))
                    {
                        var files = Directory.GetFiles(spotlightFolder)
                            .Select(f => new FileInfo(f))
                            .Where(f => f.Length > 100 * 1024) // более 100 КБ
                            .OrderByDescending(f => f.LastWriteTime)
                            .ToList();

                        foreach (var file in files)
                        {
                            try
                            {
                                string destName = $"{file.Name}.jpg";
                                string destPath = Path.Combine(_cacheFolder, destName);

                                // Если файл уже есть в кэше, сразу добавляем в список
                                if (File.Exists(destPath))
                                {
                                    list.Add(new BingImage
                                    {
                                        StartDate = file.LastWriteTime.ToString("yyyyMMdd"),
                                        Title = "Windows Spotlight",
                                        Copyright = "Изображение Windows: интересное",
                                        CopyrightLink = "https://www.microsoft.com",
                                        Url = destPath,
                                        IsFavorite = false,
                                        IsApplied = false
                                    });
                                    continue;
                                }

                                // Иначе проверяем размеры оригинального файла
                                using (var stream = File.OpenRead(file.FullName))
                                {
                                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                                    var frame = decoder.Frames.FirstOrDefault();
                                    if (frame != null)
                                    {
                                        int width = frame.PixelWidth;
                                        int height = frame.PixelHeight;

                                        if (width > height && width >= 1920) // Альбомный формат и высокое разрешение
                                        {
                                            File.Copy(file.FullName, destPath, true);

                                            list.Add(new BingImage
                                            {
                                                StartDate = file.LastWriteTime.ToString("yyyyMMdd"),
                                                Title = "Windows Spotlight",
                                                Copyright = "Изображение Windows: интересное",
                                                CopyrightLink = "https://www.microsoft.com",
                                                Url = destPath,
                                                IsFavorite = false,
                                                IsApplied = false
                                            });
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Пропускаем невалидные файлы или ошибки чтения
                                _logger.LogWarning($"Не удалось прочитать файл Spotlight {file.Name}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Папка Windows Spotlight не найдена.");
                    }

                    // Дополнительно сканируем стандартную папку обоев Windows
                    string winWallpaperDir = @"C:\Windows\Web\Wallpaper";
                    if (Directory.Exists(winWallpaperDir))
                    {
                        var winFiles = Directory.GetFiles(winWallpaperDir, "*.jpg", SearchOption.AllDirectories)
                            .Select(f => new FileInfo(f))
                            .Where(f => f.Length > 100 * 1024)
                            .OrderByDescending(f => f.LastWriteTime)
                            .ToList();

                        foreach (var file in winFiles)
                        {
                            try
                            {
                                using (var stream = File.OpenRead(file.FullName))
                                {
                                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                                    var frame = decoder.Frames.FirstOrDefault();
                                    if (frame != null)
                                    {
                                        int width = frame.PixelWidth;
                                        int height = frame.PixelHeight;

                                        if (width > height && width >= 1920)
                                        {
                                            list.Add(new BingImage
                                            {
                                                StartDate = file.LastWriteTime.ToString("yyyyMMdd"),
                                                Title = $"Windows Wallpaper - {Path.GetFileNameWithoutExtension(file.Name)}",
                                                Copyright = $"Стандартные обои Windows ({file.Directory?.Name})",
                                                CopyrightLink = "https://www.microsoft.com",
                                                Url = file.FullName,
                                                IsFavorite = false,
                                                IsApplied = false
                                            });
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Ошибка при чтении размеров системного файла {file.FullName}: {ex.Message}");
                            }
                        }
                    }

                    _logger.LogInfo($"Найдено {list.Count} альбомных обоев Windows Spotlight и стандартных обоев.");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Ошибка при получении обоев Windows Spotlight", ex);
                }

                return list;
            });
        }
    }
}
