param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$InnoCompiler
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root "src\CopyPaste.App\CopyPaste.App.csproj"
[xml]$project = Get-Content -Raw -LiteralPath $projectPath
$version = [string]($project.Project.PropertyGroup.Version | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($version)) { throw "Uygulama sürümü okunamadı." }

$publish = Join-Path $root "artifacts\CopyPaste-$version-$Runtime"
$artifacts = Join-Path $root "artifacts"
$script = Join-Path $root "installer\CopyPaste.iss"
$installer = Join-Path $artifacts "CopyPaste-$version-Setup.exe"

if (-not (Test-Path -LiteralPath $publish)) {
    & (Join-Path $PSScriptRoot "Build-Release.ps1") -Configuration $Configuration -Runtime $Runtime
}

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) }
    $InnoCompiler = $candidates | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { $InnoCompiler = $command.Source }
}
if ([string]::IsNullOrWhiteSpace($InnoCompiler) -or -not (Test-Path -LiteralPath $InnoCompiler)) {
    throw "Inno Setup derleyicisi bulunamadı. JRSoftware.InnoSetup paketini kurun."
}

if (Test-Path -LiteralPath $installer) {
    Remove-Item -LiteralPath $installer -Force
}

& $InnoCompiler "/DAppVersion=$version" "/DSourceDir=$publish" "/DOutputDir=$artifacts" $script
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installer)) {
    throw "CopyPaste kurulum dosyası oluşturulamadı."
}

Write-Host "CopyPaste kurucusu hazır: $installer"
