# WallTray

[![Build Status](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-lightgrey.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**WallTray** (formerly *BingWallTray*) is a lightweight, modern Windows system tray utility designed to automatically download, cache, and rotate desktop wallpapers from multiple premium sources including **Bing Daily** and **Wallhaven**. 

Built using C# and .NET 8 WPF, it features a state-of-the-art **Fluent UI** with acrylic transparencies, smooth animations, and an intuitive layout designed to blend seamlessly with Windows 11.

---

## Key Features

- 🖼️ **Multi-Source Wallpapers**: Automatically fetch fresh high-quality images from:
  - **Bing Daily Wallpaper** (full UHD/4K resolution support)
  - **Wallhaven API** (with custom tags, search queries, aspect ratios, and resolution filters)
- 🔄 **Advanced Auto-Rotation**: Set custom cycles to change wallpapers based on preconfigured time plans or custom intervals.
- ⭐ **Favorites Gallery**: Save your favorite wallpapers to a local database and configure auto-rotation exclusively from your favorites.
- 🎨 **Premium Fluent UI**: Beautiful dark-theme layout with:
  - Glassmorphic panels and thin borders
  - Active-state underline indicator pills
  - Floating bottom navigation dock
  - Real-time instant status badge in the header
- ⚙️ **Rich Diagnostics & Maintenance**:
  - View network status, API responsiveness, and monitor system parameters.
  - Track local cache metrics (total wallpaper items count and disk size in MB).
  - Clear local image cache, GitHub archive logs, or system logs in one click.
  - Integrated real-time Console Log Viewer directly inside the diagnostics page.
- 🚀 **Performance & Portability**: Runs as a single portable self-contained executable file. Zero installation or administrative privileges required. Includes auto-start on Windows boot option.

---

## UI Preview

### Main Wallpapers Gallery & Settings
- **Gallery Grid**: Browse today's wallpapers and historical archives. Double-click to inspect, preview details, or set as background.
- **Header Status Badge**: Shows the last check time (e.g. `✓ 12:34`) and features a matte glass popover displaying network connectivity and backend states.

---

## Installation & Deployment

### Option 1: Portable Version (Recommended)
1. Download the latest `WallTray.exe` from the [Releases](https://github.com/your-username/BingWallTray/releases) page.
2. Place the executable in any folder (e.g., `C:\Users\username\AppData\Local\Programs\WallTray`).
3. Run the application. It will launch directly into the system tray.

### Option 2: Silent Updater & Startup
The app contains an integrated silent updater that checks GitHub for new releases. Enable **"Start with Windows"** in the Settings tab to ensure the tray is always active.

---

## Technical Stack & Architecture

- **Framework**: .NET 8.0 Windows (WPF)
- **Pattern**: Model-View-ViewModel (MVVM)
- **Settings & Caching**: Unified JSON file database with atomic writing, locks handling, and automated `.bak` backups to prevent data corruption.
- **Footprint**: Designed with the **YAGNI** (You Aren't Gonna Need It) principle, relying on the standard library and native platform features for maximum speed and memory efficiency.

---

## Development & Building

### Prerequisites
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (with .NET Desktop Development workload) or [JetBrains Rider](https://www.jetbrains.com/rider/).
- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

### Build from Command Line
To compile the project and publish a release build:

```bash
# Clone the repository
git clone https://github.com/your-username/BingWallTray.git
cd BingWallTray

# Run automated tests
dotnet test

# Publish self-contained single file executable
dotnet publish -c Release -o publish
```

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.