using System;
using System.Text.Json.Serialization;

namespace BingWallTray.App.Models
{
    public class WallpaperCacheItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("copyright")]
        public string Copyright { get; set; } = string.Empty;

        [JsonPropertyName("copyrightlink")]
        public string CopyrightLink { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("urlbase")]
        public string UrlBase { get; set; } = string.Empty;

        [JsonPropertyName("localpath")]
        public string LocalPath { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty; // Bing, Wallhaven, Favorites

        [JsonPropertyName("startdate")]
        public string StartDate { get; set; } = string.Empty;

        [JsonPropertyName("market")]
        public string Market { get; set; } = string.Empty;

        [JsonPropertyName("downloaddate")]
        public DateTime? DownloadDate { get; set; }

        [JsonPropertyName("lastapplieddate")]
        public DateTime? LastAppliedDate { get; set; }

        [JsonPropertyName("applycount")]
        public int ApplyCount { get; set; }

        [JsonPropertyName("filesize")]
        public long FileSize { get; set; }

        [JsonPropertyName("resolution")]
        public string Resolution { get; set; } = string.Empty;

        [JsonPropertyName("isfavorite")]
        public bool IsFavorite { get; set; }
    }
}
