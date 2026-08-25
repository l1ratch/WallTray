using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using BingWallTray.App.Models;
using BingWallTray.App.Services;
using BingWallTray.App.Utils;

namespace BingWallTray.App.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IHistoryService _historyService;
        private readonly IWallpaperService _wallpaperService;
        private readonly ISchedulerService _schedulerService;
        private readonly IStartupService _startupService;
        private readonly IGitHubUpdateService _updateService;
        private readonly ILoggingService _logger;
        private readonly INotificationService _notificationService;
        private readonly IDownloadService _downloadService;
        private readonly IBingService _bingService;
        private readonly IWallhavenService _wallhavenService;
        private readonly IWingetService _wingetService;
        private readonly AppState _appState;

        private BingImage? _selectedImage;
        private bool _isSelectedImageFavorite;
        private string _appVersion = "2026.8.0";
        private string _updateStatusText = "Проверить обновления";
        private bool _isCheckingUpdate = false;
        private bool _isUpdateAvailable = false;
        private string _releaseUrl = string.Empty;
        private string _activePage = "Gallery";

        private ObservableCollection<BingImage> _todayImages = new ObservableCollection<BingImage>();
        private List<string> _favoriteIds = new List<string>();
        private ObservableCollection<WallpaperHistoryItem> _favoritesCollection = new ObservableCollection<WallpaperHistoryItem>();

        public AppSettings Settings => _settingsService.CurrentSettings;
        public AppState AppState => _appState;

        // --- Навигация внутри Flyout ---

        public string ActivePage
        {
            get => _activePage;
            set
            {
                if (SetProperty(ref _activePage, value))
                {
                    OnPropertyChanged(nameof(SelectedTabIndex));
                    OnPropertyChanged(nameof(ShowBackButton));
                    OnPropertyChanged(nameof(ShowLogo));
                    OnPropertyChanged(nameof(PageTitle));
                    OnPropertyChanged(nameof(IsGalleryActive));
                    OnPropertyChanged(nameof(IsBottomNavVisible));
                    NotifyNavStateChanged();
                }
            }
        }

        public int SelectedTabIndex
        {
            get
            {
                return ActivePage switch
                {
                    "About" => 1,
                    "Favorites" => 2,
                    "ImageDetails" => 3,
                    _ => 0
                };
            }
            set
            {
                ActivePage = value switch
                {
                    1 => "About",
                    2 => "Favorites",
                    3 => "ImageDetails",
                    _ => "Gallery"
                };
                OnPropertyChanged();
            }
        }

        public bool IsGalleryActive => ActivePage == "Gallery";
        public bool ShowBackButton => ActivePage == "ImageDetails";
        public bool ShowLogo => ActivePage != "ImageDetails";
        public bool IsBottomNavVisible => ActivePage != "ImageDetails";

        public string PageTitle
        {
            get
            {
                return ActivePage switch
                {
                    "ImageDetails" => "Детали обоев",
                    _ => "WallTray"
                };
            }
        }

        public string StatusMessageText => string.IsNullOrWhiteSpace(_appState.StatusMessage) ? "Ожидание" : _appState.StatusMessage;

        // --- Коллекции обоев ---

        private string _currentSource = "Bing";
        private ObservableCollection<BingImage> _displayedImages = new ObservableCollection<BingImage>();
        private List<BingImage> _wallhavenImages = new List<BingImage>();
        private List<BingImage> _historicalArchiveImages = new List<BingImage>();
        private int _historicalLoadedCount = 0;
        private bool _isArchiveLoading = false;

        public string CurrentSource
        {
            get => _currentSource;
            set
            {
                if (SetProperty(ref _currentSource, value))
                {
                    OnPropertyChanged(nameof(IsBingSourceActive));
                    OnPropertyChanged(nameof(IsWallhavenSourceActive));
                    NotifyNavStateChanged();
                    UpdateDisplayedImages();
                }
            }
        }

        public bool IsBingSourceActive => CurrentSource == "Bing";
        public bool IsWallhavenSourceActive => CurrentSource == "Wallhaven";
        public bool ShowSourceSelector => Settings.EnableWallhaven;
        public bool IsMultipleSourcesEnabled => Settings.EnableWallhaven;
        public bool IsSingleSourceEnabled => !IsMultipleSourcesEnabled;

        public bool IsNavGalleryActive => ActivePage == "Gallery";
        public bool IsNavBingActive => ActivePage == "Gallery" && CurrentSource == "Bing";
        public bool IsNavWallhavenActive => ActivePage == "Gallery" && CurrentSource == "Wallhaven";
        public bool IsNavFavoritesActive => ActivePage == "Favorites";
        public bool IsNavMoreActive => ActivePage == "About";

        public string BingNavTabTitle => IsMultipleSourcesEnabled ? "Bing" : "Главная";

        private void NotifyNavStateChanged()
        {
            OnPropertyChanged(nameof(IsNavGalleryActive));
            OnPropertyChanged(nameof(IsNavBingActive));
            OnPropertyChanged(nameof(IsNavWallhavenActive));
            OnPropertyChanged(nameof(IsNavFavoritesActive));
            OnPropertyChanged(nameof(IsNavMoreActive));
            OnPropertyChanged(nameof(IsMultipleSourcesEnabled));
            OnPropertyChanged(nameof(IsSingleSourceEnabled));
            OnPropertyChanged(nameof(BingNavTabTitle));
            OnPropertyChanged(nameof(StatusBadgeText));
        }

        private bool _isStatusPopupOpen;
        public bool IsStatusPopupOpen
        {
            get => _isStatusPopupOpen;
            set => SetProperty(ref _isStatusPopupOpen, value);
        }

        public string StatusBadgeText
        {
            get
            {
                if (IsStatusBusy)
                {
                    if (StatusMessage.Contains("Установка", StringComparison.OrdinalIgnoreCase) || StatusMessage.Contains("Применение", StringComparison.OrdinalIgnoreCase))
                        return "Установка";
                    if (StatusMessage.Contains("Сохранение", StringComparison.OrdinalIgnoreCase))
                        return "Сохранение";
                    if (StatusMessage.Contains("Загрузка", StringComparison.OrdinalIgnoreCase) || StatusMessage.Contains("скачивание", StringComparison.OrdinalIgnoreCase) || StatusMessage.Contains("Подгрузка", StringComparison.OrdinalIgnoreCase) || StatusMessage.Contains("Запрос", StringComparison.OrdinalIgnoreCase))
                        return "Загрузка";
                    return "Проверка";
                }
                if (IsStatusError) return "Ошибка";
                return "Готово";
            }
        }

        // Крупный заголовок для панели подробностей статуса
        public string StatusHeadline
        {
            get
            {
                if (IsStatusBusy) return "В работе";
                if (IsStatusError) return "Требуется внимание";
                return "Всё работает";
            }
        }

        public bool IsStatusBusy => IsChecking || IsDownloading;
        public bool IsStatusError => StatusMessage.StartsWith("Ошибка", StringComparison.OrdinalIgnoreCase)
            || StatusMessage.StartsWith("Не удалось", StringComparison.OrdinalIgnoreCase)
            || StatusMessage.Contains("недоступна", StringComparison.OrdinalIgnoreCase);
        public string StatusBadgeIcon => IsStatusBusy ? "↻" : IsStatusError ? "!" : "✓";
        public string StatusBadgeForeground => IsStatusBusy ? "#4ea5f5" : IsStatusError ? "#f05d5e" : "#54b36b";

        public ObservableCollection<BingImage> DisplayedImages
        {
            get => _displayedImages;
            set => SetProperty(ref _displayedImages, value);
        }

        public ObservableCollection<BingImage> TodayImages
        {
            get => _todayImages;
            set
            {
                SetProperty(ref _todayImages, value);
                OnPropertyChanged(nameof(HasImages));
            }
        }

        public bool HasImages => TodayImages != null && TodayImages.Count > 0;

        private bool _isFavoritesLoading = false;
        public bool IsFavoritesLoading
        {
            get => _isFavoritesLoading;
            set
            {
                if (SetProperty(ref _isFavoritesLoading, value))
                {
                    OnPropertyChanged(nameof(ShowFavoritesList));
                    OnPropertyChanged(nameof(ShowFavoritesEmpty));
                }
            }
        }

        public ObservableCollection<WallpaperHistoryItem> FavoritesCollection
        {
            get => _favoritesCollection;
            set
            {
                if (SetProperty(ref _favoritesCollection, value))
                {
                    OnPropertyChanged(nameof(HasFavorites));
                    OnPropertyChanged(nameof(IsFavoritesEmpty));
                    OnPropertyChanged(nameof(ShowFavoritesList));
                    OnPropertyChanged(nameof(ShowFavoritesEmpty));
                }
            }
        }

        public bool HasFavorites => FavoritesCollection != null && FavoritesCollection.Count > 0;
        public bool IsFavoritesEmpty => !HasFavorites;
        public bool ShowFavoritesList => !IsFavoritesLoading && HasFavorites;
        public bool ShowFavoritesEmpty => !IsFavoritesLoading && IsFavoritesEmpty;

        public BingImage? SelectedImage
        {
            get => _selectedImage;
            set
            {
                if (SetProperty(ref _selectedImage, value))
                {
                    OnPropertyChanged(nameof(IsImageSelected));
                    OnPropertyChanged(nameof(IsSelectedImageApplied));
                    UpdateSelectedImageFavoriteStatus();
                }
            }
        }

        public bool IsImageSelected => SelectedImage != null;

        public bool IsSelectedImageApplied
        {
            get => SelectedImage != null && GetImageId(SelectedImage) == Settings.LastAppliedImageId;
        }

        public bool IsSelectedImageFavorite
        {
            get => _isSelectedImageFavorite;
            set => SetProperty(ref _isSelectedImageFavorite, value);
        }

        private BingImage? _selectedDetailsImage;
        public BingImage? SelectedDetailsImage
        {
            get => _selectedDetailsImage;
            set
            {
                if (SetProperty(ref _selectedDetailsImage, value))
                {
                    OnPropertyChanged(nameof(IsDetailsImageSelected));
                    OnPropertyChanged(nameof(IsSelectedDetailsImageApplied));
                    OnPropertyChanged(nameof(IsSelectedDetailsImageFavorite));
                }
            }
        }

        public bool IsDetailsImageSelected => SelectedDetailsImage != null;

        public bool IsSelectedDetailsImageApplied
        {
            get => SelectedDetailsImage != null && GetImageId(SelectedDetailsImage) == Settings.LastAppliedImageId;
        }

        public bool IsSelectedDetailsImageFavorite
        {
            get => SelectedDetailsImage != null && _favoriteIds.Contains(GetImageId(SelectedDetailsImage));
        }

        // --- Состояния из AppState ---

        public bool IsChecking => _appState.IsChecking;
        public bool IsDownloading => _appState.IsDownloading;
        public string StatusMessage => _appState.StatusMessage;

        // --- Обернутые свойства настроек для привязки к UI ---

        public bool AutoChangeEnabled
        {
            get => Settings.AutoChangeEnabled;
            set
            {
                if (Settings.AutoChangeEnabled != value)
                {
                    Settings.AutoChangeEnabled = value;
                    SaveSettings();
                    OnPropertyChanged();
                    _schedulerService.UpdateInterval();
                }
            }
        }

        public bool AutoCheckBingEnabled
        {
            get => Settings.AutoCheckBingEnabled;
            set
            {
                if (Settings.AutoCheckBingEnabled != value)
                {
                    Settings.AutoCheckBingEnabled = value;
                    SaveSettings();
                    OnPropertyChanged();
                    _schedulerService.UpdateInterval();
                }
            }
        }

        public string LastCheckStatusText
        {
            get
            {
                if (string.IsNullOrEmpty(Settings.LastCheckUtc))
                {
                    return "Последнее обновление: не проводилось";
                }
                if (DateTime.TryParse(Settings.LastCheckUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                {
                    return $"Последнее обновление: {dt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
                }
                return $"Последнее обновление: {Settings.LastCheckUtc}";
            }
        }

        public string ShortLastCheckTime
        {
            get
            {
                if (string.IsNullOrEmpty(Settings.LastCheckUtc))
                {
                    return "--:--";
                }
                if (DateTime.TryParse(Settings.LastCheckUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                {
                    return dt.ToLocalTime().ToString("HH:mm");
                }
                return "--:--";
            }
        }

        public string AutoChangeSource
        {
            get => Settings.AutoChangeSource;
            set
            {
                if (Settings.AutoChangeSource != value)
                {
                    Settings.AutoChangeSource = value;
                    SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        public string AutoChangeTrigger
        {
            get => Settings.AutoChangeTrigger;
            set
            {
                if (Settings.AutoChangeTrigger != value)
                {
                    Settings.AutoChangeTrigger = value;
                    SaveSettings();
                    OnPropertyChanged();
                    _schedulerService.UpdateInterval();
                }
            }
        }

        public bool Paused
        {
            get => Settings.Paused;
            set
            {
                if (Settings.Paused != value)
                {
                    Settings.Paused = value;
                    SaveSettings();
                    OnPropertyChanged();
                    _schedulerService.UpdateInterval();
                    _notificationService.ShowInfo("Режим изменен", value ? "Автопроверка приостановлена." : "Автопроверка возобновлена.");
                }
            }
        }

        public bool Locked
        {
            get => Settings.Locked;
            set
            {
                if (Settings.Locked != value)
                {
                    Settings.Locked = value;
                    SaveSettings();
                    OnPropertyChanged();
                    _notificationService.ShowInfo("Режим изменен", value ? "Текущие обои зафиксированы." : "Фиксация обоев отключена.");
                }
            }
        }

        public bool StartWithWindows
        {
            get => Settings.StartWithWindows;
            set
            {
                if (Settings.StartWithWindows != value)
                {
                    Settings.StartWithWindows = value;
                    _startupService.SetStartup(value);
                    SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        public bool StartMinimizedToTray
        {
            get => Settings.StartMinimizedToTray;
            set
            {
                if (Settings.StartMinimizedToTray != value)
                {
                    Settings.StartMinimizedToTray = value;
                    SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowNotifications
        {
            get => Settings.ShowNotifications;
            set
            {
                if (Settings.ShowNotifications != value)
                {
                    Settings.ShowNotifications = value;
                    SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        public bool EnableHistoricalArchive
        {
            get => Settings.EnableHistoricalArchive;
            set
            {
                if (Settings.EnableHistoricalArchive != value)
                {
                    Settings.EnableHistoricalArchive = value;
                    SaveSettings();
                    OnPropertyChanged();
                    if (value)
                    {
                        _ = LoadHistoricalArchiveAsync();
                    }
                }
            }
        }

        public bool EnableWallhaven
        {
            get => Settings.EnableWallhaven;
            set
            {
                if (Settings.EnableWallhaven != value)
                {
                    Settings.EnableWallhaven = value;
                    SaveSettings();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowSourceSelector));
                    NotifyNavStateChanged();
                    if (!value && CurrentSource == "Wallhaven")
                    {
                        CurrentSource = "Bing";
                    }
                    _ = LoadImagesAsync(forceReload: true);
                }
            }
        }

        public string WallhavenQuery
        {
            get => Settings.WallhavenQuery;
            set
            {
                if (Settings.WallhavenQuery != value)
                {
                    Settings.WallhavenQuery = value;
                    SaveSettings();
                    OnPropertyChanged();
                    _ = LoadImagesAsync(forceReload: true);
                }
            }
        }

        public string WallhavenCategories
        {
            get => Settings.WallhavenCategories;
            set
            {
                if (Settings.WallhavenCategories != value)
                {
                    Settings.WallhavenCategories = value;
                    SaveSettings();
                    OnPropertyChanged();
                    _ = LoadImagesAsync(forceReload: true);
                }
            }
        }

        public string WallhavenResolutions
        {
            get => Settings.WallhavenResolutions;
            set
            {
                if (Settings.WallhavenResolutions != value)
                {
                    Settings.WallhavenResolutions = value;
                    SaveSettings();
                    OnPropertyChanged();
                    _ = LoadImagesAsync(forceReload: true);
                }
            }
        }

        public bool LoggingEnabled
        {
            get => Settings.LoggingEnabled;
            set
            {
                if (Settings.LoggingEnabled != value)
                {
                    Settings.LoggingEnabled = value;
                    SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        public string LogLevelString
        {
            get => Settings.LogLevel;
            set
            {
                if (Settings.LogLevel != value)
                {
                    Settings.LogLevel = value;
                    SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        public string LastUpdateText
        {
            get
            {
                if (string.IsNullOrEmpty(Settings.LastCheckUtc))
                {
                    return "Обновление еще не производилось";
                }

                if (DateTime.TryParse(Settings.LastCheckUtc, out var dt))
                {
                    var localTime = dt.ToLocalTime();
                    return $"Последнее обновление: {localTime:HH:mm:ss} ({localTime:dd.MM.yyyy})";
                }

                return "Обновление: неизвестно";
            }
        }

        public string WallpaperStyleString
        {
            get => Settings.WallpaperStyle;
            set
            {
                if (Settings.WallpaperStyle != value)
                {
                    Settings.WallpaperStyle = value;
                    SaveSettings();
                    OnPropertyChanged();
                    // Если есть текущие обои, можно применить стиль сразу
                    if (!string.IsNullOrEmpty(_appState.LastAppliedPath) && File.Exists(_appState.LastAppliedPath))
                    {
                        if (Enum.TryParse<WallpaperStyle>(value, true, out var style))
                        {
                            _wallpaperService.SetWallpaper(_appState.LastAppliedPath, style);
                        }
                    }
                }
            }
        }

        private string _selectedIntervalPreset = "12h";
        public string SelectedIntervalPreset
        {
            get => _selectedIntervalPreset;
            set
            {
                if (_selectedIntervalPreset != value)
                {
                    _selectedIntervalPreset = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCustomIntervalVisible));
                    
                    if (value != "Custom")
                    {
                        Settings.AutoChangeInterval = value;
                        SaveSettings();
                        _schedulerService.UpdateInterval();
                    }
                    else
                    {
                        Settings.AutoChangeInterval = CustomIntervalString;
                        SaveSettings();
                        _schedulerService.UpdateInterval();
                    }
                }
            }
        }

        private string _customIntervalString = "15m";
        public string CustomIntervalString
        {
            get => _customIntervalString;
            set
            {
                if (_customIntervalString != value)
                {
                    _customIntervalString = value;
                    OnPropertyChanged();
                    
                    if (SelectedIntervalPreset == "Custom")
                    {
                        Settings.AutoChangeInterval = value;
                        SaveSettings();
                        _schedulerService.UpdateInterval();
                    }
                }
            }
        }

        public bool IsCustomIntervalVisible => SelectedIntervalPreset == "Custom";

        public int CheckIntervalHours
        {
            get => Settings.CheckIntervalHours;
            set
            {
                if (Settings.CheckIntervalHours != value)
                {
                    Settings.CheckIntervalHours = value;
                    SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        public string Market
        {
            get => Settings.Market;
            set
            {
                if (Settings.Market != value)
                {
                    Settings.Market = value;
                    SaveSettings();
                    OnPropertyChanged();
                    // Обновляем список картинок с новым регионом
                    _ = OnCheckNowAsync();
                }
            }
        }

        public string DownloadFolder
        {
            get => Settings.DownloadFolder;
            set
            {
                if (Settings.DownloadFolder != value)
                {
                    Settings.DownloadFolder = value;
                    SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        // --- Информация о версии и обновлениях ---

        public string AppVersion
        {
            get => _appVersion;
            set => SetProperty(ref _appVersion, value);
        }

        public string UpdateStatusText
        {
            get => _updateStatusText;
            set => SetProperty(ref _updateStatusText, value);
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

        public string ReleaseUrl
        {
            get => _releaseUrl;
            set => SetProperty(ref _releaseUrl, value);
        }

        // --- Команды ---

        public ICommand SetWallpaperCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand CheckNowCommand { get; }
        public ICommand ForceCheckBingCommand { get; }
        public ICommand ForceReloadArchiveCommand { get; }
        public ICommand ClearCacheCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand CheckUpdateCommand { get; }
        public ICommand OpenReleaseUrlCommand { get; }
        public ICommand RefreshImagesCommand { get; }
        
        // Команды навигации
        public ICommand GoToGalleryCommand { get; }
        public ICommand GoToSettingsCommand { get; }
        public ICommand GoToAboutCommand { get; }
        public ICommand ChooseFolderCommand { get; }
        public ICommand GoToFavoritesCommand { get; }
        public ICommand ApplyFavoriteCommand { get; }
        public ICommand RemoveFavoriteItemCommand { get; }
        public ICommand OpenFavoriteFolderCommand { get; }
        public ICommand ToggleFavoriteForItemCommand { get; }
        public ICommand GoToImageDetailsCommand { get; }
        public ICommand DoubleClickedImageCommand { get; }
        public ICommand SetDetailsWallpaperCommand { get; }
        public ICommand ToggleDetailsFavoriteCommand { get; }
        public ICommand OpenUrlCommand { get; }

        public ICommand OpenLogsCommand { get; }
        public ICommand ClearLogsCommand { get; }

        public ICommand SwitchSourceCommand { get; }
        public ICommand LoadMoreHistoricalImagesCommand { get; }
        public ICommand AddKeywordTagCommand { get; }
        public ICommand ClearKeywordQueryCommand { get; }

        public ICommand OpenPageCommand { get; }
        public ICommand SelectSourceAndGalleryCommand { get; }
        public ICommand ToggleStatusPopupCommand { get; }
        public ICommand OpenSettingsWindowCommand { get; }

        public MainViewModel(
            ISettingsService settingsService,
            IHistoryService historyService,
            IDownloadService downloadService,
            IWallpaperService wallpaperService,
            ISchedulerService schedulerService,
            IStartupService startupService,
            IGitHubUpdateService updateService,
            ILoggingService logger,
            INotificationService notificationService,
            AppState appState,
            IBingService bingService,
            IWallhavenService wallhavenService,
            IWingetService wingetService)
        {
            _settingsService = settingsService;
            _historyService = historyService;
            _downloadService = downloadService;
            _wallpaperService = wallpaperService;
            _schedulerService = schedulerService;
            _startupService = startupService;
            _updateService = updateService;
            _logger = logger;
            _notificationService = notificationService;
            _appState = appState;
            _appState.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppState.StatusMessage) || e.PropertyName == nameof(AppState.IsDownloading) || e.PropertyName == nameof(AppState.IsChecking))
                {
                    OnPropertyChanged(nameof(StatusMessage));
                    OnPropertyChanged(nameof(StatusMessageText));
                    OnPropertyChanged(nameof(IsDownloading));
                    OnPropertyChanged(nameof(IsChecking));
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(StatusHeadline));
                    OnPropertyChanged(nameof(IsStatusBusy));
                    OnPropertyChanged(nameof(IsStatusError));
                    OnPropertyChanged(nameof(StatusBadgeIcon));
                    OnPropertyChanged(nameof(StatusBadgeForeground));
                }
            };
            _bingService = bingService;
            _wallhavenService = wallhavenService;
            _wingetService = wingetService;

            var informationalVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (informationalVersion != null && informationalVersion.Contains("+"))
            {
                informationalVersion = informationalVersion.Split('+')[0];
            }

            AppVersion = informationalVersion ?? "2026.8.0";

            SetWallpaperCommand = new RelayCommand(async () => await OnSetWallpaperAsync(), () => IsImageSelected);
            ToggleFavoriteCommand = new RelayCommand(async () => await OnToggleFavoriteAsync(), () => IsImageSelected);
            CheckNowCommand = new RelayCommand(async () => await OnCheckNowAsync());
            ForceCheckBingCommand = new RelayCommand(async () => await OnForceCheckBingAsync());
            ForceReloadArchiveCommand = new RelayCommand(async () => await OnForceReloadArchiveAsync(), () => Settings.EnableHistoricalArchive);
            ClearCacheCommand = new RelayCommand(async () => await OnClearCacheAsync());
            OpenFolderCommand = new RelayCommand(OnOpenFolder);
            OpenLogsCommand = new RelayCommand(OnOpenLogs);
            ClearLogsCommand = new RelayCommand(async () => await OnClearLogsAsync());
            ExitCommand = new RelayCommand(OnExit);
            CheckUpdateCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
            OpenReleaseUrlCommand = new RelayCommand(OnOpenReleaseUrl, () => !string.IsNullOrEmpty(ReleaseUrl));
            RefreshImagesCommand = new RelayCommand(async () => await LoadImagesAsync());

            SwitchSourceCommand = new RelayCommand<string>(OnSwitchSource);
            LoadMoreHistoricalImagesCommand = new RelayCommand(async () => await OnLoadMoreHistoricalImagesAsync());
            AddKeywordTagCommand = new RelayCommand<string>(OnAddKeywordTag);
            ClearKeywordQueryCommand = new RelayCommand(OnClearKeywordQuery);

            GoToGalleryCommand = new RelayCommand(() => ActivePage = "Gallery");
            GoToSettingsCommand = new RelayCommand(OnOpenSettingsWindow);
            GoToAboutCommand = new RelayCommand(() => ActivePage = "About");
            ChooseFolderCommand = new RelayCommand(OnChooseFolder);

            OpenPageCommand = new RelayCommand<string>(OnOpenPage);
            SelectSourceAndGalleryCommand = new RelayCommand<string>(OnSelectSourceAndGallery);
            ToggleStatusPopupCommand = new RelayCommand(OnToggleStatusPopup);
            OpenSettingsWindowCommand = new RelayCommand(OnOpenSettingsWindow);

            GoToFavoritesCommand = new RelayCommand(async () =>
            {
                ActivePage = "Favorites";
                await LoadFavoritesPageAsync();
            });
            ApplyFavoriteCommand = new RelayCommand<WallpaperHistoryItem>(async (item) => await OnApplyFavoriteAsync(item));
            RemoveFavoriteItemCommand = new RelayCommand<WallpaperHistoryItem>(async (item) => await OnRemoveFavoriteItemAsync(item));
            OpenFavoriteFolderCommand = new RelayCommand<WallpaperHistoryItem>(OnOpenFavoriteFolder);

            ToggleFavoriteForItemCommand = new RelayCommand<BingImage>(async (img) => await OnToggleFavoriteForItemAsync(img));
            GoToImageDetailsCommand = new RelayCommand<BingImage>((img) => OnGoToImageDetails(img));
            DoubleClickedImageCommand = new RelayCommand<BingImage>(async (img) => await OnDoubleClickedImageAsync(img));
            SetDetailsWallpaperCommand = new RelayCommand(async () => await OnSetDetailsWallpaperAsync(), () => IsDetailsImageSelected);
            ToggleDetailsFavoriteCommand = new RelayCommand(async () => await OnToggleDetailsFavoriteAsync(), () => IsDetailsImageSelected);
            OpenUrlCommand = new RelayCommand<string>(OnOpenUrl);

            // Инициализация интервала автосмены на основе сохраненных настроек
            string savedInterval = Settings.AutoChangeInterval;
            var presets = new List<string> { "30m", "1h", "6h", "12h", "24h" };
            if (presets.Contains(savedInterval))
            {
                _selectedIntervalPreset = savedInterval;
            }
            else
            {
                _selectedIntervalPreset = "Custom";
                _customIntervalString = savedInterval;
            }
        }

        private void SaveSettings()
        {
            _ = Task.Run(() => _settingsService.SaveAsync(Settings));
        }

        private bool _isLoadedOnce = false;

        public async Task LoadImagesAsync(bool forceReload = false)
        {
            var favs = await _historyService.GetFavoritesAsync();
            _favoriteIds = favs.Select(f => f.Id).ToList();

            if (_appState.TodayImages == null || _appState.TodayImages.Count == 0)
            {
                await _schedulerService.StartAutoCheckAsync(isManual: false, isStartup: !_isLoadedOnce);
            }

            string activeId = Settings.LastAppliedImageId;

            if (_isLoadedOnce && !forceReload)
            {
                UpdateAppliedStatus(activeId);
                UpdateSelectedImageFavoriteStatus();
                OnPropertyChanged(nameof(IsSelectedImageApplied));
                OnPropertyChanged(nameof(IsSelectedDetailsImageApplied));
                return;
            }

            var list = _appState.TodayImages?.ToList() ?? new List<BingImage>();
            foreach (var img in list)
            {
                string id = GetImageId(img);
                img.IsApplied = (id == activeId);
                img.IsFavorite = _favoriteIds.Contains(id);
                img.Source = "Bing";
            }

            TodayImages = new ObservableCollection<BingImage>(list);



            if (Settings.EnableWallhaven)
            {
                try
                {
                    var wallhaven = await _wallhavenService.GetWallhavenImagesAsync(Settings.WallhavenQuery, Settings.WallhavenCategories, Settings.WallhavenResolutions);
                    foreach (var img in wallhaven)
                    {
                        string id = GetImageId(img);
                        img.IsApplied = (id == activeId);
                        img.IsFavorite = _favoriteIds.Contains(id);
                        img.Source = "Wallhaven";
                    }

                    _wallhavenImages = wallhaven;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Ошибка при получении обоев Wallhaven во VM", ex);
                }
            }

            if (Settings.EnableHistoricalArchive && _historicalArchiveImages.Count == 0 && !_isArchiveLoading)
            {
                _ = LoadHistoricalArchiveAsync();
            }

            UpdateDisplayedImages();

            if (SelectedImage == null || !DisplayedImages.Contains(SelectedImage))
            {
                SelectedImage = DisplayedImages.FirstOrDefault();
            }
            else
            {
                UpdateSelectedImageFavoriteStatus();
            }

            OnPropertyChanged(nameof(IsChecking));
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(IsSelectedImageApplied));
            OnPropertyChanged(nameof(LastUpdateText));
            _isLoadedOnce = true;
        }

        private async Task LoadHistoricalArchiveAsync()
        {
            if (_isArchiveLoading) return;
            _isArchiveLoading = true;
            _logger.LogInfo("Начало фоновой загрузки исторического архива обоев...");

            try
            {
                var archive = await _bingService.GetHistoricalArchiveImagesAsync(Settings.Market, Settings.UseUhd);
                if (archive != null && archive.Count > 0)
                {
                    string activeId = Settings.LastAppliedImageId;
                    foreach (var img in archive)
                    {
                        string id = GetImageId(img);
                        img.IsApplied = (id == activeId);
                        img.IsFavorite = _favoriteIds.Contains(id);
                        img.Source = "Bing";
                    }

                    _historicalArchiveImages = archive;
                    _historicalLoadedCount = 10;

                    if (CurrentSource == "Bing")
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            UpdateDisplayedImages();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при фоновой загрузке архива", ex);
            }
            finally
            {
                _isArchiveLoading = false;
            }
        }

        private void OnSwitchSource(string source)
        {
            if (string.IsNullOrEmpty(source)) return;
            CurrentSource = source;
        }

        private async Task OnLoadMoreHistoricalImagesAsync()
        {
            if (CurrentSource == "Wallhaven")
            {
                if (_isArchiveLoading) return;
                _isArchiveLoading = true;
                _appState.StatusMessage = "Подгрузка обоев Wallhaven...";
                OnPropertyChanged(nameof(StatusMessage));

                try
                {
                    var moreImages = await _wallhavenService.GetWallhavenImagesAsync(Settings.WallhavenQuery, Settings.WallhavenCategories, Settings.WallhavenResolutions);
                    if (moreImages != null && moreImages.Count > 0)
                    {
                        string activeId = Settings.LastAppliedImageId;
                        foreach (var img in moreImages)
                        {
                            var imgId = GetImageId(img);
                            if (!_wallhavenImages.Any(x => GetImageId(x) == imgId))
                            {
                                img.IsApplied = (imgId == activeId);
                                img.IsFavorite = _favoriteIds.Contains(imgId);
                                _wallhavenImages.Add(img);
                            }
                        }

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            UpdateDisplayedImages();
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Ошибка при подгрузке обоев Wallhaven", ex);
                }
                finally
                {
                    _isArchiveLoading = false;
                    _appState.StatusMessage = string.Empty;
                    OnPropertyChanged(nameof(StatusMessage));
                }
                return;
            }

            if (CurrentSource != "Bing" || !Settings.EnableHistoricalArchive) return;
            if (_isArchiveLoading) return;

            if (_historicalArchiveImages.Count == 0)
            {
                await LoadHistoricalArchiveAsync();
                return;
            }

            if (_historicalLoadedCount >= _historicalArchiveImages.Count)
            {
                return;
            }

            _historicalLoadedCount = Math.Min(_historicalLoadedCount + 10, _historicalArchiveImages.Count);
            _logger.LogInfo($"Загружено больше архивных обоев. Всего: {_historicalLoadedCount}");

            UpdateDisplayedImages();
        }

        private void UpdateDisplayedImages()
        {
            if (CurrentSource == "Wallhaven")
            {
                DisplayedImages = new ObservableCollection<BingImage>(_wallhavenImages);
            }
            else
            {
                var list = new List<BingImage>(TodayImages);
                foreach (var img in _historicalArchiveImages.Take(_historicalLoadedCount))
                {
                    var imgId = GetImageId(img);
                    if (!list.Any(i => GetImageId(i) == imgId))
                    {
                        list.Add(img);
                    }
                }
                DisplayedImages = new ObservableCollection<BingImage>(list);
            }
        }

        private void UpdateSelectedImageFavoriteStatus()
        {
            if (SelectedImage == null)
            {
                IsSelectedImageFavorite = false;
                return;
            }
            string id = GetImageId(SelectedImage);
            IsSelectedImageFavorite = _favoriteIds.Contains(id);
        }

        private async Task OnSetWallpaperAsync()
        {
            if (SelectedImage == null) return;

            _appState.StatusMessage = "Установка обоев...";
            _appState.IsDownloading = true;
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(StatusMessage));

            try
            {
                string localPath = await _downloadService.DownloadImageAsync(SelectedImage, Settings.DownloadFolder);
                _appState.LastAppliedPath = localPath;

                if (Enum.TryParse<WallpaperStyle>(Settings.WallpaperStyle, true, out var style))
                {
                    // OK
                }
                else
                {
                    style = WallpaperStyle.Fill;
                }

                bool success = await _wallpaperService.SetWallpaperAsync(localPath, style);
                if (success)
                {
                    string id = GetImageId(SelectedImage);
                    Settings.LastAppliedImageId = id;
                    Settings.LastAutoAppliedDate = TodayImages.FirstOrDefault()?.StartDate ?? string.Empty;
                    SaveSettings();

                    // Сохраняем информацию о применении обоев в базу кэша
                    await _historyService.AddToCacheAsync(SelectedImage, SelectedImage.Source ?? "Bing", isApplied: true);

                    // Обновляем статус IsApplied для всех картинок подборки в памяти
                    UpdateAppliedStatus(id);
                    OnPropertyChanged(nameof(IsSelectedImageApplied));
                    OnPropertyChanged(nameof(IsSelectedDetailsImageApplied));

                    _notificationService.ShowInfo("Обои изменены", SelectedImage.Title);
                    await _historyService.CleanOldNonFavoriteImagesAsync(Settings.DownloadFolder, localPath);
                }
                else
                {
                    _notificationService.ShowError("Ошибка", "Не удалось применить выбранные обои.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка ручной установки обоев", ex);
                _notificationService.ShowError("Ошибка", "Не удалось скачать или применить обои.");
            }
            finally
            {
                _appState.IsDownloading = false;
                _appState.StatusMessage = string.Empty;
                OnPropertyChanged(nameof(IsDownloading));
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        private async Task OnToggleFavoriteAsync()
        {
            if (SelectedImage == null) return;
            await OnToggleFavoriteForItemAsync(SelectedImage);
        }

        private async Task OnToggleFavoriteForItemAsync(BingImage img)
        {
            if (img == null) return;

            string id = GetImageId(img);
            bool isFav = _favoriteIds.Contains(id);

            if (isFav)
            {
                await _historyService.RemoveFavoriteAsync(id);
                _favoriteIds.Remove(id);
                img.IsFavorite = false;
                _notificationService.ShowInfo("Избранное", "Изображение удалено из избранного.");
            }
            else
            {
                _appState.StatusMessage = "Сохранение в избранное...";
                try
                {
                    string localPath = await _downloadService.DownloadImageAsync(img, Settings.DownloadFolder);

                    var item = new WallpaperHistoryItem
                    {
                        Id = id,
                        Date = img.StartDate,
                        Market = Settings.Market,
                        Title = img.Title,
                        Copyright = img.Copyright,
                        CopyrightLink = img.CopyrightLink,
                        RemoteUrl = img.Url,
                        LocalPath = localPath,
                        DownloadedAtUtc = DateTime.UtcNow,
                        IsFavorite = true
                    };

                    await _historyService.AddOrUpdateFavoriteAsync(item);
                    _favoriteIds.Add(id);
                    img.IsFavorite = true;
                    _notificationService.ShowInfo("Избранное", "Изображение добавлено в избранное.");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Не удалось сохранить изображение в избранное", ex);
                    _notificationService.ShowError("Ошибка", "Не удалось сохранить файл.");
                }
                finally
                {
                    _appState.StatusMessage = string.Empty;
                }
            }

            if (SelectedImage != null && GetImageId(SelectedImage) == id)
            {
                UpdateSelectedImageFavoriteStatus();
            }
            if (SelectedDetailsImage != null && GetImageId(SelectedDetailsImage) == id)
            {
                OnPropertyChanged(nameof(IsSelectedDetailsImageFavorite));
            }
        }

        private void OnGoToImageDetails(BingImage img)
        {
            if (img == null) return;
            SelectedDetailsImage = img;
            ActivePage = "ImageDetails";
        }

        private async Task OnDoubleClickedImageAsync(BingImage img)
        {
            if (img == null) return;
            SelectedImage = img;
            await OnSetWallpaperAsync();
        }

        private async Task OnSetDetailsWallpaperAsync()
        {
            if (SelectedDetailsImage == null) return;
            SelectedImage = SelectedDetailsImage;
            await OnSetWallpaperAsync();
            OnPropertyChanged(nameof(IsSelectedDetailsImageApplied));
        }

        private async Task OnToggleDetailsFavoriteAsync()
        {
            if (SelectedDetailsImage == null) return;
            await OnToggleFavoriteForItemAsync(SelectedDetailsImage);
            OnPropertyChanged(nameof(IsSelectedDetailsImageFavorite));
        }

        public async Task LoadFavoritesPageAsync()
        {
            if (FavoritesCollection == null || FavoritesCollection.Count == 0)
            {
                IsFavoritesLoading = true;
            }

            try
            {
                var favs = await Task.Run(async () => await _historyService.GetFavoritesAsync());

                bool unchanged = FavoritesCollection != null
                    && FavoritesCollection.Count == favs.Count
                    && FavoritesCollection.Select(f => f.Id).SequenceEqual(favs.Select(f => f.Id));

                if (!unchanged)
                {
                    FavoritesCollection = new ObservableCollection<WallpaperHistoryItem>(favs);
                }
            }
            finally
            {
                IsFavoritesLoading = false;
            }
        }

        private async Task OnApplyFavoriteAsync(WallpaperHistoryItem item)
        {
            if (item == null) return;

            string localPath = item.LocalPath;
            if (!string.IsNullOrEmpty(localPath) &&
                (localPath.Contains(@"\OneDrive\", StringComparison.OrdinalIgnoreCase) ||
                 localPath.Contains(@"\Pictures\", StringComparison.OrdinalIgnoreCase) ||
                 localPath.Contains(@"\Изображения\", StringComparison.OrdinalIgnoreCase)))
            {
                string targetPath = Path.Combine(AppPaths.DefaultWallpapersFolder, Path.GetFileName(localPath));
                if (File.Exists(targetPath))
                {
                    localPath = targetPath;
                }
                else if (File.Exists(localPath))
                {
                    try
                    {
                        AppPaths.EnsureDirectoryExists(AppPaths.DefaultWallpapersFolder);
                        File.Copy(localPath, targetPath, true);
                        localPath = targetPath;
                    }
                    catch { }
                }
                item.LocalPath = localPath;
            }

            _appState.StatusMessage = "Установка обоев...";
            _appState.IsDownloading = true;
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(StatusMessage));

            try
            {
                if (string.IsNullOrEmpty(item.LocalPath) || !File.Exists(item.LocalPath))
                {
                    if (string.IsNullOrEmpty(item.RemoteUrl))
                    {
                        _notificationService.ShowError("Ошибка", "Файл обоев не найден на ПК и отсутствует ссылка для скачивания.");
                        return;
                    }

                    _appState.StatusMessage = "Скачивание файла обоев...";
                    var tempBingImage = new BingImage
                    {
                        Url = item.RemoteUrl,
                        StartDate = item.Date
                    };
                    string downloadedPath = await _downloadService.DownloadImageAsync(tempBingImage, Settings.DownloadFolder);
                    item.LocalPath = downloadedPath;
                    await _historyService.AddOrUpdateFavoriteAsync(item);
                }

                _appState.LastAppliedPath = item.LocalPath;

                if (!Enum.TryParse<WallpaperStyle>(Settings.WallpaperStyle, true, out var style))
                {
                    style = WallpaperStyle.Fill;
                }

                bool success = await _wallpaperService.SetWallpaperAsync(item.LocalPath, style);
                if (success)
                {
                    Settings.LastAppliedImageId = item.Id;
                    Settings.LastAutoAppliedDate = TodayImages.FirstOrDefault()?.StartDate ?? string.Empty;
                    SaveSettings();

                    // Сохраняем информацию о применении обоев в базу кэша
                    var bingImg = new BingImage
                    {
                        StartDate = item.Date,
                        Url = item.RemoteUrl ?? string.Empty,
                        Title = item.Title,
                        Copyright = item.Copyright,
                        CopyrightLink = item.CopyrightLink,
                        Market = item.Market
                    };
                    await _historyService.AddToCacheAsync(bingImg, "Favorites", isApplied: true);

                    UpdateAppliedStatus(item.Id);
                    OnPropertyChanged(nameof(IsSelectedImageApplied));
                    OnPropertyChanged(nameof(IsSelectedDetailsImageApplied));

                    _notificationService.ShowInfo("Обои изменены", item.Title);
                    await _historyService.CleanOldNonFavoriteImagesAsync(Settings.DownloadFolder, item.LocalPath);
                }
                else
                {
                    _notificationService.ShowError("Ошибка", "Не удалось применить выбранные обои.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка установки обоев из избранного", ex);
                _notificationService.ShowError("Ошибка", "Не удалось установить обои.");
            }
            finally
            {
                _appState.IsDownloading = false;
                _appState.StatusMessage = string.Empty;
                OnPropertyChanged(nameof(IsDownloading));
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        private async Task OnRemoveFavoriteItemAsync(WallpaperHistoryItem item)
        {
            if (item == null) return;

            await _historyService.RemoveFavoriteAsync(item.Id);
            _favoriteIds.Remove(item.Id);

            FavoritesCollection.Remove(item);
            OnPropertyChanged(nameof(HasFavorites));
            OnPropertyChanged(nameof(IsFavoritesEmpty));

            if (SelectedImage != null)
            {
                string selId = GetImageId(SelectedImage);
                if (selId == item.Id)
                {
                    UpdateSelectedImageFavoriteStatus();
                }
            }

            _notificationService.ShowInfo("Избранное", "Изображение удалено из избранного.");
        }

        private void OnOpenFavoriteFolder(WallpaperHistoryItem item)
        {
            if (item == null) return;

            try
            {
                string path = item.LocalPath;
                if (!string.IsNullOrEmpty(path) &&
                    (path.Contains(@"\OneDrive\", StringComparison.OrdinalIgnoreCase) ||
                     path.Contains(@"\Pictures\", StringComparison.OrdinalIgnoreCase) ||
                     path.Contains(@"\Изображения\", StringComparison.OrdinalIgnoreCase)))
                {
                    string redirected = Path.Combine(AppPaths.DefaultWallpapersFolder, Path.GetFileName(path));
                    if (File.Exists(redirected))
                    {
                        path = redirected;
                    }
                }

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    string argument = $"/select,\"{path}\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                else
                {
                    string targetFolder = Directory.Exists(Settings.DownloadFolder) ? Settings.DownloadFolder : AppPaths.DefaultWallpapersFolder;
                    if (Directory.Exists(targetFolder))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", targetFolder);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Не удалось открыть папку избранного: {ex.Message}");
            }
        }

        private async Task OnCheckNowAsync()
        {
            await _schedulerService.StartAutoCheckAsync(isManual: true);
            await LoadImagesAsync();
        }

        private async Task OnForceCheckBingAsync()
        {
            _appState.StatusMessage = "Выполняется проверка и обновление обоев...";
            await _schedulerService.StartAutoCheckAsync(isManual: true, forceReload: true);
            await LoadImagesAsync();
            OnPropertyChanged(nameof(LastCheckStatusText));
            OnPropertyChanged(nameof(ShortLastCheckTime));
            _notificationService.ShowInfo("Обновление обоев", "Проверка завершена. Обои успешно обновлены.");
        }

        private async Task OnForceReloadArchiveAsync()
        {
            if (_isArchiveLoading) return;
            _historicalArchiveImages.Clear();
            await LoadHistoricalArchiveAsync();
            await LoadImagesAsync();
            _notificationService.ShowInfo("Архив GitHub", $"Список обоев успешно обновлен. Доступно {_historicalArchiveImages.Count} изображений из архива.");
        }

        private async Task OnClearCacheAsync()
        {
            await _historyService.ClearCacheAsync();
            await LoadImagesAsync();
            _notificationService.ShowInfo("Очистка кэша", "Все избранные файлы и кэш успешно удалены.");
        }

        private async Task OnClearLogsAsync()
        {
            try
            {
                string logFolder = AppPaths.LogFolder;
                if (Directory.Exists(logFolder))
                {
                    var files = Directory.GetFiles(logFolder, "*.log");
                    int deletedCount = 0;
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch { }
                    }
                    _notificationService.ShowInfo("Очистка логов", $"Удалено файлов логов: {deletedCount}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при очистке логов", ex);
                _notificationService.ShowError("Ошибка", "Не удалось очистить папку логов.");
            }
            await Task.CompletedTask;
        }

        private void OnOpenFolder()
        {
            try
            {
                if (Directory.Exists(Settings.DownloadFolder))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{Settings.DownloadFolder}\"");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка открытия папки обоев", ex);
            }
        }

        private void OnExit()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var app = (App)System.Windows.Application.Current;
                app.ShutdownApplication();
            });
        }

        public async Task CheckForUpdatesAsync()
        {
            if (IsCheckingUpdate) return;

            IsCheckingUpdate = true;
            UpdateStatusText = "Поиск...";
            OnPropertyChanged(nameof(IsCheckingUpdate));

            try
            {
                var result = await _updateService.CheckForUpdatesAsync("l1ratch", "BingWallTray");
                if (result.IsUpdateAvailable)
                {
                    IsUpdateAvailable = true;
                    ReleaseUrl = result.ReleaseUrl;
                    UpdateStatusText = $"Скачать v{result.NewVersion}";
                }
                else
                {
                    IsUpdateAvailable = false;
                    UpdateStatusText = "Обновлений нет";
                }
            }
            catch
            {
                UpdateStatusText = "Ошибка сети";
            }
            finally
            {
                IsCheckingUpdate = false;
                OnPropertyChanged(nameof(IsCheckingUpdate));
            }
        }

        private void OnOpenReleaseUrl()
        {
            if (string.IsNullOrEmpty(ReleaseUrl)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ReleaseUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void OnChooseFolder()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку для сохранения обоев Bing";
                dialog.UseDescriptionForTitle = true;
                dialog.SelectedPath = Settings.DownloadFolder;
                
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    DownloadFolder = dialog.SelectedPath;
                }
            }
        }

        private void OnOpenLogs()
        {
            try
            {
                string logFolder = AppPaths.LogFolder;
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

        private void OnOpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Не удалось открыть ссылку: {url}. Ошибка: {ex.Message}");
            }
        }

        private void OnAddKeywordTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            var current = WallhavenQuery ?? "";
            if (string.IsNullOrEmpty(current))
            {
                WallhavenQuery = tag;
            }
            else
            {
                var tags = current.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    WallhavenQuery = current.TrimEnd() + " " + tag;
                }
            }
        }

        private void OnClearKeywordQuery()
        {
            WallhavenQuery = string.Empty;
        }

        private string GetImageId(BingImage img)
        {
            if (img == null) return string.Empty;
            if (img.Url.Contains("wallhaven.cc", StringComparison.OrdinalIgnoreCase))
            {
                return "Wallhaven_" + Path.GetFileNameWithoutExtension(img.Url);
            }
            return $"{img.StartDate}_{Settings.Market}";
        }

        private void UpdateAppliedStatus(string appliedId)
        {
            if (TodayImages != null)
            {
                foreach (var img in TodayImages) img.IsApplied = (GetImageId(img) == appliedId);
            }
            if (_wallhavenImages != null)
            {
                foreach (var img in _wallhavenImages) img.IsApplied = (GetImageId(img) == appliedId);
            }
            if (_historicalArchiveImages != null)
            {
                foreach (var img in _historicalArchiveImages) img.IsApplied = (GetImageId(img) == appliedId);
            }
            if (DisplayedImages != null)
            {
                foreach (var img in DisplayedImages) img.IsApplied = (GetImageId(img) == appliedId);
            }
        }

        // --- Кэш ---
        private string _cacheCountString = "Загрузка...";
        public string CacheCountString
        {
            get => _cacheCountString;
            set => SetProperty(ref _cacheCountString, value);
        }

        private string _cacheSizeString = "Загрузка...";
        public string CacheSizeString
        {
            get => _cacheSizeString;
            set => SetProperty(ref _cacheSizeString, value);
        }

        public async Task UpdateCacheStatsAsync()
        {
            try
            {
                int totalCount = await _historyService.GetTotalCacheCountAsync();
                int downloadedCount = await _historyService.GetDownloadedCacheCountAsync();
                long totalSize = await _historyService.GetDownloadedCacheSizeAsync();

                double sizeMb = (double)totalSize / (1024 * 1024);

                CacheCountString = $"{totalCount} элементов ({downloadedCount} сохранено на диск)";
                CacheSizeString = $"{sizeMb:F2} МБ";
            }
            catch (Exception ex)
            {
                CacheCountString = "Ошибка";
                CacheSizeString = $"Ошибка ({ex.Message})";
            }
        }

        private void OnOpenPage(string? page)
        {
            if (string.IsNullOrEmpty(page)) return;
            ActivePage = page;
        }

        private void OnSelectSourceAndGallery(string? source)
        {
            if (!string.IsNullOrEmpty(source))
            {
                CurrentSource = source;
            }
            ActivePage = "Gallery";
        }

        private void OnToggleStatusPopup()
        {
            IsStatusPopupOpen = !IsStatusPopupOpen;
        }

        public void RefreshWallhavenSource(bool enabled)
        {
            if (!enabled && CurrentSource == "Wallhaven")
            {
                CurrentSource = "Bing";
            }
            NotifyNavStateChanged();
            _ = LoadImagesAsync(forceReload: true);
        }

        private void OnOpenSettingsWindow()
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var existing = System.Windows.Application.Current.Windows.OfType<BingWallTray.App.Views.SettingsWindow>().FirstOrDefault();
                    if (existing != null)
                    {
                        if (existing.WindowState == System.Windows.WindowState.Minimized)
                        {
                            existing.WindowState = System.Windows.WindowState.Normal;
                        }
                        existing.Topmost = true;
                        existing.Activate();
                        existing.Focus();
                        existing.Topmost = false;
                        return;
                    }

                    BingWallTray.App.Views.SettingsWindow win = null!;
                    var vm = new SettingsViewModel(
                        _settingsService,
                        _historyService,
                        _startupService,
                        _logger,
                        _wingetService,
                        _updateService,
                        _notificationService,
                        () => win?.Close(),
                        RefreshWallhavenSource
                    );

                    win = new BingWallTray.App.Views.SettingsWindow(vm);
                    win.ShowInTaskbar = true;
                    win.Show();
                    win.Activate();
                    win.Focus();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при открытии окна параметров во VM", ex);
            }
        }
    }
}
