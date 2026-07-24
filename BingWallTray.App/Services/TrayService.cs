using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Threading;
using BingWallTray.App.Models;
using BingWallTray.App.Utils;

namespace BingWallTray.App.Services
{
    public interface ITrayService
    {
        void Initialize();
        void Cleanup();
    }

    public class TrayService : ITrayService
    {
        private readonly ILoggingService _logger;
        private readonly INotificationService _notificationService;
        private NotifyIcon? _notifyIcon;

        public TrayService(
            ILoggingService logger,
            INotificationService notificationService)
        {
            _logger = logger;
            _notificationService = notificationService;
        }

        public void Initialize()
        {
            _logger.LogInfo("Инициализация системного трея...");

            // Загружаем иконку приложения из ресурсов сборки
            Icon? trayIcon = null;
            try
            {
                var iconUri = new Uri("pack://application:,,,/Assets/app.ico");
                var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
                if (streamInfo != null)
                {
                    using (var stream = streamInfo.Stream)
                    {
                        trayIcon = new Icon(stream, 16, 16);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Не удалось загрузить иконку из ресурсов сборки: {ex.Message}");
            }

            // Если не удалось загрузить из ресурсов, пробуем файл на диске
            if (trayIcon == null)
            {
                try
                {
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    string iconPath = Path.Combine(exeDir, "Assets", "app.ico");
                    if (File.Exists(iconPath))
                    {
                        trayIcon = new Icon(iconPath, 16, 16);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Не удалось загрузить иконку с диска: {ex.Message}");
                }
            }

            // Если все еще null, используем системную дефолтную иконку
            if (trayIcon == null)
            {
                trayIcon = SystemIcons.Application;
            }

            // Инициализируем NotifyIcon
            _notifyIcon = new NotifyIcon
            {
                Icon = trayIcon,
                Text = "BingWallTray",
                Visible = true
            };

            // Подписка на клики мыши по иконке
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

            // Подписка на сервис уведомлений
            _notificationService.NotificationRequested += NotificationService_NotificationRequested;

            _logger.LogInfo("Системный трей успешно инициализирован.");
        }

        private void NotificationService_NotificationRequested(object? sender, NotificationRequestedEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(3000, e.Title, e.Message, e.Icon);
            }
        }

        private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var app = (App)System.Windows.Application.Current;
                    app.ShowMainWindow();
                }));
            }
            else if (e.Button == MouseButtons.Right)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var app = (App)System.Windows.Application.Current;
                    app.ShowContextMenu();
                }));
            }
        }

        private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var app = (App)System.Windows.Application.Current;
                app.ShowMainWindow();
            }));
        }

        public void Cleanup()
        {
            _logger.LogInfo("Очистка ресурсов трея...");
            try
            {
                _notificationService.NotificationRequested -= NotificationService_NotificationRequested;
            }
            catch { }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
