@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Nexus Installer Build Script
echo ========================================
echo.

set VERSION=1.0.0

echo [1/3] Cleaning old files...
if exist "bin\Release\net8.0-windows\win-x64\publish" rmdir /s /q "bin\Release\net8.0-windows\win-x64\publish"
if exist "Output" rmdir /s /q "Output"

echo [2/3] Publishing application...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b 1
)

echo [3/3] Building installer...
set ISCC_PATH=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe
if not exist "%ISCC_PATH%" set ISCC_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe
if not exist "%ISCC_PATH%" set ISCC_PATH=C:\Program Files\Inno Setup 6\ISCC.exe

"%ISCC_PATH%" installer.iss
if %errorlevel% neq 0 (
    echo Installer build failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build completed!
echo Installer: Output\Nexus-%VERSION%-win-x64.exe
echo ========================================
pause
