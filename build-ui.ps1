Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

function Show-BuildUI {
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Nexus Build Tool"
    $form.Size = New-Object System.Drawing.Size(500, 400)
    $form.StartPosition = "CenterScreen"
    $form.FormBorderStyle = "FixedSingle"
    $form.MaximizeBox = $false
    $form.BackColor = [System.Drawing.Color]::FromArgb(240, 240, 240)

    $titleFont = New-Object System.Drawing.Font("Microsoft YaHei", 14, [System.Drawing.FontStyle]::Bold)
    $labelFont = New-Object System.Drawing.Font("Microsoft YaHei", 10)
    $buttonFont = New-Object System.Drawing.Font("Microsoft YaHei", 10)

    $titleLabel = New-Object System.Windows.Forms.Label
    $titleLabel.Text = "Nexus Build Tool"
    $titleLabel.Font = $titleFont
    $titleLabel.Size = New-Object System.Drawing.Size(460, 30)
    $titleLabel.Location = New-Object System.Drawing.Point(20, 15)
    $titleLabel.TextAlign = "MiddleCenter"
    $form.Controls.Add($titleLabel)

    $versionLabel = New-Object System.Windows.Forms.Label
    $versionLabel.Text = "Version:"
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
    $uploadGroupBox.Text = "GitHub Upload Options"
    $uploadGroupBox.Font = $labelFont
    $uploadGroupBox.Size = New-Object System.Drawing.Size(440, 100)
    $uploadGroupBox.Location = New-Object System.Drawing.Point(20, 100)
    $form.Controls.Add($uploadGroupBox)

    $uploadYesRadio = New-Object System.Windows.Forms.RadioButton
    $uploadYesRadio.Text = "Yes, upload to GitHub"
    $uploadYesRadio.Font = $labelFont
    $uploadYesRadio.Size = New-Object System.Drawing.Size(200, 25)
    $uploadYesRadio.Location = New-Object System.Drawing.Point(20, 25)
    $uploadYesRadio.Checked = $false
    $uploadGroupBox.Controls.Add($uploadYesRadio)

    $uploadNoRadio = New-Object System.Windows.Forms.RadioButton
    $uploadNoRadio.Text = "No, local build only"
    $uploadNoRadio.Font = $labelFont
    $uploadNoRadio.Size = New-Object System.Drawing.Size(220, 25)
    $uploadNoRadio.Location = New-Object System.Drawing.Point(20, 55)
    $uploadNoRadio.Checked = $true
    $uploadGroupBox.Controls.Add($uploadNoRadio)

    $notesLabel = New-Object System.Windows.Forms.Label
    $notesLabel.Text = "Release Notes (optional):"
    $notesLabel.Font = $labelFont
    $notesLabel.Size = New-Object System.Drawing.Size(150, 25)
    $notesLabel.Location = New-Object System.Drawing.Point(30, 215)
    $form.Controls.Add($notesLabel)

    $notesTextBox = New-Object System.Windows.Forms.TextBox
    $notesTextBox.Font = $labelFont
    $notesTextBox.Size = New-Object System.Drawing.Size(410, 60)
    $notesTextBox.Location = New-Object System.Drawing.Point(30, 245)
    $notesTextBox.Multiline = $true
    $notesTextBox.ScrollBars = "Vertical"
    $notesTextBox.Enabled = $false
    $form.Controls.Add($notesTextBox)

    $uploadYesRadio.Add_CheckedChanged({
        $notesTextBox.Enabled = $uploadYesRadio.Checked
    })

    $buildButton = New-Object System.Windows.Forms.Button
    $buildButton.Text = "Build"
    $buildButton.Font = $buttonFont
    $buildButton.Size = New-Object System.Drawing.Size(120, 35)
    $buildButton.Location = New-Object System.Drawing.Point(170, 320)
    $buildButton.BackColor = [System.Drawing.Color]::FromArgb(0, 122, 204)
    $buildButton.ForeColor = [System.Drawing.Color]::White
    $form.Controls.Add($buildButton)

    $cancelButton = New-Object System.Windows.Forms.Button
    $cancelButton.Text = "Cancel"
    $cancelButton.Font = $buttonFont
    $cancelButton.Size = New-Object System.Drawing.Size(80, 35)
    $cancelButton.Location = New-Object System.Drawing.Point(310, 320)
    $form.Controls.Add($cancelButton)

    $buildButton.Add_Click({
        $version = $versionTextBox.Text.Trim()
        if ([string]::IsNullOrEmpty($version)) {
            [System.Windows.Forms.MessageBox]::Show("Please enter version number!", "Error", "OK", "Error")
            return
        }

        $uploadToGitHub = $uploadYesRadio.Checked
        $releaseNotes = $notesTextBox.Text.Trim()

        $form.DialogResult = "OK"
        $form.Tag = @{
            Version = $version
            UploadToGitHub = $uploadToGitHub
            ReleaseNotes = $releaseNotes
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

$config = Show-BuildUI

if ($config -eq $null) {
    Write-Host "Build cancelled"
    exit 0
}

$VERSION = $config.Version
$UPLOAD_GITHUB = $config.UploadToGitHub
$RELEASE_NOTES = $config.ReleaseNotes

$OUTPUT_DIR = "Output"
$PUBLISH_DIR = "bin\Release\net8.0-windows\win-x64\publish"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Version: $VERSION" -ForegroundColor Green
Write-Host "Upload to GitHub: $UPLOAD_GITHUB" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/5] Cleaning old files..." -ForegroundColor Yellow
if (Test-Path $PUBLISH_DIR) { Remove-Item -Recurse -Force $PUBLISH_DIR }
if (Test-Path "$OUTPUT_DIR\Nexus-$VERSION-win-x64") { Remove-Item -Recurse -Force "$OUTPUT_DIR\Nexus-$VERSION-win-x64" }
if (Test-Path "$OUTPUT_DIR\Nexus-$VERSION-win-x64.zip") { Remove-Item -Force "$OUTPUT_DIR\Nexus-$VERSION-win-x64.zip" }
if (Test-Path "$OUTPUT_DIR\Nexus-$VERSION-win-x64.exe") { Remove-Item -Force "$OUTPUT_DIR\Nexus-$VERSION-win-x64.exe" }
if (-not (Test-Path $OUTPUT_DIR)) { New-Item -ItemType Directory -Path $OUTPUT_DIR | Out-Null }

Write-Host "[2/5] Updating version..." -ForegroundColor Yellow
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

Write-Host "[3/5] Building..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Restore failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "[4/5] Creating packages..." -ForegroundColor Yellow
Copy-Item -Recurse -Force "$PUBLISH_DIR\*" "$OUTPUT_DIR\Nexus-$VERSION-win-x64\"

$ISCC_PATH = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $ISCC_PATH)) { $ISCC_PATH = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $ISCC_PATH)) { $ISCC_PATH = "C:\Program Files\Inno Setup 6\ISCC.exe" }

& $ISCC_PATH installer.iss
if ($LASTEXITCODE -ne 0) {
    Write-Host "Installer build failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Compress-Archive -Path "$OUTPUT_DIR\Nexus-$VERSION-win-x64" -DestinationPath "$OUTPUT_DIR\Nexus-$VERSION-win-x64.zip" -Force

Write-Host "[5/5] Post-build operations..." -ForegroundColor Yellow

if ($UPLOAD_GITHUB) {
    Write-Host "Uploading to GitHub..." -ForegroundColor Yellow
    
    git stash
    git pull origin main --rebase
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Pull failed, please resolve conflicts manually" -ForegroundColor Red
        git stash pop
        Read-Host "Press Enter to exit"
        exit 1
    }
    git stash pop
    
    git add -A
    git commit -m "release: v$VERSION"
    git push origin main
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Push failed!" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
    
    Write-Host "Creating GitHub Release..." -ForegroundColor Yellow
    
    if ([string]::IsNullOrEmpty($RELEASE_NOTES)) {
        $RELEASE_NOTES = "## Nexus v$VERSION Release`n`n### Download`n- Nexus-$VERSION-win-x64.exe - Installer (Recommended)`n- Nexus-$VERSION-win-x64.zip - Portable version`n`n### System Requirements`n- Windows x64`n- .NET 8.0 Runtime (self-contained)"
    }
    gh release create "v$VERSION" "$OUTPUT_DIR\Nexus-$VERSION-win-x64.exe" "$OUTPUT_DIR\Nexus-$VERSION-win-x64.zip" --title "Nexus v$VERSION" --notes $RELEASE_NOTES
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "GitHub Release creation failed!" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Build and Upload completed!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Version: $VERSION" -ForegroundColor Cyan
    Write-Host "Installer: $OUTPUT_DIR\Nexus-$VERSION-win-x64.exe" -ForegroundColor Cyan
    Write-Host "ZIP: $OUTPUT_DIR\Nexus-$VERSION-win-x64.zip" -ForegroundColor Cyan
    Write-Host "GitHub Release: https://github.com/chenxingpengs/Nexus/releases/tag/v$VERSION" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Build completed! (No GitHub upload)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Version: $VERSION" -ForegroundColor Cyan
    Write-Host "Installer: $OUTPUT_DIR\Nexus-$VERSION-win-x64.exe" -ForegroundColor Cyan
    Write-Host "ZIP: $OUTPUT_DIR\Nexus-$VERSION-win-x64.zip" -ForegroundColor Cyan
}

Write-Host ""
Read-Host "Press Enter to exit"
