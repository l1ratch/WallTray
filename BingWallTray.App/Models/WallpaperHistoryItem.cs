using System;
using System.Text.Json.Serialization;

namespace BingWallTray.App.Models
{
    public class WallpaperHistoryItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("market")]
        public string Market { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("copyright")]
        public string Copyright { get; set; } = string.Empty;

        [JsonPropertyName("copyrightLink")]
        public string CopyrightLink { get; set; } = string.Empty;

        [JsonPropertyName("remoteUrl")]
        public string RemoteUrl { get; set; } = string.Empty;

        [JsonPropertyName("localPath")]
        public string LocalPath { get; set; } = string.Empty;

        [JsonIgnore]
        public string DisplayPath => System.IO.File.Exists(LocalPath) ? LocalPath : RemoteUrl;

        [JsonPropertyName("downloadedAtUtc")]
        public DateTime? DownloadedAtUtc { get; set; }

        [JsonPropertyName("appliedAtUtc")]
        public DateTime? AppliedAtUtc { get; set; }

        [JsonPropertyName("isFavorite")]
        public bool IsFavorite { get; set; } = false;
    }
}
