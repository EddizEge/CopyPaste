param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion
)

$ErrorActionPreference = "Stop"
$archive = [IO.Path]::GetFullPath($ArchivePath)
if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) {
    throw "Portable arşiv bulunamadı: $archive"
}

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
$testDirectory = Join-Path $tempRoot ("CopyPaste-PortableSmoke-" + [Guid]::NewGuid().ToString("N"))
$testDirectoryFull = [IO.Path]::GetFullPath($testDirectory)
if (-not $testDirectoryFull.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Portable smoke-test klasörü geçici dizinin dışında olamaz."
}

try {
    Expand-Archive -LiteralPath $archive -DestinationPath $testDirectoryFull
    $executable = Join-Path $testDirectoryFull "CopyPaste.App.exe"
    $resultFile = Join-Path $testDirectoryFull "portable-smoke-result.txt"
    foreach ($requiredFile in @("CopyPaste.App.exe", "CopyPaste.App.dll", "CopyPaste.Core.dll", "coreclr.dll", "hostfxr.dll", "README.txt", "CopyPaste.Portable")) {
        if (-not (Test-Path -LiteralPath (Join-Path $testDirectoryFull $requiredFile) -PathType Leaf)) {
            throw "Portable arşivde zorunlu dosya eksik: $requiredFile"
        }
    }

    $process = Start-Process -FilePath $executable `
        -ArgumentList @("--portable-smoke-test", "`"$resultFile`"") `
        -PassThru -Wait -WindowStyle Hidden
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $resultFile)) {
        throw "Portable uygulama sessiz smoke testini tamamlamadı."
    }
    $reportedVersion = (Get-Content -Raw -LiteralPath $resultFile).Trim()
    if ($reportedVersion -ne $ExpectedVersion) {
        throw "Portable sürüm uyuşmazlığı: beklenen $ExpectedVersion, bulunan $reportedVersion"
    }
    Write-Host "Portable smoke testi başarılı: CopyPaste $reportedVersion"
}
finally {
    if (Test-Path -LiteralPath $testDirectoryFull) {
        Remove-Item -LiteralPath $testDirectoryFull -Recurse -Force
    }
}
