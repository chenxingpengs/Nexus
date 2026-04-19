@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Nexus Build Script
echo ========================================
echo.

set /p VERSION="Enter version number (e.g., 1.1.0): "
if "%VERSION%"=="" (
    echo Version number is required!
    pause
    exit /b 1
)

echo.
echo Version: %VERSION%
echo.
set UPLOAD_GITHUB=
set /p UPLOAD_GITHUB="Upload to GitHub? (Y/N): "

set DO_UPLOAD=0
if /i "%UPLOAD_GITHUB%"=="Y" set DO_UPLOAD=1

if "%DO_UPLOAD%"=="1" goto :input_notes
goto :skip_notes

:input_notes
echo.
echo Enter release notes (press Enter to use default template):
set /p RELEASE_NOTES="> "

:skip_notes

set CERT_PATH=certificates\NexusCodeSigning.pfx
set DO_SIGN=0

if exist "%CERT_PATH%" (
    echo.
    set /p DO_SIGN_PROMPT="Certificate found. Sign the application? (Y/N): "
    if /i "!DO_SIGN_PROMPT!"=="Y" set DO_SIGN=1
)

if "%DO_SIGN%"=="1" (
    set /p CERT_PASSWORD="Enter certificate password: "
)

set OUTPUT_DIR=Output
set PUBLISH_DIR=bin\Release\net8.0-windows\win-x64\publish

echo.
echo ========================================
echo [1/6] Cleaning old files...
echo ========================================
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64" rmdir /s /q "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64"
if exist "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.zip" del /f /q "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.zip"
if exist "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.exe" del /f /q "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.exe"
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo.
echo ========================================
echo [2/6] Updating version in files...
echo ========================================

powershell -NoProfile -ExecutionPolicy Bypass -File "update-version.ps1" -Version "%VERSION%"
if %errorlevel% neq 0 (
    echo Version update failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo [3/6] Restoring and publishing...
echo ========================================
dotnet restore
if %errorlevel% neq 0 (
    echo Restore failed!
    pause
    exit /b 1
)

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo [4/6] Signing application...
echo ========================================

if "%DO_SIGN%"=="1" (
    echo Signing published files...
    
    set SIGNTOOL_PATH=
    for /f "delims=" %%i in ('dir /b /ad "%ProgramFiles(x86)%\Windows Kits\10\bin\10.*" 2^>nul') do (
        if exist "%ProgramFiles(x86)%\Windows Kits\10\bin\%%i\x64\signtool.exe" (
            set SIGNTOOL_PATH=%ProgramFiles(x86)%\Windows Kits\10\bin\%%i\x64\signtool.exe
        )
    )
    
    if exist "%SIGNTOOL_PATH%" (
        echo Using SignTool: %SIGNTOOL_PATH%
        
        echo Signing EXE files...
        for /r "%PUBLISH_DIR%" %%f in (*.exe) do (
            echo   Signing: %%~nxf
            "%SIGNTOOL_PATH%" sign /f "%CERT_PATH%" /p "%CERT_PASSWORD%" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 "%%f"
        )
        
        echo Signing DLL files...
        for /r "%PUBLISH_DIR%" %%f in (*.dll) do (
            echo   Signing: %%~nxf
            "%SIGNTOOL_PATH%" sign /f "%CERT_PATH%" /p "%CERT_PASSWORD%" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 "%%f"
        )
        
        echo Signing completed!
    ) else (
        echo [WARN] SignTool not found, skipping code signing
        echo [INFO] Please install Windows SDK to enable code signing
    )
) else (
    echo Skipping code signing...
)

echo.
echo ========================================
echo [5/6] Building packages...
echo ========================================

echo Copying published files...
xcopy /E /I /Y "%PUBLISH_DIR%\*" "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64%"

echo Building Inno Setup installer...
set ISCC_PATH=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe
if not exist "%ISCC_PATH%" set ISCC_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe
if not exist "%ISCC_PATH%" set ISCC_PATH=C:\Program Files\Inno Setup 6\ISCC.exe

"%ISCC_PATH%" installer.iss
if %errorlevel% neq 0 (
    echo Installer build failed!
    pause
    exit /b 1
)

if "%DO_SIGN%"=="1" (
    if exist "%SIGNTOOL_PATH%" (
        echo Signing installer...
        "%SIGNTOOL_PATH%" sign /f "%CERT_PATH%" /p "%CERT_PASSWORD%" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.exe"
        echo Installer signed!
    )
)

echo Creating ZIP archive...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%OUTPUT_DIR%\Nexus-%VERSION%-win-x64' -DestinationPath '%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.zip' -Force"

echo.
echo ========================================
echo [6/6] Post-build operations...
echo ========================================

if "%DO_UPLOAD%"=="1" (
    echo.
    echo Uploading to GitHub...
    
    cd /d "%~dp0"
    
    echo Pulling latest changes...
    git pull origin main --rebase
    if %errorlevel% neq 0 (
        echo Pull failed! Please resolve conflicts manually.
        pause
        exit /b 1
    )
    
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
    
    echo Creating GitHub Release...
    if "%RELEASE_NOTES%"=="" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content 'release-notes.md' -Raw) -replace 'VERSION_PLACEHOLDER', '%VERSION%' | Set-Content 'release-notes-temp.md' -NoNewline"
        gh release create v%VERSION% "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.exe" "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.zip" --title "Nexus v%VERSION%" --notes-file "release-notes-temp.md"
        del /f /q "release-notes-temp.md"
    ) else (
        gh release create v%VERSION% "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.exe" "%OUTPUT_DIR%\Nexus-%VERSION%-win-x64.zip" --title "Nexus v%VERSION%" --notes "%RELEASE_NOTES%"
    )

    if %errorlevel% neq 0 (
        echo GitHub Release creation failed! Please check if the release already exists.
        pause
        exit /b 1
    )
    
    echo.
    echo ========================================
    echo Build and Upload completed!
    echo ========================================
    echo.
    echo Version: %VERSION%
    echo Installer: %OUTPUT_DIR%\Nexus-%VERSION%-win-x64.exe
    echo ZIP: %OUTPUT_DIR%\Nexus-%VERSION%-win-x64.zip
    echo GitHub Release: https://github.com/chenxingpengs/Nexus/releases/tag/v%VERSION%
) else (
    echo.
    echo ========================================
    echo Build completed! (No GitHub upload)
    echo ========================================
    echo.
    echo Version: %VERSION%
    echo Installer: %OUTPUT_DIR%\Nexus-%VERSION%-win-x64.exe
    echo ZIP: %OUTPUT_DIR%\Nexus-%VERSION%-win-x64.zip
)

echo.
pause
