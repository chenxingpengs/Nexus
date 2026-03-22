param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$csprojPath = "Nexus.csproj"
$issPath = "installer.iss"

$content = Get-Content $csprojPath -Raw
$content = $content -replace '<Version>[^<]*</Version>', "<Version>$Version.0</Version>"
$content = $content -replace '<FileVersion>[^<]*</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
$content = $content -replace '<AssemblyVersion>[^<]*</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$content = $content -replace '<InformationalVersion>[^<]*</InformationalVersion>', "<InformationalVersion>$Version</InformationalVersion>"
Set-Content $csprojPath -Value $content -NoNewline

$content = Get-Content $issPath -Raw
$content = $content -replace '#define MyAppVersion "[^"]*"', "#define MyAppVersion `"$Version`""
Set-Content $issPath -Value $content -NoNewline

Write-Host "Version updated to $Version"
