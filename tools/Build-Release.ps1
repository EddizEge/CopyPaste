param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root ".dotnet\dotnet.exe"
$projectPath = Join-Path $root "src\CopyPaste.App\CopyPaste.App.csproj"
[xml]$project = Get-Content -Raw -LiteralPath $projectPath
$version = [string]($project.Project.PropertyGroup.Version | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($version)) { throw "Uygulama sürümü proje dosyasından okunamadı." }
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "CopyPaste-$version-$Runtime"
$archive = "$publish.zip"
$buildOutput = Join-Path $root "src\CopyPaste.App\bin\x64\$Configuration\net8.0-windows10.0.19041.0"
$artifactsFull = [IO.Path]::GetFullPath($artifacts).TrimEnd('\') + '\'
$publishFull = [IO.Path]::GetFullPath($publish)
$archiveFull = [IO.Path]::GetFullPath($archive)

if (-not $publishFull.StartsWith($artifactsFull, [StringComparison]::OrdinalIgnoreCase) -or
    -not $archiveFull.StartsWith($artifactsFull, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Yayın hedefi artifacts klasörünün dışında olamaz."
}

if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnetCommand) { throw "Kullanılabilir .NET SDK bulunamadı." }
    $dotnet = $dotnetCommand.Source
}

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
if (Test-Path -LiteralPath $publish) {
    Remove-Item -LiteralPath $publish -Recurse -Force
}
if (Test-Path -LiteralPath $archive) {
    Remove-Item -LiteralPath $archive -Force
}

& $dotnet build (Join-Path $root "CopyPaste.sln") `
    -c $Configuration `
    -p:Platform=x64

if ($LASTEXITCODE -ne 0) {
    throw "CopyPaste Release derlemesi başarısız oldu."
}

New-Item -ItemType Directory -Path $publish -Force | Out-Null
Get-ChildItem -LiteralPath $buildOutput |
    Where-Object { $_.Name -notin @("publish", "win-x64", "AppPackages") } |
    Copy-Item -Destination $publish -Recurse -Force

Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $archive -CompressionLevel Optimal
Write-Host "CopyPaste hazır: $archive"
