@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Nexus 批量签名工具
echo ========================================
echo.

set CERT_PATH=certificates\NexusCodeSigning.pfx
set CERT_PASSWORD=

if not exist "%CERT_PATH%" (
    echo [ERROR] 证书文件不存在: %CERT_PATH%
    echo [INFO] 请先运行 create-cert.ps1 生成证书
    pause
    exit /b 1
)

set /p CERT_PASSWORD="请输入证书密码: "

echo.
echo [INFO] 证书文件: %CERT_PATH%
echo.

set SIGNTOOL_PATH=

for /f "delims=" %%i in ('dir /b /ad "%ProgramFiles(x86)%\Windows Kits\10\bin\10.*" 2^>nul') do (
    if exist "%ProgramFiles(x86)%\Windows Kits\10\bin\%%i\x64\signtool.exe" (
        set SIGNTOOL_PATH=%ProgramFiles(x86)%\Windows Kits\10\bin\%%i\x64\signtool.exe
    )
)

if not exist "%SIGNTOOL_PATH%" (
    echo [ERROR] SignTool 未找到，请安装 Windows SDK
    pause
    exit /b 1
)

echo [INFO] SignTool 路径: %SIGNTOOL_PATH%
echo.

set EXE_COUNT=0
set DLL_COUNT=0
set SUCCESS_COUNT=0
set FAIL_COUNT=0

echo ========================================
echo 签名发布目录中的文件...
echo ========================================
echo.

set PUBLISH_DIR=bin\Release\net8.0-windows\win-x64\publish

if not exist "%PUBLISH_DIR%" (
    echo [ERROR] 发布目录不存在: %PUBLISH_DIR%
    echo [INFO] 请先运行构建命令
    pause
    exit /b 1
)

echo [INFO] 正在签名 EXE 文件...
for /r "%PUBLISH_DIR%" %%f in (*.exe) do (
    set /a EXE_COUNT+=1
    echo [!EXE_COUNT!] 签名: %%~nxf
    "%SIGNTOOL_PATH%" sign /f "%CERT_PATH%" /p "%CERT_PASSWORD%" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 "%%f" >nul 2>&1
    if !errorlevel! equ 0 (
        set /a SUCCESS_COUNT+=1
        echo     [OK] 签名成功
    ) else (
        set /a FAIL_COUNT+=1
        echo     [FAIL] 签名失败
    )
)

echo.
echo [INFO] 正在签名 DLL 文件...
for /r "%PUBLISH_DIR%" %%f in (*.dll) do (
    set /a DLL_COUNT+=1
    echo [!DLL_COUNT!] 签名: %%~nxf
    "%SIGNTOOL_PATH%" sign /f "%CERT_PATH%" /p "%CERT_PASSWORD%" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 "%%f" >nul 2>&1
    if !errorlevel! equ 0 (
        set /a SUCCESS_COUNT+=1
        echo     [OK] 签名成功
    ) else (
        set /a FAIL_COUNT+=1
        echo     [FAIL] 签名失败
    )
)

echo.
echo ========================================
echo 签名统计
echo ========================================
echo EXE 文件: %EXE_COUNT%
echo DLL 文件: %DLL_COUNT%
echo 成功: %SUCCESS_COUNT%
echo 失败: %FAIL_COUNT%
echo ========================================
echo.

if %FAIL_COUNT% gtr 0 (
    echo [WARN] 部分文件签名失败
    pause
    exit /b 1
)

echo [SUCCESS] 所有文件签名完成!
pause
