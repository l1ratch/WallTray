using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BingWallTray.App.Utils
{
    // ponytail: decodes favorite thumbnails at a small fixed size instead of full resolution,
    // cutting per-item memory/CPU cost when the favorites list is realized.
    public class PathToThumbnailConverter : IValueConverter
    {
        private const int DecodePixelWidth = 220;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource> _cache = new();

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? path = value as string;
            if (string.IsNullOrWhiteSpace(path)) return null;

            if (path.Contains(@"\OneDrive\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains(@"\Pictures\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains(@"\Изображения\", StringComparison.OrdinalIgnoreCase))
            {
                string localRedirect = System.IO.Path.Combine(AppPaths.DefaultWallpapersFolder, System.IO.Path.GetFileName(path));
                if (System.IO.File.Exists(localRedirect))
                {
                    path = localRedirect;
                }
            }

            if (_cache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
                {
                    bitmap.UriSource = uri;
                }
                else
                {
                    bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                }
                bitmap.DecodePixelWidth = DecodePixelWidth;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.EndInit();
                bitmap.Freeze();

                _cache[path] = bitmap;
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
