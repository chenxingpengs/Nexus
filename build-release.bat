@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Nexus Release Build Script
echo ========================================
echo.

set VERSION=1.0.0
set OUTPUT_DIR=Output
set PUBLISH_DIR=bin\Release\net8.0-windows\win-x64\publish

echo [1/4] Cleaning old files...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"

echo [2/4] Restoring dependencies...
dotnet restore
if %errorlevel% neq 0 (
    echo Restore failed!
    pause
    exit /b 1
)

echo [3/4] Publishing Release version...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b 1
)

echo [4/4] Creating output directory...
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Copying published files...
xcopy /E /I /Y "%PUBLISH_DIR%\*" "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64\"

echo Creating ZIP archive...
powershell -Command "Compress-Archive -Path '%OUTPUT_DIR%\Nexus-%VERSION%-win-x64' -DestinationPath '%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.zip' -Force"

echo.
echo ========================================
echo Build completed!
echo Output: %OUTPUT_DIR%\Nexus-%VERSION%-win-x64.zip
echo ========================================
echo.
echo Server URL: https://api.hqzx.me (Release mode)
echo.
pause
