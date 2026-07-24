using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BingWallTray.App.Models;
using BingWallTray.App.Utils;

namespace BingWallTray.App.Services
{
    public interface IDownloadService
    {
        Task<string> DownloadImageAsync(BingImage image, string targetFolder);
    }

    public class DownloadService : IDownloadService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly ILoggingService _logger;

        public DownloadService(ILoggingService logger)
        {
            _logger = logger;
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "BingWallTray/1.0.0 (.NET 8 WPF Wallpaper Utility)");
            }
        }

        public async Task<string> DownloadImageAsync(BingImage image, string targetFolder)
        {
            if (string.IsNullOrEmpty(image.Url))
            {
                throw new ArgumentException("Ссылка на изображение пуста.", nameof(image));
            }

            if (File.Exists(image.Url))
            {
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                string localTitle = string.IsNullOrWhiteSpace(image.Title) ? "wallpaper" : image.Title.Trim();
                string localSanitizedTitle = FileNameSanitizer.Sanitize(localTitle);
                string localMarket = string.IsNullOrWhiteSpace(image.Market) ? "unknown" : image.Market;
                string localExtension = Path.GetExtension(image.Url);
                if (string.IsNullOrEmpty(localExtension)) localExtension = ".jpg";
                string localFileName = $"{image.StartDate}_{localMarket}_{localSanitizedTitle}{localExtension}";
                string localDestinationPath = Path.Combine(targetFolder, localFileName);

                if (!File.Exists(localDestinationPath))
                {
                    File.Copy(image.Url, localDestinationPath, true);
                    _logger.LogInfo($"Локальный файл скопирован в папку загрузок: {image.Url} -> {localDestinationPath}");
                }
                return localDestinationPath;
            }

            if (!Directory.Exists(targetFolder))
            {
                try
                {
                    Directory.CreateDirectory(targetFolder);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Не удалось создать целевую папку для обоев: {targetFolder}", ex);
                    throw;
                }
            }

            // Формируем имя файла
            string title = string.IsNullOrWhiteSpace(image.Title) ? "bing-wallpaper" : image.Title.Trim();
            string sanitizedTitle = FileNameSanitizer.Sanitize(title);
            string market = string.IsNullOrWhiteSpace(image.Market) ? "unknown" : image.Market;
            string extension = Path.GetExtension(image.Url);
            if (string.IsNullOrEmpty(extension)) extension = ".jpg";
            if (extension.Contains("?"))
            {
                extension = extension.Substring(0, extension.IndexOf('?'));
            }
            string fileName = $"{image.StartDate}_{market}_{sanitizedTitle}{extension}";
            string destinationPath = Path.Combine(targetFolder, fileName);

            // Проверяем, существует ли файл
            if (File.Exists(destinationPath))
            {
                _logger.LogInfo($"Файл обоев уже существует на диске: {destinationPath}. Скачивание пропущено.");
                return destinationPath;
            }

            _logger.LogInfo($"Скачивание обоев: {image.Url} -> {destinationPath}");

            string tempPath = Path.Combine(targetFolder, $"{Guid.NewGuid()}.tmp");

            try
            {
                using (var response = await _httpClient.GetAsync(image.Url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                // Проверяем целостность файла
                var fileInfo = new FileInfo(tempPath);
                if (fileInfo.Length < 10240) // Минимальный размер 10 КБ
                {
                    throw new InvalidDataException($"Размер скачанного файла подозрительно мал: {fileInfo.Length} байт.");
                }

                // Проверяем сигнатуру JPEG (magic bytes: FF D8 FF) или PNG (magic bytes: 89 50 4E)
                string correctedExtension = extension;
                using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[3];
                    int bytesRead = await fs.ReadAsync(buffer, 0, 3);
                    
                    bool isJpeg = bytesRead >= 3 && buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF;
                    bool isPng = bytesRead >= 3 && buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E;
                    
                    if (!isJpeg && !isPng)
                    {
                        throw new InvalidDataException("Файл не является корректным JPEG или PNG изображением (неверная сигнатура magic bytes).");
                    }

                    if (isPng)
                    {
                        correctedExtension = ".png";
                    }
                    else if (isJpeg)
                    {
                        correctedExtension = ".jpg";
                    }
                }

                if (correctedExtension != extension)
                {
                    destinationPath = Path.ChangeExtension(destinationPath, correctedExtension);
                    _logger.LogInfo($"Расширение файла скорректировано на основе сигнатуры данных: {extension} -> {correctedExtension}");
                }

                // Переименовываем временный файл в целевой
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(tempPath, destinationPath);

                _logger.LogInfo($"Обои успешно скачаны и сохранены: {destinationPath}");
                return destinationPath;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при скачивании обоев со ссылки {image.Url}", ex);
                
                // Удаляем временный файл в случае неудачи
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* Игнорируем */ }
                }
                throw;
            }
        }
    }
}
