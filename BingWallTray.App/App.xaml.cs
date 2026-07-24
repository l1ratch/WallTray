using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BingWallTray.App.Models;
using BingWallTray.App.Services;
using BingWallTray.App.Utils;
using BingWallTray.App.ViewModels;
using BingWallTray.App.Views;

namespace BingWallTray.App
{
    public partial class App : System.Windows.Application
    {
        private static System.Threading.Mutex? _appMutex;

        private ILoggingService? _logger;
        private AppState? _appState;
        private IDateTimeProvider? _dateTimeProvider;
        private ISettingsService? _settingsService;
        private IHistoryService? _historyService;
        private IBingService? _bingService;
        private IDownloadService? _downloadService;
        private IWallpaperService? _wallpaperService;
        private IStartupService? _startupService;
        private IGitHubUpdateService? _gitHubUpdateService;
        private INotificationService? _notificationService;
        private ITrayService? _trayService;
        private ISchedulerService? _schedulerService;
        private ISpotlightService? _spotlightService;
        private IWallhavenService? _wallhavenService;

        private MainViewModel? _mainViewModel;
        private MainWindow? _mainWindow;

        protected override async void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "Local\\BingWallTrayMutex_171a8f9";
            _appMutex = new System.Threading.Mutex(true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                _appMutex.Dispose();
                _appMutex = null;
                System.Windows.Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            // 1. Инициализация логирования и базовых утилит
            _logger = new LoggingService();
            _appState = new AppState();
            
            // Настройка режима явного закрытия для трей-приложения
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Настройка глобального перехвата ошибок
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                _logger?.LogError("Критическое необработанное исключение процесса (AppDomain)", ex);
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                _logger?.LogError("Необработанное исключение потока UI Dispatcher", args.Exception);
                
                // Если окно еще не загружено, это фатальная ошибка старта
                if (_mainWindow == null || !_mainWindow.IsLoaded)
                {
                    System.Windows.MessageBox.Show(
                        $"Критическая ошибка при запуске приложения:\n{args.Exception.Message}\n\nДетали записаны в лог.",
                        "BingWallTray - Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    ShutdownApplication();
                }
                else
                {
                    args.Handled = true; // Пытаемся предотвратить аварийное закрытие во время работы
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                _logger?.LogError("Необработанное исключение в фоновой задаче Task", args.Exception);
                args.SetObserved();
            };

            _dateTimeProvider = new LocalDateTimeProvider();
            _logger.LogInfo("=== Запуск BingWallTray ===");

            // 2. Инициализация сервисов
            _settingsService = new SettingsService(_logger, _dateTimeProvider);
            _historyService = new HistoryService(_logger, _dateTimeProvider);
            _bingService = new BingService(_logger);
            _downloadService = new DownloadService(_logger);
            _wallpaperService = new WallpaperService(_logger);
            _startupService = new StartupService(_logger);
            _gitHubUpdateService = new GitHubUpdateService(_logger);
            _notificationService = new NotificationService(_settingsService, _logger);
            _spotlightService = new SpotlightService(_logger);
            _wallhavenService = new WallhavenService(_logger);

            // Перехват уведомления о поврежденном JSON
            string? brokenSettingsFile = null;
            _settingsService.SettingsCorrupted += (s, brokenFile) =>
            {
                brokenSettingsFile = brokenFile;
            };

            // 3. Загрузка настроек
            var settings = await _settingsService.LoadAsync();

            // Создаем папки по умолчанию
            try
            {
                if (!Directory.Exists(settings.DownloadFolder))
                {
                    Directory.CreateDirectory(settings.DownloadFolder);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Критическая ошибка создания папки скачивания {settings.DownloadFolder}", ex);
            }

            // 4. Инициализация планировщика
            _schedulerService = new SchedulerService(
                _logger,
                _settingsService,
                _historyService,
                _bingService,
                _downloadService,
                _wallpaperService,
                _notificationService,
                _dateTimeProvider,
                _appState
            );

            // 5. Инициализация единого MainViewModel
            _mainViewModel = new MainViewModel(
                _settingsService,
                _historyService,
                _downloadService,
                _wallpaperService,
                _schedulerService,
                _startupService,
                _gitHubUpdateService,
                _logger,
                _notificationService,
                _appState,
                _bingService,
                _spotlightService,
                _wallhavenService
            );

            // 6. Инициализация системного трея
            _trayService = new TrayService(_logger, _notificationService);
            _trayService.Initialize();

            // Запускаем периодические проверки по таймеру
            _schedulerService.Start();

            // Проверяем автозапуск в реестре на актуальность пути
            _startupService.IsStartupEnabled();

            // Выводим уведомление о повреждении настроек, если оно случилось
            if (brokenSettingsFile != null)
            {
                _notificationService.ShowWarning(
                    "Файл настроек поврежден",
                    $"Настройки сброшены по умолчанию. Резервная копия сохранена в файл {brokenSettingsFile}"
                );
            }

            // 7. Создаем главное окно (оно скрыто при старте)
            _mainWindow = new MainWindow
            {
                DataContext = _mainViewModel
            };

            // 8. Запускаем фоновую проверку обоев при старте
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500); // Небольшая задержка
                await _schedulerService.StartAutoCheckAsync(isManual: false, isStartup: true);
            });

            // 9. Решаем, нужно ли показать окно при запуске
            bool isMinimizedArg = e.Args.Contains("--minimized") || e.Args.Contains("/minimized");
            
            if (settings.IsFirstRun)
            {
                _logger.LogInfo("Первый запуск приложения. Отображение приветственного окна.");
                
                var welcomeWin = new Views.WelcomeWindow();
                welcomeWin.ShowDialog();
                
                settings.IsFirstRun = false;
                await _settingsService.SaveAsync(settings);
                
                // После первого запуска открываем главное окно, чтобы пользователь сразу его настроил
                ShowMainWindow();
            }
            else if (isMinimizedArg || settings.StartMinimizedToTray)
            {
                _logger.LogInfo("Приложение запущено в трее (тихий режим).");
            }
            else
            {
                _logger.LogInfo("Приложение запущено с открытием окна.");
                ShowMainWindow();
            }
        }

        public void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow
                {
                    DataContext = _mainViewModel
                };
            }

            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            _mainWindow.Activate();
        }

        public void ShowContextMenu()
        {
            var menu = new ContextMenuWindow
            {
                DataContext = _mainViewModel
            };

            // Получаем координаты курсора мыши
            var cursor = System.Windows.Forms.Cursor.Position;
            double x = cursor.X;
            double y = cursor.Y;

            // Конвертируем физические пиксели в DIPs для учета масштаба Windows DPI
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;
            try
            {
                using (var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                {
                    dpiScaleX = graphics.DpiX / 96.0;
                    dpiScaleY = graphics.DpiY / 96.0;
                }
            }
            catch { }

            x /= dpiScaleX;
            y /= dpiScaleY;

            // Ширина меню: 200, Высота: 210 (авторазмер по содержимому)
            double menuWidth = 200;
            double menuHeight = 210;

            // Центрируем меню по горизонтали относительно курсора
            double left = x - menuWidth / 2;
            double top = y - menuHeight;

            // Ограничиваем границами рабочей области
            var workArea = SystemParameters.WorkArea;
            if (left < workArea.Left) left = workArea.Left;
            if (top < workArea.Top) top = workArea.Top;
            if (left + menuWidth > workArea.Right) left = workArea.Right - menuWidth;
            if (top + menuHeight > workArea.Bottom) top = workArea.Bottom - menuHeight;

            menu.Left = left;
            menu.Top = top;
            menu.Show();
            menu.Activate();
        }

        public void OnWallpaperChangedExternally()
        {
            if (_mainViewModel != null)
            {
                _ = _mainViewModel.LoadImagesAsync();
            }
        }

        public void ShutdownApplication()
        {
            _logger?.LogInfo("Инициация завершения приложения...");

            // Останавливаем фоновые проверки
            _schedulerService?.Stop();

            // Удаляем иконку из трея
            _trayService?.Cleanup();

            // Полностью закрываем окно
            _mainWindow?.ForceClose();

            // Завершаем работу процесса
            _logger?.LogInfo("=== Завершение процесса BingWallTray ===");
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _schedulerService?.Stop();
            _trayService?.Cleanup();

            if (_appMutex != null)
            {
                try
                {
                    _appMutex.ReleaseMutex();
                }
                catch { }
                _appMutex.Dispose();
                _appMutex = null;
            }

            base.OnExit(e);
        }
    }
}
