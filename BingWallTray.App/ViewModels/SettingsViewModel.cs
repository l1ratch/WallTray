using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BingWallTray.App.Models;
using BingWallTray.App.Services;
using BingWallTray.App.Utils;

namespace BingWallTray.App.ViewModels
{
    public class MonitorInfoItem
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRate { get; set; }
        public string AspectRatio { get; set; } = string.Empty;
        public string ResolutionString => $"{Width} × {Height}" + (RefreshRate > 0 ? $" @ {RefreshRate} Гц" : string.Empty);
        public string Details => $"Соотношение: {AspectRatio} • {(IsPrimary ? "Основной дисплей" : "Вторичный дисплей")}";
    }

    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IHistoryService _historyService;
        private readonly ILoggingService _logger;
        private readonly IStartupService _startupService;
        private readonly IGitHubUpdateService _updateService;
        private readonly INotificationService _notificationService;
        private readonly Action _closeWindowAction;
        private readonly Action<bool> _wallhavenChanged;

        private readonly AppSettings _settings;
        private int _selectedPageIndex = 0;
        private string _cacheSizeString = "Вычисление...";
        private bool _isCheckingUpdate = false;
        private bool _isDownloadingUpdate = false;
        private bool _isUpdateDownloaded = false;
        private bool _isUpdateAvailable = false;
        private double _downloadProgress = 0.0;
        private string _newVersion = string.Empty;
        private string _updateStatusText = "Нажмите «Проверить обновления»";
        private string _releaseUrl = string.Empty;
        private string _networkStatus = "Не проверено";
        private string _bingApiStatus = "Не проверено";
        private bool _isRunningDiagnostics = false;
        private bool _isLogConsoleExpanded = false;

        public SettingsViewModel(
            ISettingsService settingsService,
            IHistoryService historyService,
            IStartupService startupService,
            ILoggingService logger,
            IGitHubUpdateService updateService,
            INotificationService notificationService,
            Action closeWindowAction,
            Action<bool> wallhavenChanged)
        {
            _settingsService = settingsService;
            _historyService = historyService;
            _startupService = startupService;
            _logger = logger;
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
            AppVersion = informationalVersion ?? "26.8.0";

            // Синхронизация событий службы обновлений Velopack
            _updateService.ProgressChanged += (s, progress) =>
            {
                DownloadProgress = progress * 100.0;
            };
            _updateService.StatusChanged += (s, status) =>
            {
                IsUpdateDownloaded = _updateService.IsUpdateDownloaded;
                if (!string.IsNullOrEmpty(_updateService.StatusMessage))
                {
                    UpdateStatusText = _updateService.StatusMessage;
                }
            };

            SelectPageCommand = new RelayCommand<string>(OnSelectPage);
            ChooseFolderCommand = new RelayCommand(OnChooseFolder);
            OpenDownloadFolderCommand = new RelayCommand(OnOpenDownloadFolder);
            ClearCacheCommand = new RelayCommand(async () => await OnClearCacheAsync());
            OpenLogsCommand = new RelayCommand(OnOpenLogs);
            ClearLogsCommand = new RelayCommand(async () => await OnClearLogsAsync());
            CheckUpdatesCommand = new RelayCommand(async () => await OnCheckUpdatesAsync());
            DownloadUpdateCommand = new RelayCommand(async () => await OnDownloadUpdateAsync());
            ApplyAndRestartCommand = new RelayCommand(OnApplyAndRestart);
            OpenReleaseUrlCommand = new RelayCommand(OnOpenReleaseUrl);
            RunDiagnosticsCommand = new RelayCommand(async () => await OnRunDiagnosticsAsync());
            ToggleLogConsoleCommand = new RelayCommand(() => IsLogConsoleExpanded = !IsLogConsoleExpanded);
            SetWallhavenQueryCommand = new RelayCommand<string>(tag => { if (!string.IsNullOrEmpty(tag)) WallhavenQuery = tag; });
            AutoDetectResolutionCommand = new RelayCommand(OnAutoDetectResolution);
            OpenUrlCommand = new RelayCommand<string>(OnOpenUrl);
            CloseWindowCommand = new RelayCommand(OnCloseWindow);

            _ = InitializeAsync();
        }

        public string AppVersion { get; }

        // --- Навигация: 2 группы (НАСТРОЙКИ и О ПРИЛОЖЕНИИ) ---
        public int SelectedPageIndex
        {
            get => _selectedPageIndex;
            set
            {
                if (SetProperty(ref _selectedPageIndex, value))
                {
                    OnPropertyChanged(nameof(IsPageGeneral));
                    OnPropertyChanged(nameof(IsPageSources));
                    OnPropertyChanged(nameof(IsPageBing));
                    OnPropertyChanged(nameof(IsPageWallhaven));
                    OnPropertyChanged(nameof(IsPageAutoChange));
                    OnPropertyChanged(nameof(IsPageStorage));
                    OnPropertyChanged(nameof(IsPageDiagnostics));
                    OnPropertyChanged(nameof(IsPageAbout));
                    OnPropertyChanged(nameof(IsPageUpdates));
                    OnPropertyChanged(nameof(IsPageLicenses));

                    if (value == 6) // Диагностика
                    {
                        _ = OnRunDiagnosticsAsync();
                    }
                }
            }
        }

        public bool IsPageGeneral => SelectedPageIndex == 0;
        public bool IsPageSources => SelectedPageIndex == 1;
        public bool IsPageBing => SelectedPageIndex == 2;
        public bool IsPageWallhaven => SelectedPageIndex == 3;
        public bool IsPageAutoChange => SelectedPageIndex == 4;
        public bool IsPageStorage => SelectedPageIndex == 5;
        public bool IsPageDiagnostics => SelectedPageIndex == 6;
        public bool IsPageAbout => SelectedPageIndex == 7;
        public bool IsPageUpdates => SelectedPageIndex == 8;
        public bool IsPageLicenses => SelectedPageIndex == 9;

        private void OnSelectPage(string? indexStr)
        {
            if (int.TryParse(indexStr, out int idx))
            {
                SelectedPageIndex = idx;
            }
        }

        // --- 1. Раздел: ОБЩИЕ ---
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

        public string Theme
        {
            get => _settings.Theme;
            set { _settings.Theme = value; SaveSettings(); OnPropertyChanged(); }
        }

        // --- 2. Раздел: ИСТОЧНИК BING ---
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

        // --- 3. Раздел: ИСТОЧНИК WALLHAVEN ---
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
            set
            {
                _settings.WallhavenCategories = value;
                SaveSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(WallhavenCatGeneral));
                OnPropertyChanged(nameof(WallhavenCatAnime));
                OnPropertyChanged(nameof(WallhavenCatPeople));
            }
        }

        public bool WallhavenCatGeneral
        {
            get => GetCategoryFlag(0);
            set => SetCategoryFlag(0, value);
        }

        public bool WallhavenCatAnime
        {
            get => GetCategoryFlag(1);
            set => SetCategoryFlag(1, value);
        }

        public bool WallhavenCatPeople
        {
            get => GetCategoryFlag(2);
            set => SetCategoryFlag(2, value);
        }

        private bool GetCategoryFlag(int index)
        {
            var cat = _settings.WallhavenCategories ?? "110";
            if (index < cat.Length)
                return cat[index] == '1';
            return false;
        }

        private void SetCategoryFlag(int index, bool val)
        {
            var cat = (_settings.WallhavenCategories ?? "110").ToCharArray();
            if (cat.Length < 3) cat = "110".ToCharArray();
            if (index < cat.Length)
            {
                cat[index] = val ? '1' : '0';
                _settings.WallhavenCategories = new string(cat);
                SaveSettings();
                OnPropertyChanged(nameof(WallhavenCategories));
                OnPropertyChanged(nameof(WallhavenCatGeneral));
                OnPropertyChanged(nameof(WallhavenCatAnime));
                OnPropertyChanged(nameof(WallhavenCatPeople));
            }
        }

        public string WallhavenResolutions
        {
            get => _settings.WallhavenResolutions;
            set
            {
                _settings.WallhavenResolutions = value;
                SaveSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(WallhavenRes1080p));
                OnPropertyChanged(nameof(WallhavenRes1440p));
                OnPropertyChanged(nameof(WallhavenRes4K));
                OnPropertyChanged(nameof(WallhavenResUltrawide));
            }
        }

        public bool WallhavenRes1080p
        {
            get => HasResolution("1920x1080");
            set => ToggleResolution("1920x1080", value);
        }

        public bool WallhavenRes1440p
        {
            get => HasResolution("2560x1440");
            set => ToggleResolution("2560x1440", value);
        }

        public bool WallhavenRes4K
        {
            get => HasResolution("3840x2160");
            set => ToggleResolution("3840x2160", value);
        }

        public bool WallhavenResUltrawide
        {
            get => HasResolution("3440x1440");
            set => ToggleResolution("3440x1440", value);
        }

        private bool HasResolution(string res)
        {
            var list = (_settings.WallhavenResolutions ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return list.Contains(res);
        }

        private void ToggleResolution(string res, bool enable)
        {
            var list = (_settings.WallhavenResolutions ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (enable && !list.Contains(res)) list.Add(res);
            else if (!enable && list.Contains(res)) list.Remove(res);
            _settings.WallhavenResolutions = string.Join(",", list);
            SaveSettings();
            OnPropertyChanged(nameof(WallhavenResolutions));
            OnPropertyChanged(nameof(WallhavenRes1080p));
            OnPropertyChanged(nameof(WallhavenRes1440p));
            OnPropertyChanged(nameof(WallhavenRes4K));
            OnPropertyChanged(nameof(WallhavenResUltrawide));
        }

        private void OnAutoDetectResolution()
        {
            var monitors = ConnectedMonitors;
            var detectedList = new HashSet<string>();
            foreach (var m in monitors)
            {
                if (m.Width > 0 && m.Height > 0)
                {
                    detectedList.Add($"{m.Width}x{m.Height}");
                }
            }

            var current = (_settings.WallhavenResolutions ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
            foreach (var d in detectedList) current.Add(d);

            WallhavenResolutions = string.Join(",", current);
            _notificationService.ShowInfo("Разрешения определены", $"Добавлены разрешения обнаруженных экранов: {string.Join(", ", detectedList)}");
        }

        // --- 4. Раздел: АВТОСМЕНА ---
        public bool AutoChangeEnabled
        {
            get => _settings.AutoChangeEnabled;
            set 
            { 
                _settings.AutoChangeEnabled = value; 
                SaveSettings(); 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsAutoChangeOptionsEnabled));
            }
        }

        public bool IsAutoChangeOptionsEnabled => AutoChangeEnabled;

        public string AutoChangeSource
        {
            get => string.Equals(_settings.AutoChangeSource, "NewBing", StringComparison.OrdinalIgnoreCase) ? "TodayBing" : _settings.AutoChangeSource;
            set 
            { 
                _settings.AutoChangeSource = value; 
                SaveSettings(); 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsTodayBingSelected));
                OnPropertyChanged(nameof(IsPeriodicSourceSelected));
                OnPropertyChanged(nameof(IsIntervalTriggerVisible));
            }
        }

        public bool IsTodayBingSelected => string.Equals(AutoChangeSource, "TodayBing", StringComparison.OrdinalIgnoreCase) || string.Equals(AutoChangeSource, "NewBing", StringComparison.OrdinalIgnoreCase);

        public bool IsPeriodicSourceSelected => !IsTodayBingSelected;

        public bool IsWallhavenAutoChangeAvailable => EnableWallhaven;

        public string AutoChangeTrigger
        {
            get => _settings.AutoChangeTrigger;
            set 
            { 
                _settings.AutoChangeTrigger = value; 
                SaveSettings(); 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsIntervalTriggerVisible)); 
            }
        }

        public bool IsIntervalTriggerVisible => IsPeriodicSourceSelected && (AutoChangeTrigger == "Interval" || AutoChangeTrigger == "Both");

        private static readonly string[] PresetIntervals = { "15m", "30m", "1h", "2h", "6h", "12h", "24h" };

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

        // --- 5. Раздел: ХРАНИЛИЩЕ И КЭШ ---
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

        // --- 6. Раздел: ДИАГНОСТИКА И ЛОГИ (ВКЛЮЧАЯ ПОЛНЫЙ АНАЛИЗ ВСЕХ МОНИТОРОВ) ---
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

        // Мультимониторный анализ системы
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        public List<MonitorInfoItem> ConnectedMonitors => GetConnectedMonitors();

        public static List<MonitorInfoItem> GetConnectedMonitors()
        {
            var list = new List<MonitorInfoItem>();
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                int idx = 1;
                foreach (var s in screens)
                {
                    int w = s.Bounds.Width;
                    int h = s.Bounds.Height;
                    int hz = 0;

                    DEVMODE dm = default;
                    dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                    if (EnumDisplaySettings(s.DeviceName, -1, ref dm))
                    {
                        if (dm.dmPelsWidth > 0 && dm.dmPelsHeight > 0)
                        {
                            w = dm.dmPelsWidth;
                            h = dm.dmPelsHeight;
                            hz = dm.dmDisplayFrequency;
                        }
                    }

                    list.Add(new MonitorInfoItem
                    {
                        Index = idx,
                        Name = s.Primary ? $"Дисплей {idx} (Основной)" : $"Дисплей {idx}",
                        IsPrimary = s.Primary,
                        Width = w,
                        Height = h,
                        RefreshRate = hz,
                        AspectRatio = CalculateAspectRatio(w, h)
                    });
                    idx++;
                }
            }
            catch
            {
                int w = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
                int h = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
                list.Add(new MonitorInfoItem
                {
                    Index = 1,
                    Name = "Основной дисплей",
                    IsPrimary = true,
                    Width = w,
                    Height = h,
                    AspectRatio = CalculateAspectRatio(w, h)
                });
            }
            return list;
        }

        private static string CalculateAspectRatio(int width, int height)
        {
            if (width <= 0 || height <= 0) return "16:9";
            int gcd = GreatestCommonDivisor(width, height);
            int x = width / gcd;
            int y = height / gcd;

            if ((x == 8 && y == 5) || (x == 16 && y == 10)) return "16:10";
            if ((x == 64 && y == 27) || (x == 43 && y == 18) || (x == 12 && y == 5) || (x == 21 && y == 9)) return "21:9";
            if ((x == 32 && y == 9)) return "32:9";
            if (x == 16 && y == 9) return "16:9";
            if (x == 4 && y == 3) return "4:3";
            return $"{x}:{y}";
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

        public string DisplayResolution
        {
            get
            {
                var monitors = ConnectedMonitors;
                if (monitors.Count == 1)
                {
                    var m = monitors[0];
                    return $"{m.ResolutionString} ({m.AspectRatio})";
                }
                return $"{monitors.Count} монитора: " + string.Join(", ", monitors.Select(m => $"{m.Width}×{m.Height}"));
            }
        }

        public string OSVersion => Environment.OSVersion.ToString();

        public bool IsLogConsoleExpanded
        {
            get => _isLogConsoleExpanded;
            set => SetProperty(ref _isLogConsoleExpanded, value);
        }

        public string DiagnosticsLogText
        {
            get
            {
                try
                {
                    string logFolder = AppPaths.LogFolder;
                    string fullPath = Path.Combine(logFolder, $"app-{DateTime.Today:yyyyMMdd}.log");
                    if (File.Exists(fullPath))
                    {
                        var lines = File.ReadLines(fullPath).TakeLast(35);
                        return string.Join(Environment.NewLine, lines);
                    }
                    return "Файл журнала за сегодня пока пуст или не создан.";
                }
                catch (Exception ex)
                {
                    return $"Не удалось прочитать логи: {ex.Message}";
                }
            }
        }

        // --- 7. Раздел: ЦЕНТР ОБНОВЛЕНИЙ (VELOPACK) ---
        public bool IsCheckingUpdate
        {
            get => _isCheckingUpdate;
            set => SetProperty(ref _isCheckingUpdate, value);
        }

        public bool IsDownloadingUpdate
        {
            get => _isDownloadingUpdate;
            set => SetProperty(ref _isDownloadingUpdate, value);
        }

        public bool IsUpdateDownloaded
        {
            get => _isUpdateDownloaded;
            set => SetProperty(ref _isUpdateDownloaded, value);
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set => SetProperty(ref _isUpdateAvailable, value);
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set => SetProperty(ref _downloadProgress, value);
        }

        public string NewVersion
        {
            get => _newVersion;
            set => SetProperty(ref _newVersion, value);
        }

        public string UpdateStatusText
        {
            get => _updateStatusText;
            set => SetProperty(ref _updateStatusText, value);
        }

        public bool IncludePrereleases
        {
            get => _settings.IncludePrereleases;
            set
            {
                _settings.IncludePrereleases = value;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        // --- Команды ---
        public ICommand SelectPageCommand { get; }
        public ICommand ChooseFolderCommand { get; }
        public ICommand OpenDownloadFolderCommand { get; }
        public ICommand ClearCacheCommand { get; }
        public ICommand OpenLogsCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand CheckUpdatesCommand { get; }
        public ICommand DownloadUpdateCommand { get; }
        public ICommand ApplyAndRestartCommand { get; }
        public ICommand OpenReleaseUrlCommand { get; }
        public ICommand RunDiagnosticsCommand { get; }
        public ICommand ToggleLogConsoleCommand { get; }
        public ICommand SetWallhavenQueryCommand { get; }
        public ICommand AutoDetectResolutionCommand { get; }
        public ICommand OpenUrlCommand { get; }
        public ICommand CloseWindowCommand { get; }

        private async Task InitializeAsync()
        {
            await UpdateCacheStatsAsync();
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

        private void OnOpenDownloadFolder()
        {
            try
            {
                if (Directory.Exists(DownloadFolder))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{DownloadFolder}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    _notificationService.ShowError("Папка не найдена", "Указанная папка обоев не существует на диске.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка открытия папки обоев", ex);
            }
        }

        private async Task OnClearCacheAsync()
        {
            await _historyService.ClearCacheAsync();
            await UpdateCacheStatsAsync();
            _notificationService.ShowInfo("Кэш очищен", "Кэш обоев успешно очищен.");
        }

        private void OnOpenLogs()
        {
            try
            {
                string logFolder = AppPaths.LogFolder;
                if (Directory.Exists(logFolder))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{logFolder}\"",
                        UseShellExecute = true
                    });
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
                string logFolder = AppPaths.LogFolder;
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
            UpdateStatusText = "Проверка наличия обновлений Velopack...";

            try
            {
                var result = await _updateService.CheckForUpdatesAsync("l1ratch", "WallTray", IncludePrereleases);
                if (result.IsUpdateAvailable)
                {
                    IsUpdateAvailable = true;
                    NewVersion = result.NewVersion;
                    _releaseUrl = result.ReleaseUrl;
                    UpdateStatusText = $"Доступна новая версия v{result.NewVersion}!";
                }
                else
                {
                    IsUpdateAvailable = false;
                    UpdateStatusText = "У вас установлена последняя версия.";
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText = $"Ошибка проверки обновлений: {ex.Message}";
                _logger.LogError("Ошибка при проверке обновлений", ex);
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        private async Task OnDownloadUpdateAsync()
        {
            if (IsDownloadingUpdate || !IsUpdateAvailable) return;
            IsDownloadingUpdate = true;
            DownloadProgress = 0.0;
            UpdateStatusText = "Скачивание и подготовка пакета...";

            try
            {
                bool success = await _updateService.DownloadUpdateAsync(p =>
                {
                    DownloadProgress = p * 100.0;
                });

                if (success)
                {
                    IsUpdateDownloaded = true;
                    UpdateStatusText = "Обновление готово! Нажмите «Применить и перезапустить».";
                    _notificationService.ShowInfo("Обновление скачано", "Новая версия готова к установке.");
                }
                else
                {
                    UpdateStatusText = "Ошибка скачивания пакета обновления.";
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText = $"Сбой при загрузке: {ex.Message}";
                _logger.LogError("Ошибка загрузки обновления", ex);
            }
            finally
            {
                IsDownloadingUpdate = false;
            }
        }

        private void OnApplyAndRestart()
        {
            try
            {
                _updateService.ApplyUpdateAndRestart();
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка применения обновления Velopack", ex);
                _notificationService.ShowError("Ошибка обновления", ex.Message);
            }
        }

        private void OnOpenReleaseUrl()
        {
            if (!string.IsNullOrEmpty(_releaseUrl))
            {
                OnOpenUrl(_releaseUrl);
            }
            else
            {
                OnOpenUrl("https://github.com/l1ratch/WallTray/releases");
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
                    ? "Доступен (подключение к интернету активно)"
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
                    : $"Ошибка (HTTP {(int)response.StatusCode})";
            }
            catch (Exception ex)
            {
                BingApiStatus = $"Ошибка запроса ({ex.Message})";
            }

            OnPropertyChanged(nameof(ConnectedMonitors));
            OnPropertyChanged(nameof(DisplayResolution));
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
