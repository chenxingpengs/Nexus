Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$CERT_PATH = "certificates\NexusCodeSigning.pfx"

if (-not (Test-Path $CERT_PATH)) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "未找到证书文件!" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "代码签名需要证书文件。" -ForegroundColor Cyan
    Write-Host "正在打开证书生成工具..." -ForegroundColor Cyan
    Write-Host ""
    
    $scriptPath = $PSScriptRoot
    $createCertPath = Join-Path $scriptPath "create-cert.ps1"
    
    if (Test-Path $createCertPath) {
        & powershell -ExecutionPolicy Bypass -File $createCertPath
    } else {
        Write-Host "[ERROR] 未找到 create-cert.ps1!" -ForegroundColor Red
        Read-Host "按回车键退出"
        exit 1
    }
    
    if (-not (Test-Path $CERT_PATH)) {
        Write-Host ""
        Write-Host "[INFO] 证书未创建，构建已取消。" -ForegroundColor Yellow
        Read-Host "按回车键退出"
        exit 0
    }
    
    Write-Host ""
    Write-Host "[INFO] 证书创建成功，正在启动构建工具..." -ForegroundColor Green
    Write-Host ""
}

function Show-BuildUI {
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Nexus 构建工具"
    $form.Size = New-Object System.Drawing.Size(500, 520)
    $form.StartPosition = "CenterScreen"
    $form.FormBorderStyle = "FixedSingle"
    $form.MaximizeBox = $false
    $form.BackColor = [System.Drawing.Color]::FromArgb(240, 240, 240)

    $titleFont = New-Object System.Drawing.Font("Microsoft YaHei", 14, [System.Drawing.FontStyle]::Bold)
    $labelFont = New-Object System.Drawing.Font("Microsoft YaHei", 10)
    $buttonFont = New-Object System.Drawing.Font("Microsoft YaHei", 10)

    $titleLabel = New-Object System.Windows.Forms.Label
    $titleLabel.Text = "Nexus 构建工具"
    $titleLabel.Font = $titleFont
    $titleLabel.Size = New-Object System.Drawing.Size(460, 30)
    $titleLabel.Location = New-Object System.Drawing.Point(20, 15)
    $titleLabel.TextAlign = "MiddleCenter"
    $form.Controls.Add($titleLabel)

    $versionLabel = New-Object System.Windows.Forms.Label
    $versionLabel.Text = "版本号:"
    $versionLabel.Font = $labelFont
    $versionLabel.Size = New-Object System.Drawing.Size(80, 25)
    $versionLabel.Location = New-Object System.Drawing.Point(30, 60)
    $form.Controls.Add($versionLabel)

    $versionTextBox = New-Object System.Windows.Forms.TextBox
    $versionTextBox.Font = $labelFont
    $versionTextBox.Size = New-Object System.Drawing.Size(150, 25)
    $versionTextBox.Location = New-Object System.Drawing.Point(110, 58)
    $versionTextBox.Text = "1.1.0"
    $form.Controls.Add($versionTextBox)

    $uploadGroupBox = New-Object System.Windows.Forms.GroupBox
    $uploadGroupBox.Text = "GitHub 上传选项"
    $uploadGroupBox.Font = $labelFont
    $uploadGroupBox.Size = New-Object System.Drawing.Size(440, 70)
    $uploadGroupBox.Location = New-Object System.Drawing.Point(20, 100)
    $form.Controls.Add($uploadGroupBox)

    $uploadYesRadio = New-Object System.Windows.Forms.RadioButton
    $uploadYesRadio.Text = "是，上传到 GitHub"
    $uploadYesRadio.Font = $labelFont
    $uploadYesRadio.Size = New-Object System.Drawing.Size(200, 25)
    $uploadYesRadio.Location = New-Object System.Drawing.Point(20, 25)
    $uploadYesRadio.Checked = $false
    $uploadGroupBox.Controls.Add($uploadYesRadio)

    $uploadNoRadio = New-Object System.Windows.Forms.RadioButton
    $uploadNoRadio.Text = "否，仅本地构建"
    $uploadNoRadio.Font = $labelFont
    $uploadNoRadio.Size = New-Object System.Drawing.Size(220, 25)
    $uploadNoRadio.Location = New-Object System.Drawing.Point(220, 25)
    $uploadNoRadio.Checked = $true
    $uploadGroupBox.Controls.Add($uploadNoRadio)

    $signGroupBox = New-Object System.Windows.Forms.GroupBox
    $signGroupBox.Text = "代码签名选项"
    $signGroupBox.Font = $labelFont
    $signGroupBox.Size = New-Object System.Drawing.Size(440, 100)
    $signGroupBox.Location = New-Object System.Drawing.Point(20, 180)
    $form.Controls.Add($signGroupBox)

    $signCheckBox = New-Object System.Windows.Forms.CheckBox
    $signCheckBox.Text = "使用证书签名应用程序"
    $signCheckBox.Font = $labelFont
    $signCheckBox.Size = New-Object System.Drawing.Size(250, 25)
    $signCheckBox.Location = New-Object System.Drawing.Point(20, 25)
    if (Test-Path $CERT_PATH) {
        $signCheckBox.Checked = $true
        $signCheckBox.Enabled = $true
    } else {
        $signCheckBox.Checked = $false
        $signCheckBox.Enabled = $false
        $signCheckBox.Text = "签名 (未找到证书)"
    }
    $signGroupBox.Controls.Add($signCheckBox)

    $passwordLabel = New-Object System.Windows.Forms.Label
    $passwordLabel.Text = "密码:"
    $passwordLabel.Font = $labelFont
    $passwordLabel.Size = New-Object System.Drawing.Size(80, 25)
    $passwordLabel.Location = New-Object System.Drawing.Point(20, 60)
    $passwordLabel.Enabled = $signCheckBox.Checked
    $signGroupBox.Controls.Add($passwordLabel)

    $passwordTextBox = New-Object System.Windows.Forms.TextBox
    $passwordTextBox.Font = $labelFont
    $passwordTextBox.Size = New-Object System.Drawing.Size(200, 25)
    $passwordTextBox.Location = New-Object System.Drawing.Point(100, 58)
    $passwordTextBox.PasswordChar = "*"
    $passwordTextBox.Enabled = $signCheckBox.Checked
    $signGroupBox.Controls.Add($passwordTextBox)

    $signCheckBox.Add_CheckedChanged({
        $passwordLabel.Enabled = $signCheckBox.Checked
        $passwordTextBox.Enabled = $signCheckBox.Checked
    })

    $notesLabel = New-Object System.Windows.Forms.Label
    $notesLabel.Text = "发布说明 (可选):"
    $notesLabel.Font = $labelFont
    $notesLabel.Size = New-Object System.Drawing.Size(150, 25)
    $notesLabel.Location = New-Object System.Drawing.Point(30, 295)
    $form.Controls.Add($notesLabel)

    $notesTextBox = New-Object System.Windows.Forms.TextBox
    $notesTextBox.Font = $labelFont
    $notesTextBox.Size = New-Object System.Drawing.Size(410, 60)
    $notesTextBox.Location = New-Object System.Drawing.Point(30, 325)
    $notesTextBox.Multiline = $true
    $notesTextBox.ScrollBars = "Vertical"
    $notesTextBox.Enabled = $false
    $form.Controls.Add($notesTextBox)

    $uploadYesRadio.Add_CheckedChanged({
        $notesTextBox.Enabled = $uploadYesRadio.Checked
    })

    $buildButton = New-Object System.Windows.Forms.Button
    $buildButton.Text = "构建"
    $buildButton.Font = $buttonFont
    $buildButton.Size = New-Object System.Drawing.Size(120, 35)
    $buildButton.Location = New-Object System.Drawing.Point(170, 400)
    $buildButton.BackColor = [System.Drawing.Color]::FromArgb(0, 122, 204)
    $buildButton.ForeColor = [System.Drawing.Color]::White
    $form.Controls.Add($buildButton)

    $cancelButton = New-Object System.Windows.Forms.Button
    $cancelButton.Text = "取消"
    $cancelButton.Font = $buttonFont
    $cancelButton.Size = New-Object System.Drawing.Size(80, 35)
    $cancelButton.Location = New-Object System.Drawing.Point(310, 400)
    $form.Controls.Add($cancelButton)

    $buildButton.Add_Click({
        $version = $versionTextBox.Text.Trim()
        if ([string]::IsNullOrEmpty($version)) {
            [System.Windows.Forms.MessageBox]::Show("请输入版本号!", "错误", "OK", "Error")
            return
        }

        $uploadToGitHub = $uploadYesRadio.Checked
        $releaseNotes = $notesTextBox.Text.Trim()
        $signApp = $signCheckBox.Checked
        $certPassword = $passwordTextBox.Text

        if ($signApp -and [string]::IsNullOrEmpty($certPassword)) {
            [System.Windows.Forms.MessageBox]::Show("请输入证书密码!", "错误", "OK", "Error")
            return
        }

        $form.DialogResult = "OK"
        $form.Tag = @{
            Version = $version
            UploadToGitHub = $uploadToGitHub
            ReleaseNotes = $releaseNotes
            SignApplication = $signApp
            CertificatePassword = $certPassword
        }
        $form.Close()
    })

    $cancelButton.Add_Click({
        $form.DialogResult = "Cancel"
        $form.Close()
    })

    $result = $form.ShowDialog()
    return $form.Tag
}

function Find-SignTool {
    $kitPaths = @()
    $baseDir = "$env:ProgramFiles(x86)\Windows Kits\10\bin"
    
    if (Test-Path $baseDir) {
        $versions = Get-ChildItem $baseDir -Directory | Where-Object { $_.Name -match "^10\." } | Sort-Object Name -Descending
        foreach ($ver in $versions) {
            $signtool = Join-Path $ver.FullName "x64\signtool.exe"
            if (Test-Path $signtool) {
                return $signtool
            }
        }
    }
    
    $fallbackPaths = @(
        "$env:ProgramFiles(x86)\Windows Kits\10\bin\x64\signtool.exe",
        "$env:ProgramFiles\Windows Kits\10\bin\x64\signtool.exe"
    )
    
    foreach ($path in $fallbackPaths) {
        if (Test-Path $path) {
            return $path
        }
    }
    
    return $null
}

function Sign-Files {
    param(
        [string]$Path,
        [string]$CertPath,
        [string]$Password,
        [string]$SignToolPath
    )
    
    $files = Get-ChildItem -Path $Path -Include *.exe, *.dll -Recurse
    $successCount = 0
    $failCount = 0
    
    foreach ($file in $files) {
        Write-Host "  签名: $($file.Name)" -ForegroundColor Cyan
        $result = & $SignToolPath sign /f $CertPath /p $Password /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 $file.FullName 2>&1
        if ($LASTEXITCODE -eq 0) {
            $successCount++
        } else {
            Write-Host "    [失败] $result" -ForegroundColor Red
            $failCount++
        }
    }
    
    return @{ Success = $successCount; Fail = $failCount }
}

$config = Show-BuildUI

if ($config -eq $null) {
    Write-Host "构建已取消"
    exit 0
}

$VERSION = $config.Version
$UPLOAD_GITHUB = $config.UploadToGitHub
$RELEASE_NOTES = $config.ReleaseNotes
$SIGN_APP = $config.SignApplication
$CERT_PASSWORD = $config.CertificatePassword

$OUTPUT_DIR = "Output"
$PUBLISH_DIR = "bin\Release\net8.0-windows\win-x64\publish"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "版本: $VERSION" -ForegroundColor Green
Write-Host "上传到 GitHub: $UPLOAD_GITHUB" -ForegroundColor Green
Write-Host "签名应用程序: $SIGN_APP" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/6] 清理旧文件..." -ForegroundColor Yellow
if (Test-Path $PUBLISH_DIR) { Remove-Item -Recurse -Force $PUBLISH_DIR }
if (Test-Path "$OUTPUT_DIR\Nexus-$VERSION-win-x64") { Remove-Item -Recurse -Force "$OUTPUT_DIR\Nexus-$VERSION-win-x64" }
if (Test-Path "$OUTPUT_DIR\Nexus-$VERSION-win-x64.zip") { Remove-Item -Force "$OUTPUT_DIR\Nexus-$VERSION-win-x64.zip" }
if (Test-Path "$OUTPUT_DIR\Nexus-$VERSION-win-x64.exe") { Remove-Item -Force "$OUTPUT_DIR\Nexus-$VERSION-win-x64.exe" }
if (-not (Test-Path $OUTPUT_DIR)) { New-Item -ItemType Directory -Path $OUTPUT_DIR | Out-Null }

Write-Host "[2/6] 更新版本号..." -ForegroundColor Yellow
$csprojPath = "Nexus.csproj"
$issPath = "installer.iss"

$content = Get-Content $csprojPath -Raw
$content = $content -replace '<Version>[^<]*</Version>', "<Version>$VERSION.0</Version>"
$content = $content -replace '<FileVersion>[^<]*</FileVersion>', "<FileVersion>$VERSION.0</FileVersion>"
$content = $content -replace '<AssemblyVersion>[^<]*</AssemblyVersion>', "<AssemblyVersion>$VERSION.0</AssemblyVersion>"
$content = $content -replace '<InformationalVersion>[^<]*</InformationalVersion>', "<InformationalVersion>$VERSION</InformationalVersion>"
Set-Content $csprojPath -Value $content -NoNewline

$content = Get-Content $issPath -Raw
$content = $content -replace '#define MyAppVersion "[^"]*"', "#define MyAppVersion `"$VERSION`""
Set-Content $issPath -Value $content -NoNewline

Write-Host "[3/6] 构建中..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "还原失败!" -ForegroundColor Red
    Read-Host "按回车键退出"
    exit 1
}

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    Write-Host "构建失败!" -ForegroundColor Red
    Read-Host "按回车键退出"
    exit 1
}

Write-Host "[4/6] 签名应用程序..." -ForegroundColor Yellow

if ($SIGN_APP) {
    $signToolPath = Find-SignTool
    
    if ($signToolPath) {
        Write-Host "使用 SignTool: $signToolPath" -ForegroundColor Cyan
        
        $result = Sign-Files -Path $PUBLISH_DIR -CertPath $CERT_PATH -Password $CERT_PASSWORD -SignToolPath $signToolPath
        
        Write-Host ""
        Write-Host "签名完成: $($result.Success) 成功, $($result.Fail) 失败" -ForegroundColor $(if ($result.Fail -eq 0) { "Green" } else { "Yellow" })
    } else {
        Write-Host "[WARN] 未找到 SignTool，跳过代码签名" -ForegroundColor Yellow
        Write-Host "[INFO] 请安装 Windows SDK 以启用代码签名" -ForegroundColor Yellow
    }
} else {
    Write-Host "跳过代码签名..." -ForegroundColor Gray
}

Write-Host "[5/6] 创建安装包..." -ForegroundColor Yellow
Copy-Item -Recurse -Force "$PUBLISH_DIR\*" "$OUTPUT_DIR\Nexus-$VERSION-win-x64\"

$ISCC_PATH = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $ISCC_PATH)) { $ISCC_PATH = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $ISCC_PATH)) { $ISCC_PATH = "C:\Program Files\Inno Setup 6\ISCC.exe" }

& $ISCC_PATH installer.iss
if ($LASTEXITCODE -ne 0) {
    Write-Host "安装包构建失败!" -ForegroundColor Red
    Read-Host "按回车键退出"
    exit 1
}

if ($SIGN_APP -and $signToolPath) {
    Write-Host "签名安装程序..." -ForegroundColor Cyan
    & $signToolPath sign /f $CERT_PATH /p $CERT_PASSWORD /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 "$OUTPUT_DIR\Nexus-$VERSION-win-x64.exe"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "安装程序签名成功!" -ForegroundColor Green
    } else {
        Write-Host "[WARN] 安装程序签名失败" -ForegroundColor Yellow
    }
}

Compress-Archive -Path "$OUTPUT_DIR\Nexus-$VERSION-win-x64" -DestinationPath "$OUTPUT_DIR\Nexus-$VERSION-win-x64.zip" -Force

Write-Host "[6/6] 构建后操作..." -ForegroundColor Yellow

if ($UPLOAD_GITHUB) {
    Write-Host "上传到 GitHub..." -ForegroundColor Yellow
    
    git stash
    git pull origin main --rebase
    if ($LASTEXITCODE -ne 0) {
        Write-Host "拉取失败，请手动解决冲突" -ForegroundColor Red
        git stash pop
        Read-Host "按回车键退出"
        exit 1
    }
    git stash pop
    
    git add -A
    git commit -m "release: v$VERSION"
    git push origin main
    if ($LASTEXITCODE -ne 0) {
        Write-Host "推送失败!" -ForegroundColor Red
        Read-Host "按回车键退出"
        exit 1
    }
    
    Write-Host "创建 GitHub Release..." -ForegroundColor Yellow
    
    if ([string]::IsNullOrEmpty($RELEASE_NOTES)) {
        $RELEASE_NOTES = "## Nexus v$VERSION 发布`n`n### 下载`n- Nexus-$VERSION-win-x64.exe - 安装程序 (推荐)`n- Nexus-$VERSION-win-x64.zip - 便携版`n`n### 系统要求`n- Windows x64`n- .NET 8.0 运行时 (已包含)"
    }
    gh release create "v$VERSION" "$OUTPUT_DIR\Nexus-$VERSION-win-x64.exe" "$OUTPUT_DIR\Nexus-$VERSION-win-x64.zip" --title "Nexus v$VERSION" --notes $RELEASE_NOTES
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "GitHub Release 创建失败!" -ForegroundColor Red
        Read-Host "按回车键退出"
        exit 1
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "构建并上传完成!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "版本: $VERSION" -ForegroundColor Cyan
    Write-Host "安装程序: $OUTPUT_DIR\Nexus-$VERSION-win-x64.exe" -ForegroundColor Cyan
    Write-Host "ZIP: $OUTPUT_DIR\Nexus-$VERSION-win-x64.zip" -ForegroundColor Cyan
    Write-Host "GitHub Release: https://github.com/chenxingpengs/Nexus/releases/tag/v$VERSION" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "构建完成! (未上传到 GitHub)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "版本: $VERSION" -ForegroundColor Cyan
    Write-Host "安装程序: $OUTPUT_DIR\Nexus-$VERSION-win-x64.exe" -ForegroundColor Cyan
    Write-Host "ZIP: $OUTPUT_DIR\Nexus-$VERSION-win-x64.zip" -ForegroundColor Cyan
}

Write-Host ""
Read-Host "按回车键退出"
