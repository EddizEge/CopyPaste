param(
    [string]$Configuration = "Release",
    [string]$CertificatePath,
    [string]$CertificatePassword
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root ".dotnet\dotnet.exe"
$project = Join-Path $root "src\CopyPaste.App\CopyPaste.App.csproj"
$manifest = Join-Path $root "src\CopyPaste.App\Package.appxmanifest"
[xml]$projectXml = Get-Content -Raw -LiteralPath $project
$version = [string]($projectXml.Project.PropertyGroup.Version | Select-Object -First 1)
$arguments = @(
    "build", $project, "-c", $Configuration, "-p:Platform=x64",
    "-p:BuildMsix=true", "-p:GenerateAppxPackageOnBuild=true", "-p:AppxBundle=Never"
)
if (-not [string]::IsNullOrWhiteSpace($CertificatePath)) {
    if (-not (Test-Path -LiteralPath $CertificatePath)) { throw "MSIX sertifikası bulunamadı." }
    $manifestOriginal = [IO.File]::ReadAllText($manifest)
    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $CertificatePath,
        $CertificatePassword,
        [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    [xml]$manifestXml = $manifestOriginal
    $manifestXml.Package.Identity.Publisher = $certificate.Subject
    $utf8 = [Text.UTF8Encoding]::new($false)
    $writerSettings = [Xml.XmlWriterSettings]::new()
    $writerSettings.Encoding = $utf8
    $writerSettings.Indent = $true
    $writer = [Xml.XmlWriter]::Create($manifest, $writerSettings)
    try { $manifestXml.Save($writer) } finally { $writer.Dispose() }
    $arguments += "-p:AppxPackageSigningEnabled=true"
    $arguments += "-p:PackageCertificateKeyFile=$CertificatePath"
    $arguments += "-p:PackageCertificatePassword=$CertificatePassword"
}
else {
    $arguments += "-p:AppxPackageSigningEnabled=false"
}

try {
    & $dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "CopyPaste MSIX derlemesi başarısız oldu." }
}
finally {
    if ($null -ne $manifestOriginal) {
        [IO.File]::WriteAllText($manifest, $manifestOriginal, [Text.UTF8Encoding]::new($false))
    }
}
$package = Get-ChildItem (Join-Path $root "src\CopyPaste.App\AppPackages") -Filter "*.msix" -Recurse |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $package) { throw "MSIX paketi bulunamadı." }
$artifacts = Join-Path $root "artifacts"
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$output = Join-Path $artifacts "CopyPaste-$version-x64.msix"
Copy-Item -LiteralPath $package.FullName -Destination $output -Force
Write-Host "CopyPaste MSIX hazır: $output"
