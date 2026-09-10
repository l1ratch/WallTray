using System;
using System.IO;
using System.Net.Http;
using System.Threading;
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
        private static readonly HttpClient _httpClient = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectTimeout = TimeSpan.FromSeconds(10)
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        private readonly ILoggingService _logger;

        public DownloadService(ILoggingService logger)
        {
            _logger = logger;
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "26.8.0";
                _httpClient.DefaultRequestHeaders.Add("User-Agent", $"BingWallTray/{version} (.NET 8 WPF Wallpaper Utility)");
            }
        }

        // ponytail: ретраи и пер-попытный дедлайн — без токена чтение тела ответа
        // (ResponseHeadersRead) не имеет дедлайна вообще: обрыв CDN висел 141с и
        // завершался ошибкой «response ended prematurely» (лог 26.8.4, 2026-09-11).
        private const int MaxDownloadAttempts = 3;
        private const int DownloadAttemptTimeoutSeconds = 45;

        public Task<string> DownloadImageAsync(BingImage image, string targetFolder)
            => DownloadImageWithRetryAsync(_httpClient, image, targetFolder, _logger);

        // internal: тесты подставляют HttpClient со stub-хендлером и проверяют ретраи.
        internal static async Task<string> DownloadImageWithRetryAsync(HttpClient client, BingImage image, string targetFolder, ILoggingService logger)
        {
            if (string.IsNullOrEmpty(image.Url))
            {
                throw new ArgumentException("Ссылка на изображение пуста.", nameof(image));
            }

            // Проверяем локальный путь (только если это не сетевой HTTP/HTTPS URL)
            if (!image.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !image.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(image.Url))
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
                    logger.LogInfo($"Локальный файл скопирован в папку загрузок: {image.Url} -> {localDestinationPath}");
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
                    logger.LogError($"Не удалось создать целевую папку для обоев: {targetFolder}", ex);
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
                logger.LogInfo($"Файл обоев уже существует на диске: {destinationPath}. Скачивание пропущено.");
                return destinationPath;
            }

            logger.LogInfo($"Скачивание обоев: {image.Url} -> {destinationPath}");

            Exception? lastError = null;
            for (int attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
            {
                string tempPath = Path.Combine(targetFolder, $"{Guid.NewGuid()}.tmp");
                try
                {
                    // Дедлайн на всю попытку (HTTP-запрос + чтение тела): без токена чтение
                    // тела при ResponseHeadersRead не ограничено ничем.
                    using var attemptCts = new CancellationTokenSource(TimeSpan.FromSeconds(DownloadAttemptTimeoutSeconds));
                    using (var response = await client.GetAsync(image.Url, HttpCompletionOption.ResponseHeadersRead, attemptCts.Token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var stream = await response.Content.ReadAsStreamAsync(attemptCts.Token).ConfigureAwait(false))
                        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
                        {
                            await stream.CopyToAsync(fs, attemptCts.Token).ConfigureAwait(false);
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
                        logger.LogInfo($"Расширение файла скорректировано на основе сигнатуры данных: {extension} -> {correctedExtension}");
                    }

                    // Переименовываем временный файл в целевой
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }
                    File.Move(tempPath, destinationPath);

                    logger.LogInfo($"Обои успешно скачаны и сохранены: {destinationPath}");
                    return destinationPath;
                }
                catch (Exception ex)
                {
                    lastError = ex;

                    // Удаляем временный файл неудавшейся попытки
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { /* Игнорируем */ }
                    }

                    if (attempt < MaxDownloadAttempts)
                    {
                        logger.LogWarning($"Попытка {attempt}/{MaxDownloadAttempts} скачать обои не удалась ({ex.GetType().Name}: {ex.Message}). Повторная попытка...");
                        await Task.Delay(attempt * 1000).ConfigureAwait(false);
                    }
                }
            }

            logger.LogError($"Ошибка при скачивании обоев со ссылки {image.Url}", lastError!);
            throw lastError!;
        }
    }
}
