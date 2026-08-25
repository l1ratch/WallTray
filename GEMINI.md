# AI Development Guidelines for WallTray

Welcome, agent! This document contains the guidelines, rules, and best practices for developing WallTray. Please follow these rules strictly to maintain codebase quality.

## Core Rules

1. **WPF & MVVM Architecture**:
   - Strictly follow the Model-View-ViewModel (MVVM) pattern.
   - Do not write business or layout logic in WPF code-behind (`MainWindow.xaml.cs`) unless it is strictly UI-only window management.
   - Bind View elements to properties in `MainViewModel.cs`.

2. **CalVer (Calendar Versioning)**:
   - This project uses Calendar Versioning (CalVer) standard in the format `YYYY.M.Patch` (e.g. `2026.8.0`).
   - Suffixes like `-dev` or `-preview.1` can be added for prerelease/test builds.
   - Version properties must be declared in [BingWallTray.App.csproj](file:///E:/Projects%20Directory/BingWallTray/BingWallTray.App/BingWallTray.App.csproj) and read dynamically using `AssemblyInformationalVersionAttribute` to preserve tags.

3. **Ponytail Principles (Simplicity & Minimalism)**:
   - Apply the Ponytail methodology at intensity: full.
   - Question whether a new feature or dependency is actually needed (YAGNI).
   - Use the standard library and native Windows APIs before introducing third-party libraries.
   - Delete dead/commented-out code instead of keeping it commented.
   - Prefer writing a single clean method over complex abstract factory configurations.

4. **Data Cache & Integrity**:
   - Always read and write wallpapers metadata using the unified `WallpaperCacheService`.
   - Never write secondary caching systems or direct file writes to user data files without locking and automatic backup (`.bak`) protection.

---

## Workspace Navigation Map

- Rules/Guidelines: [`GEMINI.md`](file:///E:/Projects%20Directory/BingWallTray/GEMINI.md)
- Codebase Map: [`APP_MAP.md`](file:///E:/Projects%20Directory/BingWallTray/APP_MAP.md)
- Project History & Changelog: [`CHANGELOG.md`](file:///E:/Projects%20Directory/BingWallTray/CHANGELOG.md)
