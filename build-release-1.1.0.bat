@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Nexus Release Build and Upload Script
echo Version: 1.1.0
echo ========================================
echo.

set VERSION=1.1.0
set OUTPUT_DIR=Output
set PUBLISH_DIR=bin\Release\net8.0-windows\win-x64\publish
set ZIP_FILE=Nexus-%VERSION%-win-x64.zip

echo [1/6] Cleaning old files...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64" rmdir /s /q "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64"
if exist "%OUTPUT_DIR%\%ZIP_FILE%" del /f /q "%OUTPUT_DIR%\%ZIP_FILE%"

echo [2/6] Restoring dependencies...
dotnet restore
if %errorlevel% neq 0 (
    echo Restore failed!
    pause
    exit /b 1
)

echo [3/6] Publishing Release version...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b 1
)

echo [4/6] Creating output directory and ZIP archive...
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Copying published files...
xcopy /E /I /Y "%PUBLISH_DIR%\*" "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64\"

echo Creating ZIP archive...
powershell -Command "Compress-Archive -Path '%OUTPUT_DIR%\Nexus-%VERSION%-win-x64' -DestinationPath '%OUTPUT_DIR%\%ZIP_FILE%' -Force"

echo [5/6] Git commit and push...
cd /d "%~dp0"

git add -A
git status

echo.
echo Committing changes...
git commit -m "release: v%VERSION%"
if %errorlevel% neq 0 (
    echo No changes to commit or commit failed.
)

echo Pushing to origin...
git push origin main
if %errorlevel% neq 0 (
    echo Push failed! Please check your network or credentials.
    pause
    exit /b 1
)

echo [6/6] Creating GitHub Release...
gh release create v%VERSION% "%OUTPUT_DIR%\%ZIP_FILE%" --title "Nexus v%VERSION%" --notes "## Nexus v%VERSION% Release

### Changes
- Updated to version %VERSION%

### Download
Download the ZIP file below and extract it to run Nexus.

### System Requirements
- Windows x64
- .NET 8.0 Runtime (self-contained, no need to install separately)
"

if %errorlevel% neq 0 (
    echo GitHub Release creation failed! Please check if the release already exists.
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build and Upload completed successfully!
echo ========================================
echo.
echo Version: %VERSION%
echo Output: %OUTPUT_DIR%\%ZIP_FILE%
echo GitHub Release: https://github.com/chenxingpengs/Nexus/releases/tag/v%VERSION%
echo.
pause
