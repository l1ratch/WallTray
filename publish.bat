@echo off
chcp 65001 > nul
echo Сборка самодостаточного exe-файла для Windows x64...
dotnet publish BingWallTray.App\BingWallTray.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
if %ERRORLEVEL% equ 0 (
    echo.
    echo Сборка успешно завершена!
    echo Файл находится в папке: publish\BingWallTray.App.exe
) else (
    echo.
    echo Ошибка сборки! Проверьте логи выше.
)
pause
