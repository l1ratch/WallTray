using System;
using System.IO;
using System.Text;

namespace BingWallTray.App.Services
{
    public interface ILoggingService
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? ex = null);
        void LogDebug(string message);
        string LogFolder { get; }
        bool LoggingEnabled { get; set; }
        string LogLevel { get; set; }
    }

    public class LoggingService : ILoggingService
    {
        private readonly object _lock = new object();
        public string LogFolder { get; }
        public bool LoggingEnabled { get; set; } = true;
        public string LogLevel { get; set; } = "Info";

        public LoggingService()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                LogFolder = Path.Combine(appData, "WallTray", "Logs");
                if (!Directory.Exists(LogFolder))
                {
                    Directory.CreateDirectory(LogFolder);
                }
            }
            catch
            {
                LogFolder = string.Empty;
            }
        }

        private bool ShouldSkipLog(string level)
        {
            int GetWeight(string l) => l.ToLower() switch
            {
                "debug" => 1,
                "info" => 2,
                "warning" => 3,
                "error" => 4,
                _ => 2
            };

            return GetWeight(level) < GetWeight(LogLevel);
        }

        private void WriteLog(string level, string message, Exception? ex = null)
        {
            if (!LoggingEnabled) return;
            if (string.IsNullOrEmpty(LogFolder)) return;
            if (ShouldSkipLog(level)) return;

            try
            {
                string fileName = $"app-{DateTime.Today:yyyyMMdd}.log";
                string fullPath = Path.Combine(LogFolder, fileName);

                var sb = new StringBuilder();
                sb.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ");
                sb.Append($"[{level}] ");
                sb.Append(message);

                if (ex != null)
                {
                    Exception? currentEx = ex;
                    int depth = 0;
                    while (currentEx != null && depth < 5)
                    {
                        sb.AppendLine();
                        sb.Append(depth == 0 ? $"[Exception] " : $"[InnerException {depth}] ");
                        sb.Append($"{currentEx.GetType().Name}: {currentEx.Message}");
                        sb.AppendLine();
                        sb.Append(currentEx.StackTrace);
                        currentEx = currentEx.InnerException;
                        depth++;
                    }
                }

                lock (_lock)
                {
                    File.AppendAllText(fullPath, sb.ToString() + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Подавляем ошибки логирования во избежание падения приложения
            }
        }

        public void LogInfo(string message) => WriteLog("Info", message);
        public void LogWarning(string message) => WriteLog("Warning", message);
        public void LogError(string message, Exception? ex = null) => WriteLog("Error", message, ex);
        public void LogDebug(string message) => WriteLog("Debug", message);
    }
}
