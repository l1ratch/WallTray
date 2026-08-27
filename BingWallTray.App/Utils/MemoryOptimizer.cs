using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BingWallTray.App.Utils
{
    /// <summary>
    /// Утилита оптимизации использования оперативной памяти и тримминга неактивных страниц (Working Set).
    /// </summary>
    public static class MemoryOptimizer
    {
        [DllImport("psapi.dll", EntryPoint = "EmptyWorkingSet")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        /// <summary>
        /// Выполняет сборку мусора для неактивных объектов и сбрасывает рабочий набор страниц процесса.
        /// </summary>
        public static void TrimWorkingSet()
        {
            try
            {
                PathToThumbnailConverter.ClearCache();
                GC.Collect(2, GCCollectionMode.Optimized, false, false);

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    EmptyWorkingSet(Process.GetCurrentProcess().Handle);
                }
            }
            catch
            {
                // Игнорируем в случае системных ограничений безопасности
            }
        }
    }
}
