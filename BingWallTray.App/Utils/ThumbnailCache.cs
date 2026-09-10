using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BingWallTray.App.Models;
using BingWallTray.App.Services;

namespace BingWallTray.App.Utils
{
    // ponytail: диск-кэш миниатюр — единственное место, где миниатюры касаются сети.
    // Сеть в UI-потоке запрещена: синхронная загрузка в конвертере вешала интерфейс на
    // флaky CDN (26.8.4), а WPF-загрузчик DelayCreation отравлялся тримом памяти (26.8.3).
    // Скачанные миниатюры подменяют ThumbnailUrl/PreviewUrl на локальный путь — конвертер
    // читает только файлы с диска. Кэш не чистится: ~100 КБ на пару (миниатюра+превью),
    // потолок роста — сотни МБ при полном просмотре архива; чистить, если станет проблемой.
    public static class ThumbnailCache
    {
        private const int MaxAttempts = 3;
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly HashSet<string> _inFlight = new(StringComparer.Ordinal);
        private static readonly object _lock = new();

        static ThumbnailCache()
        {
            // Браузерный UA: Wallhaven отклоняет не-браузерные запросы
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        /// <summary>Фоновая предзагрузка миниатюр и превью для списка изображений (fire-and-forget).</summary>
        public static void Prefetch(IEnumerable<BingImage> images, ILoggingService logger)
        {
            _ = Task.Run(async () =>
            {
                foreach (var img in images)
                {
                    if (img == null) continue;

                    string? thumbLocal = await GetOrCreateAsync(img.ThumbnailUrl, logger).ConfigureAwait(false);
                    if (thumbLocal != null && !string.Equals(img.ThumbnailUrl, thumbLocal, StringComparison.OrdinalIgnoreCase))
                    {
                        SetOnUiThread(() => img.ThumbnailUrl = thumbLocal);
                    }

                    string? previewLocal = await GetOrCreateAsync(img.PreviewUrl, logger).ConfigureAwait(false);
                    if (previewLocal != null && !string.Equals(img.PreviewUrl, previewLocal, StringComparison.OrdinalIgnoreCase))
                    {
                        SetOnUiThread(() => img.PreviewUrl = previewLocal);
                    }
                }
            });
        }

        internal static Task<string?> GetOrCreateAsync(string? url, ILoggingService logger)
            => GetOrCreateAsync(_httpClient, url, AppPaths.ThumbsFolder, logger);

        // internal: тесты подставляют HttpClient со stub-хендлером и свою папку кэша.
        internal static async Task<string?> GetOrCreateAsync(HttpClient client, string? url, string cacheDir, ILoggingService logger)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string cachePath = GetCachePath(url, cacheDir);
            if (File.Exists(cachePath))
            {
                return cachePath;
            }

            lock (_lock)
            {
                // Уже качается другим фоновым проходом — этот просто подождёт следующего обновления галереи
                if (!_inFlight.Add(url)) return null;
            }

            try
            {
                for (int attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    try
                    {
                        // Timeout клиента (ResponseContentRead) покрывает чтение тела целиком —
                        // в отличие от ResponseHeadersRead, зависшая отдача обрывается за 15с.
                        byte[] bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);

                        bool isJpeg = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
                        bool isPng = bytes.Length >= 3 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E;
                        if (bytes.Length < 1024 || (!isJpeg && !isPng))
                        {
                            throw new InvalidDataException($"Скачанные данные не похожи на изображение ({bytes.Length} байт).");
                        }

                        AppPaths.EnsureDirectoryExists(cacheDir);
                        await File.WriteAllBytesAsync(cachePath, bytes).ConfigureAwait(false);
                        return cachePath;
                    }
                    catch (Exception ex)
                    {
                        if (attempt == MaxAttempts)
                        {
                            logger.LogWarning($"Не удалось скачать миниатюру после {MaxAttempts} попыток: {url} ({ex.GetType().Name}: {ex.Message})");
                        }
                        else
                        {
                            await Task.Delay(attempt * 500).ConfigureAwait(false);
                        }
                    }
                }
                return null;
            }
            finally
            {
                lock (_lock)
                {
                    _inFlight.Remove(url);
                }
            }
        }

        internal static string GetCachePath(string url, string cacheDir)
        {
            byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(url));
            return Path.Combine(cacheDir, Convert.ToHexString(hash) + ".jpg");
        }

        private static void SetOnUiThread(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }
    }
}
