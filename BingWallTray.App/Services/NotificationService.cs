using System;
using System.Windows.Forms; // Требуется для ToolTipIcon

namespace BingWallTray.App.Services
{
    public class NotificationRequestedEventArgs : EventArgs
    {
        public string Title { get; }
        public string Message { get; }
        public ToolTipIcon Icon { get; }

        public NotificationRequestedEventArgs(string title, string message, ToolTipIcon icon)
        {
            Title = title;
            Message = message;
            Icon = icon;
        }
    }

    public interface INotificationService
    {
        event EventHandler<NotificationRequestedEventArgs>? NotificationRequested;
        void ShowInfo(string title, string message);
        void ShowWarning(string title, string message);
        void ShowError(string title, string message);
    }

    public class NotificationService : INotificationService
    {
        private readonly ISettingsService _settingsService;
        private readonly ILoggingService _logger;

        public event EventHandler<NotificationRequestedEventArgs>? NotificationRequested;

        public NotificationService(ISettingsService settingsService, ILoggingService logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        private void TriggerNotification(string title, string message, ToolTipIcon icon)
        {
            _logger.LogInfo($"[Уведомление] {title} - {message}");

            if (_settingsService.CurrentSettings.ShowNotifications)
            {
                NotificationRequested?.Invoke(this, new NotificationRequestedEventArgs(title, message, icon));
            }
        }

        public void ShowInfo(string title, string message)
        {
            TriggerNotification(title, message, ToolTipIcon.Info);
        }

        public void ShowWarning(string title, string message)
        {
            TriggerNotification(title, message, ToolTipIcon.Warning);
        }

        public void ShowError(string title, string message)
        {
            TriggerNotification(title, message, ToolTipIcon.Error);
        }
    }
}
