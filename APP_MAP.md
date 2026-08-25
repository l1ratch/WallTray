# WallTray Application & Code Map

This document outlines the directory structure, component layers, unified caching scheme, and dual-window architecture.

## Directory Structure

```
E:\Projects Directory\BingWallTray
├── BingWallTray.App/                    # Main Application Project
│   ├── Assets/                          # Application Icons and Logos
│   │   ├── app.ico
│   │   └── logo.png
│   ├── Models/                          # Core Data Models
│   │   ├── AppSettings.cs               # Persisted user settings
│   │   ├── AppState.cs                  # Runtime app state (status, downloading)
│   │   ├── BingImage.cs                 # Bing Daily image model
│   │   ├── WallpaperCacheItem.cs        # Unified cache entry (Bing/Wallhaven/Favorites)
│   │   └── WallpaperHistoryItem.cs      # Favorites history entry
│   ├── Services/                        # Business Services
│   │   ├── WallpaperCacheService.cs     # Unified cache manager (atomic writes)
│   │   ├── BingService.cs               # Bing Daily API + historical archive
│   │   ├── WallhavenService.cs          # Wallhaven API Client
│   │   ├── HistoryService.cs            # Favorites, cache stats, auto-cleanup
│   │   │                                #   (KeepLastImages/DeleteOldImages logic)
│   │   ├── DownloadService.cs           # Wallpaper file download
│   │   ├── WallpaperService.cs          # Win32 SystemParametersInfo wallpaper set
│   │   ├── SchedulerService.cs          # Auto-change timer/trigger orchestration
│   │   ├── SettingsService.cs           # Settings load/save (JSON)
│   │   ├── StartupService.cs            # Registry Run-key autostart
│   │   ├── LoggingService.cs            # File-based logging
│   │   ├── NotificationService.cs       # Toast/tray notifications
│   │   ├── TrayService.cs               # NotifyIcon lifecycle
│   │   ├── WingetService.cs             # Winget CLI detection & upgrade
│   │   └── GitHubUpdateService.cs       # GitHub Releases update checker
│   ├── ViewModels/                      # MVVM ViewModels
│   │   ├── MainViewModel.cs             # Tray Flyout Coordinator
│   │   ├── SettingsViewModel.cs         # Standalone Settings Coordinator
│   │   ├── ViewModelBase.cs             # INotifyPropertyChanged base
│   │   └── RelayCommand.cs              # ICommand implementation
│   ├── Views/                           # Custom WPF Views and Windows
│   │   ├── SettingsWindow.xaml(.cs)     # Standalone Win11 Fluent Settings Window
│   │   ├── WelcomeWindow.xaml(.cs)      # First-run onboarding window
│   │   └── ContextMenuWindow.xaml(.cs)  # Tray icon right-click menu
│   ├── Utils/                           # Converters & helpers
│   │   ├── InverseBoolConverter.cs
│   │   ├── PathToThumbnailConverter.cs
│   │   ├── DateTimeProvider.cs
│   │   └── FileNameSanitizer.cs
│   ├── App.xaml / App.xaml.cs           # WPF Application entry point, DI wiring
│   ├── MainWindow.xaml                  # Compact Tray Flyout markup
│   │                                    #   ⚠ см. TODO_MAINWINDOW_REDESIGN.md —
│   │                                    #   докер-навигация/статус-виджет из
│   │                                    #   одобренного редизайна утеряны и
│   │                                    #   требуют восстановления по ТЗ
│   └── MainWindow.xaml.cs               # Win32 Tray positioning & event handlers
├── BingWallTray.Tests/                  # Automated Unit Tests (xUnit)
│   ├── BingServiceTests.cs
│   ├── HistoryServiceTests.cs           # Favorites + auto-cleanup logic (11 tests)
│   ├── SettingsServiceTests.cs
│   └── UnitTest1.cs
├── TODO_MAINWINDOW_REDESIGN.md          # ТЗ: восстановление дизайна трей-окна
├── TODO_SETTINGSWINDOW_STATUS.md        # ТЗ: статус окна параметров (готово)
├── LICENSE                              # MIT License
└── publish/ , publish-next/             # Compiled production binaries
```

## Architecture: Dual-Window System

WallTray follows a clean dual-window pattern:

1. **Tray Flyout (`MainWindow.xaml`)**:
   - Ultra-compact, fast, auto-hiding window located near the Windows notification area.
   - Dedicated exclusively to quick wallpaper viewing and application.
   - 4-tab structure (`MainViewModel.SelectedTabIndex`):
     - Index 0: `Gallery` (Bing / Wallhaven feed)
     - Index 1: `About` (App summary & primary `⚙️ Открыть параметры` button)
     - Index 2: `Favorites` (Starred wallpapers)
     - Index 3: `ImageDetails` (Full-screen preview & actions)
   - ⚠ The docked bottom nav bar + hover status widget approved in an
     earlier redesign pass were lost to an accidental `git checkout` and
     are not yet back in the XAML. ViewModel/code-behind logic for them
     is intact. See `TODO_MAINWINDOW_REDESIGN.md` for the restoration spec.

2. **Standalone Settings Window (`SettingsWindow.xaml`)**:
   - Full-featured, independent Windows 11 Fluent window (not owned by
     the tray flyout, so it stays open independently).
   - Flat grouped navigation list (VS Code Settings style), 13 pages
     across 6 uppercase groups:
     - `ОБЩИЕ`: Поведение, Запуск и уведомления
     - `ИСТОЧНИКИ`: Bing, Wallhaven
     - `АВТОСМЕНА`: Расписание
     - `ДАННЫЕ И ЖУРНАЛЫ`: Хранилище, Журналы
     - `ДИАГНОСТИКА`: Сеть и API, Система, Журнал событий
     - `О ПРОГРАММЕ`: Обзор, Обновления, Лицензии
   - Full details in `TODO_SETTINGSWINDOW_STATUS.md`.

## Caching Architecture

WallTray saves metadata in `%APPDATA%\WallTray\wallpapers.json` using the `WallpaperCacheItem` schema. It manages downloads locally, ensures atomicity, and prevents corruption via an automated `.bak` backup mechanism.
