using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BingWallTray.App.Services
{
    public interface IWingetService
    {
        Task<bool> IsWingetAvailableAsync();
        Task<string?> GetInstalledVersionAsync(string packageId = "l1ratch.WallTray");
        Task<bool> UpgradePackageAsync(string packageId = "l1ratch.WallTray");
        Task<bool> UninstallPackageAsync(string packageId = "l1ratch.WallTray");
    }

    public class WingetService : IWingetService
    {
        private readonly ILoggingService _logger;

        public WingetService(ILoggingService logger)
        {
            _logger = logger;
        }

        public async Task<bool> IsWingetAvailableAsync()
        {
            try
            {
                var result = await RunProcessAsync("winget", "--version");
                return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Winget недоступен в системе: {ex.Message}");
                return false;
            }
        }

        public async Task<string?> GetInstalledVersionAsync(string packageId = "l1ratch.WallTray")
        {
            try
            {
                var result = await RunProcessAsync("winget", $"list --id {packageId}");
                if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
                {
                    var match = Regex.Match(result.Output, @"\b\d{4}\.\d+\.\d+(?:-[\w\.]+)?\b");
                    if (match.Success)
                    {
                        return match.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Не удалось получить версию через Winget: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> UpgradePackageAsync(string packageId = "l1ratch.WallTray")
        {
            try
            {
                _logger.LogInfo($"Запуск обновления пакета {packageId} через Winget...");
                var result = await RunProcessAsync("winget", $"upgrade --id {packageId} --accept-source-agreements --accept-package-agreements --silent");
                return result.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка обновления пакета через Winget: {packageId}", ex);
                return false;
            }
        }

        public async Task<bool> UninstallPackageAsync(string packageId = "l1ratch.WallTray")
        {
            try
            {
                _logger.LogInfo($"Запуск удаления пакета {packageId} через Winget...");
                var result = await RunProcessAsync("winget", $"uninstall --id {packageId} --silent");
                return result.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка удаления пакета через Winget: {packageId}", ex);
                return false;
            }
        }

        private static async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, string arguments)
        {
            return await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(15000);
                return (process.ExitCode, output);
            });
        }
    }
}
