# WallTray

<div align="center">
  <img src="BingWallTray.App/Assets/logo.png" width="128" height="128" alt="WallTray Logo" />
  <h3>Автоматическая установка и каталогизация обоев для Windows</h3>
  <p>Быстрая, минималистичная и элегантная утилита в системном трее Windows 10/11 на базе .NET 8 и WPF</p>

  <p>
    <a href="https://github.com/l1ratch/WallTray/releases"><img src="https://img.shields.io/github/v/release/l1ratch/WallTray?label=Release&color=0078d4" alt="Release" /></a>
    <a href="https://dotnet.microsoft.com/download/dotnet/8.0"><img src="https://img.shields.io/badge/.NET-8.0%20WPF-512bd4.svg" alt=".NET 8" /></a>
    <a href="https://velopack.io"><img src="https://img.shields.io/badge/Updates-Velopack-blueviolet" alt="Velopack" /></a>
    <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-2ecc71.svg" alt="License" /></a>
  </p>
</div>

---

## 🌟 Ключевые возможности

- ☀️ **Bing Image of the Day**:
  - Ежедневные официальные обои Microsoft в максимальном разрешении **4K UHD (3840×2160)**.
  - Поддержка выбора региона контента (`ru-RU`, `en-US`, `de-DE`, `fr-FR`, `ja-JP`, `zh-CN`).
  - Просмотр исторического архива обоев за предыдущие дни и недели.
- 🌊 **Каталог Wallhaven.cc**:
  - Полноценная интеграция открытого каталога Wallhaven.
  - Наглядные фильтры категорий (*General*, *Anime*, *People*).
  - Быстрый поиск по тегам (*nature*, *space*, *cyberpunk*, *landscape*, *minimalism*, *cars*, *fantasy*).
  - Фильтрация по разрешениям экрана (*Full HD*, *2K QHD*, *4K UHD*, *UltraWide 21:9*).
- 🖥️ **Поддержка любых мультимониторных конфигураций**:
  - Автоматическое определение реального физического разрешения, частоты обновления (Гц) и соотношения сторон каждого подключенного экрана.
  - Функция автоподбора обоев под мониторы в один клик.
- ⏱️ **Гибкая автосмена обоев**:
  - Смена по таймеру (15м, 30м, 1ч, 2ч, 6ч, 12ч, 24ч или произвольный интервал).
  - Смена при запуске системы или комбинация интервала и старта.
  - Источники для автосмены: *Сегодняшние Bing*, *Случайные Bing* или *Избранное*.
- ⭐ **Быстрое избранное и надежный кэш**:
  - Добавление и удаление обоев в избранное в один клик.
  - Хранение кэша изолировано в `%LocalAppData%\WallTray`, что исключает конфликты и зависания при синхронизации с облаком (OneDrive).
  - Автоматическая очистка старых обоев с гарантированным сохранением избранных.
- 🚀 **Бесшовные автообновления (Velopack)**:
  - Автоматическая проверка новых релизов с поддержкой дельта-обновлений (минимум трафика).
  - Фоновое скачивание с индикатором прогресса и мгновенный перезапуск с применением патча.
  - Возможность участия в канале предварительных сборок (*Preview/Beta*).
- 🎨 **Современный интерфейс Windows 11 Fluent**:
  - Тёмная матовая тема с акриловыми эффектами и плавными анимациями.
  - Сверхтонкие плавающие скроллбары (5px в стиле iOS / macOS).
  - Чёткая векторная графика (SVG) без размытия при любом масштабе экрана (DPI).

---

## 📥 Установка и запуск

### Способ 1: Автоматический установщик Velopack (Рекомендуется)
1. Скачайте `WallTray-Setup.exe` со страницы [Релизов](https://github.com/l1ratch/WallTray/releases).
2. Запустите установщик. Приложение автоматически установится, создаст ярлыки и запустится в системном трее.
3. Все последующие обновления будут доставляться и применяться автоматически.

### Способ 2: Портативная версия (Standalone)
1. Скачайте архив `WallTray-win-x64.zip` или файл `WallTray.exe`.
2. Распакуйте в любую удобную папку и запустите. Программа не требует прав администратора.

---

## 🛠️ Сборка из исходного кода

### Требования:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Velopack CLI](https://velopack.io) (устанавливается командой `dotnet tool install -g vpk`)
- Windows 10 (1809+) или Windows 11 (x64)

### Сборка и тестирование:

```powershell
# 1. Клонирование репозитория
git clone https://github.com/l1ratch/WallTray.git
cd WallTray

# 2. Запуск автоматических тестов
dotnet test

# 3. Публикация приложения
dotnet publish BingWallTray.App/BingWallTray.App.csproj -c Release -r win-x64 --no-self-contained -o ./publish/win-x64

# 4. Создание Velopack пакета и инсталлятора (vpk)
vpk pack --packId WallTray --packVersion 26.8.0 --packDir ./publish/win-x64 --mainExe WallTray.exe --icon BingWallTray.App/Assets/app.ico --outputDir ./Releases
```

---

## 🏗️ Архитектура и стек технологий

- **Платформа**: .NET 8.0 (Windows Desktop SDK).
- **UI Framework**: Windows Presentation Foundation (WPF) с кастомной темой Fluent Dark.
- **Архитектура**: Model-View-ViewModel (MVVM) с разделением ответственности сервисов.
- **Хранение данных**: Атомарный `WallpaperCacheService` с защитой от повреждения файлов (`.bak` резервные копии и файловые блокировки).
- **Схема версионирования**: Календарное версионирование (**CalVer**) в формате `YY.M.Patch` (например: `26.8.0`).

---

## 📜 Лицензия и правовая информация

- **Код приложения**: Распространяется свободно под лицензией [MIT License](LICENSE).
- **Bing Image of the Day**: Фоновые изображения защищены авторским правом корпорации Microsoft и соответствующих фотографов. Изображения предоставляются исключительно для личного некоммерческого использования в качестве обоев рабочего стола.
- **Wallhaven.cc**: Изображения принадлежат их авторам и сообществу Wallhaven; используются через открытый публичный API сервиса.
- **Пиктограммы**: Векторные иконки [Material Design Icons](https://pictogrammers.com/library/mdi/) (Apache 2.0).

---

<div align="center">
  <sub>Разработано с заботой о производительности и эстетике рабочего стола • © 2026 l1ratch</sub>
</div>