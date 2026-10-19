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
$buildRoot = Join-Path $artifacts ".portable-build-$version"
$explorerOutput = Join-Path $buildRoot "x64\$Configuration\net8.0-windows10.0.19041.0"
$portableNotice = Join-Path $root "portable\README.txt"
$portableMarker = Join-Path $root "portable\CopyPaste.Portable"
$artifactsFull = [IO.Path]::GetFullPath($artifacts).TrimEnd('\') + '\'
$publishFull = [IO.Path]::GetFullPath($publish)
$archiveFull = [IO.Path]::GetFullPath($archive)

if (-not $publishFull.StartsWith($artifactsFull, [StringComparison]::OrdinalIgnoreCase) -or
    -not $archiveFull.StartsWith($artifactsFull, [StringComparison]::OrdinalIgnoreCase) -or
    -not ([IO.Path]::GetFullPath($buildRoot)).StartsWith($artifactsFull, [StringComparison]::OrdinalIgnoreCase)) {
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
if (Test-Path -LiteralPath $buildRoot) {
    Remove-Item -LiteralPath $buildRoot -Recurse -Force
}

try {
    & $dotnet build (Join-Path $root "CopyPaste.sln") `
        -c $Configuration `
        -p:Platform=x64 `
        -p:BaseOutputPath="$buildRoot\" `
        -p:ExplorerCommandOutputPath="$explorerOutput"

    if ($LASTEXITCODE -ne 0) {
        throw "CopyPaste Release derlemesi başarısız oldu."
    }

    & $dotnet publish $projectPath `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:Platform=x64 `
        -p:WindowsPackageType=None `
        -p:PublishSingleFile=false `
        -p:BaseOutputPath="$buildRoot\" `
        -p:ExplorerCommandOutputPath="$explorerOutput" `
        -o $publish

    if ($LASTEXITCODE -ne 0) {
        throw "CopyPaste self-contained portable yayını oluşturulamadı."
    }

    # Paketlenmemiş WinUI yayını, uygulamanın kaynak dizinini (CopyPaste.App.pri) ve derlenmiş XAML'ı
    # (*.xbf) yayın klasörüne koymuyor (SDK bunları yalnız MSIX araçlarıyla kopyalıyor). Bunlar olmadan
    # ana pencere yüklenemez ve uygulama açılışta kapanır; aynı yayın derlemesinin çıktısından alınır.
    $appBuildOutput = Join-Path $buildRoot "x64\$Configuration\net8.0-windows10.0.19041.0\$Runtime"
    $appBuildOutputFull = [IO.Path]::GetFullPath($appBuildOutput).TrimEnd('\') + '\'
    $xamlResources = @(Get-ChildItem -LiteralPath $appBuildOutput -Recurse -File |
        Where-Object { $_.Name -eq "CopyPaste.App.pri" -or $_.Extension -eq ".xbf" })
    foreach ($resource in $xamlResources) {
        $target = Join-Path $publish $resource.FullName.Substring($appBuildOutputFull.Length)
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $resource.FullName -Destination $target -Force
    }

    if (-not (Test-Path -LiteralPath $portableNotice)) {
        throw "Portable kullanım bildirimi bulunamadı: $portableNotice"
    }
    if (-not (Test-Path -LiteralPath $portableMarker)) {
        throw "Portable dağıtım işareti bulunamadı: $portableMarker"
    }
    Copy-Item -LiteralPath $portableNotice -Destination (Join-Path $publish "README.txt") -Force
    Copy-Item -LiteralPath $portableMarker -Destination (Join-Path $publish "CopyPaste.Portable") -Force

    $requiredFiles = @("CopyPaste.App.exe", "CopyPaste.App.dll", "CopyPaste.Core.dll", "coreclr.dll", "hostfxr.dll",
        "CopyPaste.App.pri", "App.xbf", "MainWindow.xbf", "README.txt", "CopyPaste.Portable")
    foreach ($requiredFile in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $publish $requiredFile))) {
            throw "Portable pakette zorunlu dosya eksik: $requiredFile"
        }
    }

    Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $archive -CompressionLevel Optimal
    & (Join-Path $PSScriptRoot "Test-PortablePackage.ps1") -ArchivePath $archive -ExpectedVersion $version
    if ($LASTEXITCODE -ne 0) {
        throw "Portable paket smoke testi başarısız oldu."
    }
}
finally {
    if (Test-Path -LiteralPath $buildRoot) {
        Remove-Item -LiteralPath $buildRoot -Recurse -Force
    }
}

Write-Host "CopyPaste hazır: $archive"
