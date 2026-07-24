using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using BingWallTray.App.Models;
using BingWallTray.App.Utils;

namespace BingWallTray.App.Services
{
    public interface ISchedulerService
    {
        void Start();
        void Stop();
        void UpdateInterval();
        Task StartAutoCheckAsync(bool isManual, bool isStartup = false, bool forceReload = false);
    }

    public class SchedulerService : ISchedulerService
    {
        private readonly ILoggingService _logger;
        private readonly ISettingsService _settingsService;
        private readonly IHistoryService _historyService;
        private readonly IBingService _bingService;
        private readonly IDownloadService _downloadService;
        private readonly IWallpaperService _wallpaperService;
        private readonly INotificationService _notificationService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly AppState _appState;

        private System.Threading.Timer? _timer;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public SchedulerService(
            ILoggingService logger,
            ISettingsService settingsService,
            IHistoryService historyService,
            IBingService bingService,
            IDownloadService downloadService,
            IWallpaperService wallpaperService,
            INotificationService notificationService,
            IDateTimeProvider dateTimeProvider,
            AppState appState)
        {
            _logger = logger;
            _settingsService = settingsService;
            _historyService = historyService;
            _bingService = bingService;
            _downloadService = downloadService;
            _wallpaperService = wallpaperService;
            _notificationService = notificationService;
            _dateTimeProvider = dateTimeProvider;
            _appState = appState;

            // Загружаем кэшированную подборку обоев для мгновенной загрузки при старте
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string cachePath = Path.Combine(appData, "BingWallTray", "today_cache.json");
                if (File.Exists(cachePath))
                {
                    string json = File.ReadAllText(cachePath);
                    var cached = JsonSerializer.Deserialize<List<BingImage>>(json);
                    if (cached != null && cached.Count > 0)
                    {
                        // Исправляем заголовки для сохраненных в кэше записей, если они были записаны старой версией программы с "Info"
                        foreach (var img in cached)
                        {
                            if (string.IsNullOrWhiteSpace(img.Title) || img.Title.Equals("Info", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!string.IsNullOrEmpty(img.Copyright))
                                {
                                    int index = img.Copyright.IndexOf(" (©");
                                    if (index >= 0)
                                    {
                                        img.Title = img.Copyright.Substring(0, index).Trim();
                                    }
                                    else
                                    {
                                        int idx2 = img.Copyright.IndexOf(" (");
                                        if (idx2 >= 0)
                                        {
                                            img.Title = img.Copyright.Substring(0, idx2).Trim();
                                        }
                                        else
                                        {
                                            img.Title = img.Copyright;
                                        }
                                    }
                                }
                            }
                        }
                        _appState.TodayImages = cached;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Не удалось прочитать локальный кэш обоев при запуске: {ex.Message}");
            }
        }

        public void Start()
        {
            _logger.LogInfo("Запуск планировщика проверок...");
            UpdateInterval();
        }

        public void Stop()
        {
            _logger.LogInfo("Остановка планировщика проверок...");
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _timer?.Dispose();
            _timer = null;
        }

        public static int ParseIntervalToMs(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 12 * 60 * 60 * 1000; // 12h по умолчанию

            input = input.Trim().ToLowerInvariant();

            // Разделяем числовую часть и буквенную часть (например: "30m", "1.5h", "2d", "10s", "1y")
            var numberPart = new string(input.TakeWhile(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
            var unitPart = new string(input.Skip(numberPart.Length).ToArray()).Trim();

            if (!double.TryParse(numberPart.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double value))
            {
                return 12 * 60 * 60 * 1000; // fallback
            }

            double multiplier = 60 * 60 * 1000; // По умолчанию часы (h)
            switch (unitPart)
            {
                case "s": // seconds
                    multiplier = 1000;
                    break;
                case "m": // minutes
                    multiplier = 60 * 1000;
                    break;
                case "h": // hours
                    multiplier = 60 * 60 * 1000;
                    break;
                case "d": // days
                    multiplier = 24 * 60 * 60 * 1000;
                    break;
                case "y": // years
                    multiplier = 365.25 * 24 * 60 * 60 * 1000;
                    break;
            }

            return (int)(value * multiplier);
        }

        public void UpdateInterval()
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _timer?.Dispose();
            _timer = null;

            var settings = _settingsService.CurrentSettings;
            
            // Запускаем таймер фоновых проверок, если включена автосмена (не при запуске) ИЛИ фоновое обновление базы Bing
            bool needTimer = (settings.AutoChangeEnabled && settings.AutoChangeTrigger != "Startup") || settings.AutoCheckBingEnabled;
            
            if (!needTimer)
            {
                _logger.LogInfo("Периодическая проверка по таймеру отключена (автосмена и фоновое обновление Bing выключены).");
                return;
            }

            int intervalMs = ParseIntervalToMs(settings.AutoChangeInterval);
            if (intervalMs <= 0)
            {
                _logger.LogInfo($"Интервал проверок равен 0 или меньше ({settings.AutoChangeInterval}). Таймер отключен.");
                return;
            }

            _timer = new System.Threading.Timer(async _ =>
            {
                _logger.LogInfo("Запуск периодической автоматической проверки по таймеру...");
                await StartAutoCheckAsync(isManual: false);
            }, null, intervalMs, intervalMs);

            _logger.LogInfo($"Планировщик настроен на интервал {settings.AutoChangeInterval} ({intervalMs} мс). (Триггер: {settings.AutoChangeTrigger}, автопроверка Bing в фоне: {settings.AutoCheckBingEnabled})");
        }

        public async Task StartAutoCheckAsync(bool isManual, bool isStartup = false, bool forceReload = false)
        {
            if (!await _semaphore.WaitAsync(0))
            {
                _logger.LogWarning("Проверка уже выполняется. Запрос проигнорирован.");
                return;
            }

            try
            {
                _logger.LogInfo($"Начало автопроверки обоев. Ручной запуск: {isManual}");
                var settings = _settingsService.CurrentSettings;

                // 1. Проверяем, есть ли сегодняшняя подборка в кэше (только для автоматических проверок без forceReload)
                if (!isManual && !forceReload && _appState.TodayImages != null && _appState.TodayImages.Count > 0)
                {
                    var firstImg = _appState.TodayImages.First();
                    string todayStr = _dateTimeProvider.Today.ToString("yyyyMMdd");
                    if (firstImg.StartDate == todayStr)
                    {
                        _logger.LogInfo("Подборка обоев за сегодня уже присутствует в кэше. Пропуск сетевого запроса.");
                        _appState.StatusMessage = "Обои актуальны.";
                        return;
                    }
                }

                // 2. Уважение режима паузы при автоматической проверке
                if (settings.Paused && !isManual)
                {
                    _logger.LogInfo("Автоматическая проверка отменена: программа на паузе.");
                    return;
                }

                _appState.IsChecking = true;
                _appState.StatusMessage = "Запрос подборки обоев Bing...";

                // 2. Получаем 8 ежедневных обоев от Bing API с циклом повторных попыток при автозапуске/фоновой загрузке
                int retryCount = 0;
                int maxRetries = (isStartup && !isManual) ? 10 : 3; // 10 попыток при автозапуске, 3 в обычном фоновом режиме
                int delayMs = 15000; // 15 секунд между попытками
                IReadOnlyList<BingImage>? latestImages = null;

                while (true)
                {
                    latestImages = await _bingService.GetLatestImagesAsync(settings.Market, 8, settings.UseUhd);
                    if (latestImages != null && latestImages.Count > 0)
                    {
                        break;
                    }

                    if (isManual)
                    {
                        // При ручном клике ошибку выводим сразу без ожидания
                        break;
                    }

                    retryCount++;
                    if (retryCount > maxRetries)
                    {
                        _logger.LogError($"Достигнут лимит попыток подключения к Bing API ({maxRetries}). Отмена.");
                        break;
                    }

                    _logger.LogWarning($"Попытка {retryCount}/{maxRetries} запроса к Bing API не удалась. Повтор через {delayMs / 1000} сек...");
                    _appState.StatusMessage = $"Сеть недоступна, повтор {retryCount}/{maxRetries}...";
                    await Task.Delay(delayMs);
                }

                if (latestImages == null || latestImages.Count == 0)
                {
                    _logger.LogError("Не удалось получить подборку изображений.");
                    _appState.StatusMessage = "Не удалось связаться с Bing.";
                    _appState.IsChecking = false;
                    if (isManual || settings.ShowNotifications)
                    {
                        _notificationService.ShowError("BingWallTray", "Не удалось загрузить данные Bing. Проверьте интернет-соединение.");
                    }
                    return;
                }

                // 3. Сохраняем подборку в AppState
                _appState.TodayImages = latestImages;

                // Записываем полученную подборку в кэш
                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string cacheDir = Path.Combine(appData, "BingWallTray");
                    if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
                    string cachePath = Path.Combine(cacheDir, "today_cache.json");
                    string json = JsonSerializer.Serialize(latestImages);
                    File.WriteAllText(cachePath, json);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Не удалось записать кэш подборки обоев: {ex.Message}");
                }

                // Выбираем изображение для автоматической установки в зависимости от источника автосмены
                BingImage todayImage;
                string todayImageId;
                string localPath = string.Empty;

                if (settings.AutoChangeSource == "Favorites")
                {
                    var favorites = await _historyService.GetFavoritesAsync();
                    if (favorites == null || favorites.Count == 0)
                    {
                        _logger.LogWarning("Автосмена настроена на 'Избранное', но список избранного пуст. Используем сегодняшние обои Bing.");
                        todayImage = latestImages.First();
                        todayImageId = $"{todayImage.StartDate}_{settings.Market}";
                    }
                    else
                    {
                        var rand = new Random();
                        var fav = favorites[rand.Next(favorites.Count)];
                        todayImage = new BingImage
                        {
                            Url = fav.LocalPath,
                            Title = fav.Title,
                            Copyright = fav.Copyright,
                            CopyrightLink = fav.CopyrightLink,
                            StartDate = fav.Date
                        };
                        todayImageId = fav.Id;
                        localPath = fav.LocalPath;
                    }
                }
                else if (settings.AutoChangeSource == "RandomBing")
                {
                    var rand = new Random();
                    todayImage = latestImages[rand.Next(latestImages.Count)];
                    todayImageId = $"{todayImage.StartDate}_{settings.Market}";
                }
                else // TodayBing или NewBing
                {
                    todayImage = latestImages.First();
                    todayImageId = $"{todayImage.StartDate}_{settings.Market}";
                }

                // 4. Проверяем, обрабатывались ли обои уже автоматически
                if (!isManual)
                {
                    if (settings.AutoChangeSource == "NewBing" && settings.LastAutoAppliedDate == todayImage.StartDate)
                    {
                        _logger.LogInfo("Сегодняшние обои уже обрабатывались планировщиком (режим 'Только новые обои').");
                        string titleTmp = string.IsNullOrWhiteSpace(todayImage.Title) ? "bing-wallpaper" : todayImage.Title.Trim();
                        string sanitizedTitleTmp = FileNameSanitizer.Sanitize(titleTmp);
                        string expectedPath = Path.Combine(settings.DownloadFolder, $"{todayImage.StartDate}_{settings.Market}_{sanitizedTitleTmp}.jpg");
                        _appState.LastAppliedPath = expectedPath;
                        _appState.StatusMessage = "Обои актуальны.";
                        _appState.IsChecking = false;
                        await _historyService.CleanOldNonFavoriteImagesAsync(settings.DownloadFolder, expectedPath);
                        return;
                    }
                    else if (settings.LastAppliedImageId == todayImageId)
                    {
                        _logger.LogInfo($"Выбранные обои {todayImageId} уже установлены. Пропуск.");
                        _appState.StatusMessage = "Обои актуальны.";
                        _appState.IsChecking = false;
                        return;
                    }
                }

                // Скачиваем изображение, если оно еще не на диске
                if (string.IsNullOrEmpty(localPath))
                {
                    _appState.StatusMessage = "Скачивание фонового изображения...";
                    _appState.IsDownloading = true;
                    try
                    {
                        localPath = await _downloadService.DownloadImageAsync(todayImage, settings.DownloadFolder);
                        _appState.LastAppliedPath = localPath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Не удалось скачать изображение {todayImageId}", ex);
                        _appState.StatusMessage = "Ошибка при загрузке картинки.";
                        _appState.IsChecking = false;
                        _appState.IsDownloading = false;
                        if (isManual || settings.ShowNotifications)
                        {
                            _notificationService.ShowError("Ошибка скачивания", "Не удалось сохранить файл изображения.");
                        }
                        return;
                    }
                    _appState.IsDownloading = false;
                }
                else
                {
                    // Для Favorites файл на диске, но перестрахуемся
                    if (!File.Exists(localPath))
                    {
                        _logger.LogWarning($"Файл из избранного {localPath} не найден на диске. Скачиваем сегодняшние обои Bing.");
                        todayImage = latestImages.First();
                        todayImageId = $"{todayImage.StartDate}_{settings.Market}";
                        
                        _appState.StatusMessage = "Скачивание фонового изображения...";
                        _appState.IsDownloading = true;
                        localPath = await _downloadService.DownloadImageAsync(todayImage, settings.DownloadFolder);
                        _appState.LastAppliedPath = localPath;
                        _appState.IsDownloading = false;
                    }
                }

                // 5. Проверка режима автосмены и триггеров
                bool shouldApply = settings.AutoChangeEnabled;
                if (!isManual && isStartup)
                {
                    if (settings.AutoChangeTrigger == "Interval")
                    {
                        shouldApply = false;
                        _logger.LogInfo("Автосмена настроена на 'Интервал'. Пропускаем применение обоев при запуске.");
                    }
                }

                if (!shouldApply && !isManual)
                {
                    _logger.LogInfo("Автосмена обоев пропущена (отключена или не совпадает с триггером).");
                    _appState.StatusMessage = "Автосмена выключена.";
                    _appState.IsChecking = false;
                    settings.LastAutoAppliedDate = todayImage.StartDate;
                    await _settingsService.SaveAsync(settings);
                    await _historyService.CleanOldNonFavoriteImagesAsync(settings.DownloadFolder, localPath);
                    return;
                }

                // 6. Проверка режима фиксации
                if (settings.Locked && !isManual)
                {
                    _logger.LogInfo("Текущие обои зафиксированы. Установка отменена.");
                    _appState.StatusMessage = "Текущий фон зафиксирован.";
                    _appState.IsChecking = false;
                    if (settings.ShowNotifications)
                    {
                        _notificationService.ShowInfo("BingWallTray", "Новые обои скачаны, но не установлены из-за фиксации.");
                    }
                    settings.LastAutoAppliedDate = todayImage.StartDate;
                    await _settingsService.SaveAsync(settings);
                    await _historyService.CleanOldNonFavoriteImagesAsync(settings.DownloadFolder, localPath);
                    return;
                }

                // 7. Установка обоев рабочего стола
                _appState.StatusMessage = "Применение обоев...";
                if (Enum.TryParse<WallpaperStyle>(settings.WallpaperStyle, true, out var style))
                {
                    // OK
                }
                else
                {
                    style = WallpaperStyle.Fill;
                }

                bool success = _wallpaperService.SetWallpaper(localPath, style);

                if (success)
                {
                    settings.LastAppliedImageId = todayImageId;
                    settings.LastAutoAppliedDate = todayImage.StartDate;
                    settings.LastCheckUtc = _dateTimeProvider.UtcNow.ToString("o");
                    await _settingsService.SaveAsync(settings);

                    _logger.LogInfo($"Обои успешно применены: {todayImageId}");
                    _appState.StatusMessage = "Успешно обновлено!";

                    if (isManual || settings.ShowNotifications)
                    {
                        _notificationService.ShowInfo("Новые обои установлены", todayImage.Title);
                    }

                    // Обновляем список файлов, стирая неизбранные
                    await _historyService.CleanOldNonFavoriteImagesAsync(settings.DownloadFolder, localPath);

                    // Оповещаем окно
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var app = (App)System.Windows.Application.Current;
                        app.OnWallpaperChangedExternally();
                    });
                }
                else
                {
                    _logger.LogError("Не удалось применить изображение на рабочий стол.");
                    _appState.StatusMessage = "Ошибка применения обоев.";
                    if (isManual || settings.ShowNotifications)
                    {
                        _notificationService.ShowError("Ошибка", "Не удалось установить обои через Win32 API.");
                    }
                }
            }
            finally
            {
                _appState.IsChecking = false;
                _semaphore.Release();
            }
        }
    }
}
