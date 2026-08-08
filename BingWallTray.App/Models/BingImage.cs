using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace BingWallTray.App.Models
{
    public class BingImage : INotifyPropertyChanged
    {
        [JsonPropertyName("startdate")]
        public string StartDate { get; set; } = string.Empty;

        [JsonPropertyName("enddate")]
        public string EndDate { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("urlbase")]
        public string UrlBase { get; set; } = string.Empty;

        [JsonPropertyName("copyright")]
        public string Copyright { get; set; } = string.Empty;

        [JsonPropertyName("copyrightlink")]
        public string CopyrightLink { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("quiz")]
        public string Quiz { get; set; } = string.Empty;

        // Внутреннее свойство для сохранения региона запроса
        [JsonIgnore]
        public string Market { get; set; } = string.Empty;

        // Источник изображения (Bing, Wallhaven)
        [JsonIgnore]
        public string Source { get; set; } = "Bing";

        private bool _isApplied;

        [JsonIgnore]
        public bool IsApplied
        {
            get => _isApplied;
            set
            {
                if (_isApplied != value)
                {
                    _isApplied = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isFavorite;

        [JsonIgnore]
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                }
            }
        }

        private string? _thumbnailUrl;
        [JsonIgnore]
        public string ThumbnailUrl
        {
            get
            {
                if (_thumbnailUrl != null) return _thumbnailUrl;
                if (string.IsNullOrEmpty(Url)) return string.Empty;
                if (!Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return Url;
                return Url.Contains("?") ? (Url + "&w=240&h=135&c=7") : (Url + "?w=240&h=135&c=7");
            }
            set => _thumbnailUrl = value;
        }

        private string? _previewUrl;
        [JsonIgnore]
        public string PreviewUrl
        {
            get
            {
                if (_previewUrl != null) return _previewUrl;
                if (string.IsNullOrEmpty(Url)) return string.Empty;
                if (!Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return Url;
                return Url.Contains("?") ? (Url + "&w=800&h=450") : (Url + "?w=800&h=450");
            }
            set => _previewUrl = value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BingResponse
    {
        [JsonPropertyName("images")]
        public BingImage[] Images { get; set; } = Array.Empty<BingImage>();
    }
}
