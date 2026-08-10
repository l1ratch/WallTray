# WallTray Changelog & Goals Tracker

This file tracks completed features, active goals, deferred backlog, and rejected ideas.

## Project Vision
To keep WallTray a lightweight, modern, robust background utility that works out-of-the-box, requires no configuration, and looks stunning.

---

## Active Status Board

- `[x]` **Unified Cache Database**: Implemented `WallpaperCacheService` with atomic backups.
- `[x]` **Fluent Design Redesign**: Overhauled UI to Windows 11 style, with translucent borders, accent-color underlines, and a header badge.
- `[x]` **Fixed Navigation Pages**: Bound ListBox to `SelectedNavIndex` to decouple TabControl order and fix mismatched button pages.
- `[x]` **Compact Floating Navigation**: Removed large bottom bar paddings to sit tightly at the bottom edge.
- `[x]` **Instant Header Status Widget**: Replaced the bottom status footer with a top-right badge showing last check time, featuring an instant matte glass popup.
- `[x]` **CalVer Versioning**: Transitioned to the calendar-based standard `2026.8.0` with dynamic informational version support.
- `[x]` **Workspace Clean up**: Deleted obsolete specification files and `.pdf` instructions, updated `.gitignore`.
- `[ ]` **Weekly Bing Multi-Wallpaper Download Plan**: Optimize background update cycles to download 7 wallpapers at once (under discussion).

---

## History of Revisions

### Version 2026.8.0 (Current)
- Introduced **Dual-Window Architecture**: Decoupled quick tray flyout (`MainWindow`) from full-featured standalone settings window (`SettingsWindow`).
- Implemented **Windows 11 Fluent UI Overhaul**: Added animated `ToggleSwitch` controls, rounded container frame clipping (`ClipToBounds="True"`), and refined dark cards.
- Integrated **Winget Package Management (`WingetService.cs`)**: Native detection and one-click upgrades for package `l1ratch.WallTray`.
- Fixed navigation accent pill glitches and added global `BoolToVisibility` converter registration.
- Added comprehensive documentation: `APP_MAP.md` update and new `CONCEPT_AND_STATUS.md`.

### Version 1.0.0 (Legacy)
- Initial release with Bing Daily, Spotlight, and Wallhaven support.
- Basic MVVM layout, settings, and automatic wallpaper change interval.
