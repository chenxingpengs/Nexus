@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Inno Setup 签名工具配置
echo ========================================
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

set CERT_PATH=certificates\NexusCodeSigning.pfx
if not exist "%CERT_PATH%" (
    echo [ERROR] 证书文件不存在: %CERT_PATH%
    echo [INFO] 请先运行 create-cert.ps1 生成证书
    pause
    exit /b 1
)

set /p CERT_PASSWORD="请输入证书密码: "

set INNO_SETUP_DIR=%LOCALAPPDATA%\Programs\Inno Setup 6
if not exist "%INNO_SETUP_DIR%" set INNO_SETUP_DIR=C:\Program Files (x86)\Inno Setup 6
if not exist "%INNO_SETUP_DIR%" set INNO_SETUP_DIR=C:\Program Files\Inno Setup 6

if not exist "%INNO_SETUP_DIR%" (
    echo [ERROR] Inno Setup 未找到
    pause
    exit /b 1
)

set INNO_CONFIG=%INNO_SETUP_DIR%\ISPP\ISPPBuiltins.iss

echo.
echo [INFO] 配置 Inno Setup 签名工具...
echo [INFO] 配置文件: %INNO_CONFIG%

set SIGN_CMD="%SIGNTOOL_PATH%" sign /f "%CERT_PATH%" /p %CERT_PASSWORD% /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 "$f"

echo.
echo 请将以下内容添加到 Inno Setup 编译器设置中:
echo.
echo 1. 打开 Inno Setup Compiler
echo 2. 点击菜单 Tools ^> Configure Sign Tools...
echo 3. 点击 Add 按钮
echo 4. Name: MySignTool
echo 5. Command: %SIGN_CMD:"="%
echo.
echo 或者，您可以手动编辑 ISPPBuiltins.iss 文件添加以下行:
echo.
echo #define MySignTool "%SIGNTOOL_PATH:"='%" sign /f "%CERT_PATH:"='%" /p %CERT_PASSWORD% /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 "$f"
echo.

echo ========================================
echo 配置说明
echo ========================================
echo.
echo 注意: 证书密码已包含在签名命令中
echo 请确保 ISPPBuiltins.iss 文件安全
echo.
pause
