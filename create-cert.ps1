<#
.SYNOPSIS
    生成代码签名证书脚本
    
.DESCRIPTION
    此脚本用于生成自签名代码签名证书，用于对 Nexus 应用程序进行数字签名。
    生成的证书可用于签名 .exe 和 .dll 文件。
    
.PARAMETER CertificateName
    证书名称，默认为 "NexusCodeSigning"
    
.PARAMETER OutputPath
    证书输出路径，默认为当前目录下的 "certificates" 文件夹
    
.EXAMPLE
    .\create-cert.ps1
    使用默认参数生成证书
    
.EXAMPLE
    .\create-cert.ps1 -CertificateName "MyApp" -OutputPath "C:\Certs"
    指定证书名称和输出路径
#>

param(
    [string]$CertificateName = "NexusCodeSigning",
    [string]$OutputPath = ".\certificates"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Nexus 代码签名证书生成工具" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "[INFO] 创建输出目录: $OutputPath" -ForegroundColor Green
}

$pfxPath = Join-Path $OutputPath "$CertificateName.pfx"
$cerPath = Join-Path $OutputPath "$CertificateName.cer"

Write-Host "[1/3] 检查现有证书..." -ForegroundColor Yellow

if (Test-Path $pfxPath) {
    Write-Host "[WARN] 证书文件已存在: $pfxPath" -ForegroundColor Yellow
    $overwrite = Read-Host "是否覆盖现有证书? (Y/N)"
    if ($overwrite -ne "Y" -and $overwrite -ne "y") {
        Write-Host "[INFO] 操作已取消" -ForegroundColor Yellow
        exit 0
    }
    Remove-Item $pfxPath -Force
    if (Test-Path $cerPath) {
        Remove-Item $cerPath -Force
    }
}

Write-Host "[2/3] 生成代码签名证书..." -ForegroundColor Yellow

$certSubject = "CN=$CertificateName, O=珠海市红旗中学, C=CN"
$certPassword = Read-Host "请输入证书密码 (留空则自动生成)" -AsSecureString

if ($certPassword.Length -eq 0) {
    $randomPassword = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 16 | ForEach-Object { [char]$_ })
    $certPassword = ConvertTo-SecureString -String $randomPassword -Force -AsPlainText
    Write-Host "[INFO] 已自动生成证书密码: $randomPassword" -ForegroundColor Green
    Write-Host "[WARN] 请妥善保存此密码!" -ForegroundColor Yellow
}

try {
    $cert = New-SelfSignedCertificate `
        -Subject $certSubject `
        -Type CodeSigningCert `
        -KeyUsage DigitalSignature `
        -FriendlyName "$CertificateName Code Signing Certificate" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(10) `
        -KeyExportPolicy Exportable `
        -KeyLength 4096 `
        -HashAlgorithm SHA256 `
        -TextExtension @("2.5.29.19={text}CA=false")
    
    Write-Host "[INFO] 证书已创建，指纹: $($cert.Thumbprint)" -ForegroundColor Green
    
    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $certPassword | Out-Null
    Write-Host "[INFO] PFX 证书已导出: $pfxPath" -ForegroundColor Green
    
    Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
    Write-Host "[INFO] 公钥证书已导出: $cerPath" -ForegroundColor Green
    
    Write-Host "[3/3] 安装证书到受信任的根证书颁发机构..." -ForegroundColor Yellow
    
    $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
    $rootStore.Open("ReadWrite")
    
    $rootCerts = $rootStore.Certificates | Where-Object { $_.Subject -eq $certSubject }
    foreach ($oldCert in $rootCerts) {
        $rootStore.Remove($oldCert)
    }
    
    $rootStore.Add($cert)
    $rootStore.Close()
    Write-Host "[INFO] 证书已安装到受信任的根证书颁发机构" -ForegroundColor Green
    
    $trustedPublisherStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPublisher", "CurrentUser")
    $trustedPublisherStore.Open("ReadWrite")
    $trustedPublisherStore.Add($cert)
    $trustedPublisherStore.Close()
    Write-Host "[INFO] 证书已安装到受信任的发布者" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "证书生成完成!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "证书文件:" -ForegroundColor Cyan
    Write-Host "  PFX (私钥): $pfxPath" -ForegroundColor White
    Write-Host "  CER (公钥): $cerPath" -ForegroundColor White
    Write-Host ""
    Write-Host "注意事项:" -ForegroundColor Yellow
    Write-Host "  1. PFX 文件包含私钥，请妥善保管" -ForegroundColor White
    Write-Host "  2. 在其他电脑上需要安装 CER 文件到受信任的根证书颁发机构" -ForegroundColor White
    Write-Host "  3. 自签名证书不会消除 SmartScreen 警告，需要用户手动信任" -ForegroundColor White
    Write-Host "  4. 如需消除 SmartScreen 警告，请购买商业代码签名证书" -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Host "[ERROR] 证书生成失败: $_" -ForegroundColor Red
    exit 1
}
