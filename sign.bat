@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Nexus 代码签名工具
echo ========================================
echo.

set CERT_PATH=certificates\NexusCodeSigning.pfx
set CERT_CER=certificates\NexusCodeSigning.cer

if "%~1"=="" (
    echo 用法: sign.bat ^<文件路径^> [证书密码]
    echo.
    echo 示例:
    echo   sign.bat Output\Nexus-2.10.0-win-x64\Nexus.exe
    echo   sign.bat Output\Nexus-2.10.0-win-x64.exe mypassword
    echo.
    pause
    exit /b 1
)

set TARGET_FILE=%~1
set CERT_PASSWORD=%~2

if not exist "%TARGET_FILE%" (
    echo [ERROR] 目标文件不存在: %TARGET_FILE%
    pause
    exit /b 1
)

if not exist "%CERT_PATH%" (
    echo [ERROR] 证书文件不存在: %CERT_PATH%
    echo [INFO] 请先运行 create-cert.ps1 生成证书
    pause
    exit /b 1
)

if "%CERT_PASSWORD%"=="" (
    set /p CERT_PASSWORD="请输入证书密码: "
)

echo.
echo [INFO] 目标文件: %TARGET_FILE%
echo [INFO] 证书文件: %CERT_PATH%
echo.

set SIGNTOOL_PATH=

if exist "%ProgramFiles(x86)%\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" (
    set SIGNTOOL_PATH=%ProgramFiles(x86)%\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe
)

if exist "%ProgramFiles(x86)%\Windows Kits\10\bin\10.0.22000.0\x64\signtool.exe" (
    set SIGNTOOL_PATH=%ProgramFiles(x86)%\Windows Kits\10\bin\10.0.22000.0\x64\signtool.exe
)

if exist "%ProgramFiles(x86)%\Windows Kits\10\bin\x64\signtool.exe" (
    set SIGNTOOL_PATH=%ProgramFiles(x86)%\Windows Kits\10\bin\x64\signtool.exe
)

for /f "delims=" %%i in ('dir /b /ad "%ProgramFiles(x86)%\Windows Kits\10\bin\10.*" 2^>nul') do (
    if exist "%ProgramFiles(x86)%\Windows Kits\10\bin\%%i\x64\signtool.exe" (
        set SIGNTOOL_PATH=%ProgramFiles(x86)%\Windows Kits\10\bin\%%i\x64\signtool.exe
    )
)

if exist "%SIGNTOOL_PATH%" (
    echo [INFO] 使用 SignTool 签名...
    echo [INFO] SignTool 路径: %SIGNTOOL_PATH%
    
    "%SIGNTOOL_PATH%" sign /f "%CERT_PATH%" /p "%CERT_PASSWORD%" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 "%TARGET_FILE%"
    
    if !errorlevel! equ 0 (
        echo.
        echo [SUCCESS] 签名成功!
        echo.
        echo [INFO] 验证签名...
        "%SIGNTOOL_PATH%" verify /pa "%TARGET_FILE%"
    ) else (
        echo.
        echo [ERROR] 签名失败!
        pause
        exit /b 1
    )
) else (
    echo [INFO] SignTool 未找到，使用 PowerShell 签名...
    
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
        "$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2('%CERT_PATH%', '%CERT_PASSWORD%'); " ^
        "$file = '%TARGET_FILE%'; " ^
        "Set-AuthenticodeSignature -FilePath $file -Certificate $cert -HashAlgorithm SHA256; " ^
        "if ($?) { Write-Host '[SUCCESS] 签名成功!' -ForegroundColor Green; $sig = Get-AuthenticodeSignature $file; Write-Host \"签名状态: $($sig.Status)\" } else { Write-Host '[ERROR] 签名失败!' -ForegroundColor Red; exit 1 }"
    
    if !errorlevel! neq 0 (
        echo [ERROR] 签名失败!
        pause
        exit /b 1
    )
)

echo.
echo ========================================
echo 签名完成!
echo ========================================
echo.
