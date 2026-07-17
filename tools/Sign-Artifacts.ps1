param(
    [Parameter(Mandatory = $true)][string[]]$Paths,
    [Parameter(Mandatory = $true)][string]$CertificatePath,
    [Parameter(Mandatory = $true)][string]$CertificatePassword,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $signtool) { throw "Windows SDK signtool.exe bulunamadı." }
if (-not (Test-Path -LiteralPath $CertificatePath)) { throw "Kod imzalama sertifikası bulunamadı." }

foreach ($path in $Paths) {
    if (-not (Test-Path -LiteralPath $path)) { throw "İmzalanacak dosya bulunamadı: $path" }
    & $signtool sign /fd SHA256 /f $CertificatePath /p $CertificatePassword /tr $TimestampUrl /td SHA256 $path
    if ($LASTEXITCODE -ne 0) { throw "Kod imzalama başarısız: $path" }
    & $signtool verify /pa /v $path
    if ($LASTEXITCODE -ne 0) { throw "Kod imzası doğrulanamadı: $path" }
}
