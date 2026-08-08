using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using BingWallTray.App.Models;
using BingWallTray.App.Services;

namespace BingWallTray.App.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IHistoryService _historyService;
        private readonly ILoggingService _logger;
        private readonly IWingetService _wingetService;
        private readonly IStartupService _startupService;
        private readonly IGitHubUpdateService _updateService;
        private readonly INotificationService _notificationService;
        private readonly Action _closeWindowAction;
        private readonly Action<bool> _wallhavenChanged;

        private readonly AppSettings _settings;
        private int _selectedPageIndex = 0;
        private string _cacheSizeString = "Вычисление...";
        private bool _isWingetAvailable = false;
        private string _wingetStatusText = "Проверка...";
        private bool _isCheckingUpdate = false;
        private bool _isUpdateAvailable = false;
        private string _updateStatusText = "Нажмите «Проверить обновления»";
        private string _releaseUrl = string.Empty;
        private string _networkStatus = "Не проверено";
        private string _bingApiStatus = "Не проверено";
        private bool _isRunningDiagnostics = false;

        public SettingsViewModel(
            ISettingsService settingsService,
            IHistoryService historyService,
            IStartupService startupService,
            ILoggingService logger,
            IWingetService wingetService,
            IGitHubUpdateService updateService,
            INotificationService notificationService,
            Action closeWindowAction,
            Action<bool> wallhavenChanged)
        {
            _settingsService = settingsService;
            _historyService = historyService;
            _startupService = startupService;
            _logger = logger;
            _wingetService = wingetService;
            _updateService = updateService;
            _notificationService = notificationService;
            _closeWindowAction = closeWindowAction;
            _wallhavenChanged = wallhavenChanged;

            _settings = _settingsService.CurrentSettings ?? new AppSettings();

            var informationalVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (informationalVersion != null && informationalVersion.Contains("+"))
            {
                informationalVersion = informationalVersion.Split('+')[0];
            }
            AppVersion = informationalVersion ?? "2026.8.0";

            SelectPageCommand = new RelayCommand<string>(OnSelectPage);
            ChooseFolderCommand = new RelayCommand(OnChooseFolder);
            ClearCacheCommand = new RelayCommand(async () => await OnClearCacheAsync());
            OpenLogsCommand = new RelayCommand(OnOpenLogs);
            ClearLogsCommand = new RelayCommand(async () => await OnClearLogsAsync());
            CheckUpdatesCommand = new RelayCommand(async () => await OnCheckUpdatesAsync());
            OpenReleaseUrlCommand = new RelayCommand(OnOpenReleaseUrl);
            WingetUpgradeCommand = new RelayCommand(async () => await OnWingetUpgradeAsync());
            RunDiagnosticsCommand = new RelayCommand(async () => await OnRunDiagnosticsAsync());
            OpenUrlCommand = new RelayCommand<string>(OnOpenUrl);
            CloseWindowCommand = new RelayCommand(OnCloseWindow);

            _ = InitializeAsync();
        }

        public string AppVersion { get; }

        // --- Навигация ---
        public int SelectedPageIndex
        {
            get => _selectedPageIndex;
            set
            {
                if (SetProperty(ref _selectedPageIndex, value))
                {
                    OnPropertyChanged(nameof(IsPageBehavior));
                    OnPropertyChanged(nameof(IsPageStartup));
                    OnPropertyChanged(nameof(IsPageBing));
                    OnPropertyChanged(nameof(IsPageWallhaven));
                    OnPropertyChanged(nameof(IsPageAutoChange));
                    OnPropertyChanged(nameof(IsPageStorage));
                    OnPropertyChanged(nameof(IsPageLogging));
                    OnPropertyChanged(nameof(IsPageDiagNetwork));
                    OnPropertyChanged(nameof(IsPageDiagSystem));
                    OnPropertyChanged(nameof(IsPageDiagLog));
                    OnPropertyChanged(nameof(IsPageAboutOverview));
                    OnPropertyChanged(nameof(IsPageAboutUpdates));
                    OnPropertyChanged(nameof(IsPageAboutLicenses));
                    if (value == 6 || value == 7 || value == 8) _ = OnRunDiagnosticsAsync();
                }
            }
        }

        public bool IsPageBehavior => SelectedPageIndex == 0;
        public bool IsPageStartup => SelectedPageIndex == 1;
        public bool IsPageBing => SelectedPageIndex == 2;
        public bool IsPageWallhaven => SelectedPageIndex == 3;
        public bool IsPageAutoChange => SelectedPageIndex == 4;
        public bool IsPageStorage => SelectedPageIndex == 5;
        public bool IsPageLogging => SelectedPageIndex == 6;
        public bool IsPageDiagNetwork => SelectedPageIndex == 7;
        public bool IsPageDiagSystem => SelectedPageIndex == 8;
        public bool IsPageDiagLog => SelectedPageIndex == 9;
        public bool IsPageAboutOverview => SelectedPageIndex == 10;
        public bool IsPageAboutUpdates => SelectedPageIndex == 11;
        public bool IsPageAboutLicenses => SelectedPageIndex == 12;

        private void OnSelectPage(string? indexStr)
        {
            if (int.TryParse(indexStr, out int idx))
            {
                SelectedPageIndex = idx;
            }
        }

        // --- Общие: Поведение ---
        public string WallpaperStyle
        {
            get => _settings.WallpaperStyle;
            set { _settings.WallpaperStyle = value; SaveSettings(); OnPropertyChanged(); }
        }

        public int CheckIntervalHours
        {
            get => _settings.CheckIntervalHours;
            set { _settings.CheckIntervalHours = value; SaveSettings(); OnPropertyChanged(); }
        }

        // --- Общие: Запуск и уведомления ---
        public bool IsStartupEnabled
        {
            get => _startupService?.IsStartupEnabled() ?? false;
            set { _startupService?.SetStartup(value); OnPropertyChanged(); }
        }

        public bool StartMinimizedToTray
        {
            get => _settings.StartMinimizedToTray;
            set { _settings.StartMinimizedToTray = value; SaveSettings(); OnPropertyChanged(); }
        }

        public bool ShowNotifications
        {
            get => _settings.ShowNotifications;
            set { _settings.ShowNotifications = value; SaveSettings(); OnPropertyChanged(); }
        }

        // --- Источники: Bing ---
        public string Market
        {
            get => _settings.Market;
            set { _settings.Market = value; SaveSettings(); OnPropertyChanged(); }
        }

        public bool UseUhd
        {
            get => _settings.UseUhd;
            set { _settings.UseUhd = value; SaveSettings(); OnPropertyChanged(); }
        }

        public bool EnableHistoricalArchive
        {
            get => _settings.EnableHistoricalArchive;
            set { _settings.EnableHistoricalArchive = value; SaveSettings(); OnPropertyChanged(); }
        }

        public bool AutoCheckBingEnabled
        {
            get => _settings.AutoCheckBingEnabled;
            set { _settings.AutoCheckBingEnabled = value; SaveSettings(); OnPropertyChanged(); }
        }

        // --- Источники: Wallhaven ---
        public bool EnableWallhaven
        {
            get => _settings.EnableWallhaven;
            set
            {
                if (_settings.EnableWallhaven == value) return;
                _settings.EnableWallhaven = value;
                SaveSettings();
                OnPropertyChanged();
                _wallhavenChanged(value);
            }
        }

        public string WallhavenQuery
        {
            get => _settings.WallhavenQuery;
            set { _settings.WallhavenQuery = value; SaveSettings(); OnPropertyChanged(); }
        }

        public string WallhavenCategories
        {
            get => _settings.WallhavenCategories;
            set { _settings.WallhavenCategories = value; SaveSettings(); OnPropertyChanged(); }
        }

        public string WallhavenResolutions
        {
            get => _settings.WallhavenResolutions;
            set { _settings.WallhavenResolutions = value; SaveSettings(); OnPropertyChanged(); }
        }

        // --- Автосмена ---
        public bool AutoChangeEnabled
        {
            get => _settings.AutoChangeEnabled;
            set { _settings.AutoChangeEnabled = value; SaveSettings(); OnPropertyChanged(); }
        }

        public string AutoChangeSource
        {
            get => _settings.AutoChangeSource;
            set { _settings.AutoChangeSource = value; SaveSettings(); OnPropertyChanged(); }
        }

        public string AutoChangeTrigger
        {
            get => _settings.AutoChangeTrigger;
            set { _settings.AutoChangeTrigger = value; SaveSettings(); OnPropertyChanged(); OnPropertyChanged(nameof(IsIntervalTriggerVisible)); }
        }

        public bool IsIntervalTriggerVisible => AutoChangeTrigger == "Interval" || AutoChangeTrigger == "Both";

        private static readonly string[] PresetIntervals = { "30m", "1h", "6h", "12h", "24h" };

        public string SelectedIntervalPreset
        {
            get => PresetIntervals.Contains(_settings.AutoChangeInterval) ? _settings.AutoChangeInterval : "Custom";
            set
            {
                if (value == "Custom")
                {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCustomIntervalVisible));
                    return;
                }
                _settings.AutoChangeInterval = value;
                SaveSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomIntervalVisible));
            }
        }

        public bool IsCustomIntervalVisible => !PresetIntervals.Contains(_settings.AutoChangeInterval);

        public string CustomIntervalString
        {
            get => _settings.AutoChangeInterval;
            set { _settings.AutoChangeInterval = value; SaveSettings(); OnPropertyChanged(); }
        }

        // --- Данные и журналы: Хранилище ---
        public string DownloadFolder
        {
            get => _settings.DownloadFolder;
            set { _settings.DownloadFolder = value; SaveSettings(); OnPropertyChanged(); }
        }

        public string CacheSizeString
        {
            get => _cacheSizeString;
            set => SetProperty(ref _cacheSizeString, value);
        }

        public bool DeleteOldImages
        {
            get => _settings.DeleteOldImages;
            set { _settings.DeleteOldImages = value; SaveSettings(); OnPropertyChanged(); }
        }

        public int KeepLastImages
        {
            get => _settings.KeepLastImages;
            set { _settings.KeepLastImages = value; SaveSettings(); OnPropertyChanged(); }
        }

        // --- Данные и журналы: Логирование ---
        public bool LoggingEnabled
        {
            get => _settings.LoggingEnabled;
            set { _settings.LoggingEnabled = value; SaveSettings(); OnPropertyChanged(); }
        }

        public string LogLevel
        {
            get => _settings.LogLevel;
            set { _settings.LogLevel = value; SaveSettings(); OnPropertyChanged(); }
        }

        // --- Диагностика ---
        public string NetworkStatus
        {
            get => _networkStatus;
            set => SetProperty(ref _networkStatus, value);
        }

        public string BingApiStatus
        {
            get => _bingApiStatus;
            set => SetProperty(ref _bingApiStatus, value);
        }

        public bool IsRunningDiagnostics
        {
            get => _isRunningDiagnostics;
            set => SetProperty(ref _isRunningDiagnostics, value);
        }

        public string DisplayResolution
        {
            get
            {
                try
                {
                    double w = System.Windows.SystemParameters.PrimaryScreenWidth;
                    double h = System.Windows.SystemParameters.PrimaryScreenHeight;
                    return $"{w}x{h}";
                }
                catch { return "Не определено"; }
            }
        }

        public string OSVersion => Environment.OSVersion.ToString();

        public string DiagnosticsLogText
        {
            get
            {
                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string logFolder = Path.Combine(appData, "WallTray", "Logs");
                    string fullPath = Path.Combine(logFolder, $"app-{DateTime.Today:yyyyMMdd}.log");
                    if (File.Exists(fullPath))
                    {
                        var lines = File.ReadLines(fullPath).TakeLast(30);
                        return string.Join(Environment.NewLine, lines);
                    }
                    return "Файл лога на сегодня еще не создан.";
                }
                catch (Exception ex)
                {
                    return $"Не удалось прочитать логи: {ex.Message}";
                }
            }
        }

        // --- О программе: Обновления ---
        public bool IsWingetAvailable
        {
            get => _isWingetAvailable;
            set => SetProperty(ref _isWingetAvailable, value);
        }

        public string WingetStatusText
        {
            get => _wingetStatusText;
            set => SetProperty(ref _wingetStatusText, value);
        }

        public bool IsCheckingUpdate
        {
            get => _isCheckingUpdate;
            set => SetProperty(ref _isCheckingUpdate, value);
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set => SetProperty(ref _isUpdateAvailable, value);
        }

        public string UpdateStatusText
        {
            get => _updateStatusText;
            set => SetProperty(ref _updateStatusText, value);
        }

        // --- Команды ---
        public ICommand SelectPageCommand { get; }
        public ICommand ChooseFolderCommand { get; }
        public ICommand ClearCacheCommand { get; }
        public ICommand OpenLogsCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand CheckUpdatesCommand { get; }
        public ICommand OpenReleaseUrlCommand { get; }
        public ICommand WingetUpgradeCommand { get; }
        public ICommand RunDiagnosticsCommand { get; }
        public ICommand OpenUrlCommand { get; }
        public ICommand CloseWindowCommand { get; }

        private async Task InitializeAsync()
        {
            await UpdateCacheStatsAsync();

            IsWingetAvailable = await _wingetService.IsWingetAvailableAsync();
            if (IsWingetAvailable)
            {
                var installedVer = await _wingetService.GetInstalledVersionAsync();
                WingetStatusText = string.IsNullOrEmpty(installedVer)
                    ? "Установлено через систему Winget (l1ratch.WallTray)"
                    : $"Пакет Winget доступен (текущая: v{installedVer})";
            }
            else
            {
                WingetStatusText = "Winget CLI не обнаружен в системе.";
            }
        }

        private async Task UpdateCacheStatsAsync()
        {
            try
            {
                int count = await _historyService.GetDownloadedCacheCountAsync();
                long bytes = await _historyService.GetDownloadedCacheSizeAsync();
                double megabytes = bytes / (1024.0 * 1024.0);
                CacheSizeString = $"{count} файлов ({megabytes:F2} МБ)";
            }
            catch
            {
                CacheSizeString = "Н/Д";
            }
        }

        private void OnChooseFolder()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Выберите папку для сохранения обоев",
                UseDescriptionForTitle = true,
                SelectedPath = DownloadFolder
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadFolder = dialog.SelectedPath;
            }
        }

        private async Task OnClearCacheAsync()
        {
            await _historyService.ClearCacheAsync();
            await UpdateCacheStatsAsync();
            _notificationService.ShowInfo("Кэш очищен", "Кэш обоев и файлов успешно удален.");
        }

        private void OnOpenLogs()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string logFolder = Path.Combine(appData, "WallTray", "Logs");
                if (Directory.Exists(logFolder))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{logFolder}\"");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка открытия папки логов", ex);
            }
        }

        private async Task OnClearLogsAsync()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string logFolder = Path.Combine(appData, "WallTray", "Logs");
                if (Directory.Exists(logFolder))
                {
                    var files = Directory.GetFiles(logFolder, "*.log");
                    int count = 0;
                    foreach (var file in files)
                    {
                        try { File.Delete(file); count++; } catch { }
                    }
                    _notificationService.ShowInfo("Очистка логов", $"Удалено файлов: {count}");
                    OnPropertyChanged(nameof(DiagnosticsLogText));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка очистки логов", ex);
            }
            await Task.CompletedTask;
        }

        private async Task OnCheckUpdatesAsync()
        {
            if (IsCheckingUpdate) return;
            IsCheckingUpdate = true;
            UpdateStatusText = "Проверка наличия новых релизов...";

            try
            {
                var result = await _updateService.CheckForUpdatesAsync("l1ratch", "BingWallTray");
                if (result.IsUpdateAvailable)
                {
                    IsUpdateAvailable = true;
                    _releaseUrl = result.ReleaseUrl;
                    UpdateStatusText = $"Доступна новая версия v{result.NewVersion}!";
                }
                else
                {
                    IsUpdateAvailable = false;
                    UpdateStatusText = "У вас установлена последняя версия.";
                }
            }
            catch
            {
                UpdateStatusText = "Ошибка сети при проверке обновлений.";
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        private void OnOpenReleaseUrl()
        {
            if (!string.IsNullOrEmpty(_releaseUrl))
            {
                OnOpenUrl(_releaseUrl);
            }
        }

        private async Task OnWingetUpgradeAsync()
        {
            if (!IsWingetAvailable) return;
            UpdateStatusText = "Запуск бесшумного обновления через Winget...";
            bool success = await _wingetService.UpgradePackageAsync();
            if (success)
            {
                _notificationService.ShowInfo("Обновление Winget", "Приложение успешно обновлено через Winget!");
                UpdateStatusText = "Пакет обновлен!";
            }
            else
            {
                _notificationService.ShowError("Ошибка Winget", "Не удалось запустить обновление через Winget.");
                UpdateStatusText = "Ошибка обновления через Winget.";
            }
        }

        private async Task OnRunDiagnosticsAsync()
        {
            if (IsRunningDiagnostics) return;
            IsRunningDiagnostics = true;
            NetworkStatus = "Проверка...";
            BingApiStatus = "Проверка...";

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 1500);
                NetworkStatus = reply.Status == IPStatus.Success
                    ? "Доступен (подключение к интернету есть)"
                    : $"Недоступен (статус: {reply.Status})";
            }
            catch (Exception ex)
            {
                NetworkStatus = $"Ошибка проверки ({ex.Message})";
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var response = await client.GetAsync("https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1");
                BingApiStatus = response.IsSuccessStatusCode
                    ? "Доступен (API отвечает корректно)"
                    : $"Ошибка (статус-код: {(int)response.StatusCode})";
            }
            catch (Exception ex)
            {
                BingApiStatus = $"Ошибка запроса ({ex.Message})";
            }

            OnPropertyChanged(nameof(DiagnosticsLogText));
            await UpdateCacheStatsAsync();
            IsRunningDiagnostics = false;
        }

        private void OnOpenUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Не удалось открыть URL {url}", ex);
            }
        }

        private void SaveSettings()
        {
            _settingsService.SaveAsync(_settings);
        }

        private void OnCloseWindow()
        {
            _closeWindowAction?.Invoke();
        }
    }
}
