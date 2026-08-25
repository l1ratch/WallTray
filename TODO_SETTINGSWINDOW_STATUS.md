# ТЗ: Окно параметров (SettingsWindow.xaml) — статус и оставшиеся задачи

## Статус: РЕАЛИЗОВАНО и работает

В отличие от MainWindow.xaml, этот редизайн НЕ пострадал от инцидента с
git checkout — файл `SettingsWindow.xaml` не существовал в старом коммите
(создан в этой сессии), поэтому checkout его не затронул. Всё нижеописанное
уже находится в коде и подтверждено сборкой (`dotnet build` — 0 ошибок,
`dotnet test` — 11/11).

Этот файл документирует финальную реализацию для справки + фиксирует
3 небольших расхождения с изначальным планом, которые можно доделать
опционально.

## Архитектура (реализовано)

Отдельное самостоятельное окно (`Views/SettingsWindow.xaml` +
`SettingsWindow.xaml.cs`), не привязанное к `Owner=MainWindow` (чтобы не
скрывалось вместе с треем). Открывается через
`MainViewModel.OpenSettingsWindowCommand` /
`MainViewModel.GoToSettingsCommand`.

Размер: `Width="1100" Height="760" MinWidth="940" MinHeight="620"`.

## Навигация — плоский группированный список (реализовано)

Стиль в духе VS Code Settings sidebar: uppercase-заголовки групп (не
кликабельны, стиль `GroupHeader`) + кликабельные пункты (стиль `NavItem`,
активный пункт подсвечен через `Tag`-биндинг на `IsPageXxx`-свойство).
Без шевронов/аккордеона — весь список сразу виден, скроллится единым
блоком.

Финальная структура (13 страниц, CommandParameter = индекс страницы):

```
ОБЩИЕ
  0  Поведение              — WallpaperStyle, CheckIntervalHours
  1  Запуск и уведомления   — IsStartupEnabled, StartMinimizedToTray, ShowNotifications
ИСТОЧНИКИ
  2  Bing                   — Market, UseUhd, EnableHistoricalArchive, AutoCheckBingEnabled
  3  Wallhaven              — EnableWallhaven, WallhavenQuery, категории, разрешения
АВТОСМЕНА
  4  Расписание             — AutoChangeEnabled, AutoChangeSource, AutoChangeTrigger, интервал
ДАННЫЕ И ЖУРНАЛЫ
  5  Хранилище              — DownloadFolder, размер кэша+очистка, KeepLastImages/DeleteOldImages
  6  Журналы                — LoggingEnabled, LogLevel, открыть/очистить логи
ДИАГНОСТИКА
  7  Сеть и API             — NetworkStatus (ping), BingApiStatus (live HTTP-проверка)
  8  Система                — DisplayResolution, OSVersion, размер кэша
  9  Журнал событий         — последние 30 строк текущего лог-файла (DiagnosticsLogText)
О ПРОГРАММЕ
  10 Обзор                  — лого, версия, описание, copyright, автор
  11 Обновления             — Winget + GitHub Releases check/download
  12 Лицензии               — MIT WallTray + .NET Runtime + источники данных (disclaimer)
```

## Лицензии (реализовано + доп. пункт)

- `LICENSE` в корне репозитория — MIT, © 2026 l1ratch
- Страница "Лицензии" содержит 3 карточки:
  1. Лицензия WallTray (полный MIT-текст)
  2. Платформа: .NET 8 / WPF Runtime (Microsoft), ссылка на
     `github.com/dotnet/runtime` — добавлено дополнительно сверх
     изначального минимального плана
  3. Источники данных: Bing (bing.com), Wallhaven (wallhaven.cc) —
     disclaimer "права на изображения принадлежат правообладателям"
- Зависимостей через NuGet в проекте нет (пустой `<ItemGroup>` в
  `.csproj`), поэтому больше никого лицензировать не нужно

## Удаление Spotlight / EnableExtraSources (реализовано полностью)

Подтверждено grep — 0 упоминаний "Spotlight" во всём коде проекта
(`BingWallTray.App/`), включая комментарии в `BingImage.cs`,
`WallpaperCacheItem.cs`, маркетинговый текст в `WelcomeWindow.xaml`.

## Удаление dead code из MainWindow (реализовано)

- Встроенная вкладка "Настройки" и вкладка "Диагностика" (дублировавшие
  новое SettingsWindow) удалены из `MainWindow.xaml`
- Диагностические свойства/команды портированы в `SettingsViewModel.cs`:
  `NetworkStatus`, `BingApiStatus`, `DisplayResolution`, `OSVersion`,
  `DiagnosticsLogText`, `RunDiagnosticsCommand`, `IsRunningDiagnostics`
- Текущая структура табов MainWindow: 0=Gallery / 1=About / 2=Favorites /
  3=ImageDetails (согласована с `MainViewModel.SelectedTabIndex`)

## KeepLastImages / DeleteOldImages — реальная логика (реализовано)

`HistoryService.CleanOldNonFavoriteImagesAsync`:
- Если `DeleteOldImages == false` — выходит немедленно, ничего не трогает
- Если `true` — оставляет `KeepLastImages` самых свежих (по
  `LastWriteTime`) не-избранных файлов, остальные удаляет
- Избранные файлы и текущий применённый wallpaper всегда защищены от
  удаления, независимо от настроек
- Тесты: `DoesNotDeleteWhenDisabled`, `RespectsKeepLastImages`,
  обновлённый `DeletesOnlyNonFavoritesAndNotCurrent` — все проходят

## Расхождения с изначальным планом (не критично, на выбор — доделать или оставить)

1. **Отдельный файл стилей**: план предполагал `Styles/SettingsStyles.xaml`
   как общий `ResourceDictionary`. По факту стили (`SettingsCard`,
   `ToggleSwitch`, `PrimaryButton`, `SecondaryButton`, `LinkButton`,
   `PageTitle`, `PageSubtitle`, `NavItem`, `GroupHeader`) определены
   локально в `Window.Resources` самого `SettingsWindow.xaml`.
   Функционально это работает одинаково, разница чисто структурная.
   Стоит выносить только если стили понадобятся повторно в другом окне.

2. **Размер окна**: план — `MinWidth=900 MinHeight=600`, по факту
   `MinWidth="940" MinHeight="620"`. Несущественное расхождение,
   финальное значение было выбрано как более комфортное для длинного
   списка навигации с 6 группами.

3. **Нумерация индекса страниц у навигации**: реализована по порядку
   0-12 подряд без пропусков (см. таблицу выше) — план не фиксировал
   точную нумерацию, поэтому расхождений по существу нет.

## Что НЕ нужно делать дальше (уже закрыто)

- ❌ Не нужно заново создавать LICENSE — существует
- ❌ Не нужно заново удалять Spotlight — подтверждено 0 совпадений
- ❌ Не нужно заново реализовывать KeepLastImages/DeleteOldImages —
  логика в HistoryService и покрыта тестами
- ❌ Не нужно трогать структуру 13 страниц навигации — финализирована

## Единственное реальное действие, которое можно сделать (опционально)

Если хочется точного соответствия изначальному плану — вынести стили
из `Window.Resources` в `Styles/SettingsStyles.xaml` и подключить через
`<ResourceDictionary Source="Styles/SettingsStyles.xaml"/>` в
`App.xaml` или `MergedDictionaries` окна. Это чисто рефакторинг без
изменения поведения — приоритет низкий.
</content>
