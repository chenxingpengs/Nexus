@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Nexus Release Build and Upload Script
echo ========================================
echo.

set VERSION=1.0.0
set OUTPUT_DIR=Output
set PUBLISH_DIR=bin\Release\net8.0-windows\win-x64\publish

echo [1/5] Cleaning old files...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"

echo [2/5] Restoring dependencies...
dotnet restore
if %errorlevel% neq 0 (
    echo Restore failed!
    pause
    exit /b 1
)

echo [3/5] Publishing Release version...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b 1
)

echo [4/5] Creating output directory...
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Copying published files...
xcopy /E /I /Y "%PUBLISH_DIR%\*" "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64\"

echo Creating ZIP archive...
powershell -Command "Compress-Archive -Path '%OUTPUT_DIR%\Nexus-%VERSION%-win-x64' -DestinationPath '%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.zip' -Force"

echo [5/5] Uploading to GitHub...

cd /d "%~dp0"

git add -A
git status

echo.
echo Commit message:
set /p COMMIT_MSG="Enter commit message (default: release: v%VERSION%): "
if "%COMMIT_MSG%"=="" set COMMIT_MSG=release: v%VERSION%

git commit -m "%COMMIT_MSG%"
if %errorlevel% neq 0 (
    echo No changes to commit or commit failed.
)

git push origin main
if %errorlevel% neq 0 (
    echo Push failed! Please check your network or credentials.
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build and Upload completed!
echo Output: %OUTPUT_DIR%\Nexus-%VERSION%-win-x64.zip
echo ========================================
echo.
echo Server URL: https://api.hqzx.me (Release mode)
echo.
pause
