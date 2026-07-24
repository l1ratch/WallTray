using System;
using System.Collections.Generic;
using BingWallTray.App.Models;

namespace BingWallTray.App.Models
{
    public class AppState
    {
        public bool IsChecking { get; set; } = false;
        public bool IsDownloading { get; set; } = false;
        public string StatusMessage { get; set; } = string.Empty;
        public IReadOnlyList<BingImage> TodayImages { get; set; } = Array.Empty<BingImage>();
        public string LastAppliedPath { get; set; } = string.Empty;
    }
}
