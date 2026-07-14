param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\src\CopyPaste.App\Assets')
)

Add-Type -AssemblyName System.Drawing
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

function New-CopyPasteAsset {
    param([string]$Name, [int]$Width, [int]$Height)

    $bitmap = [Drawing.Bitmap]::new($Width, $Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([Drawing.Color]::FromArgb(109, 93, 251))
        $fontSize = [Math]::Max(10, [Math]::Min($Width, $Height) * 0.32)
        $font = [Drawing.Font]::new('Segoe UI', $fontSize, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
        $format = [Drawing.StringFormat]::new()
        $format.Alignment = [Drawing.StringAlignment]::Center
        $format.LineAlignment = [Drawing.StringAlignment]::Center
        try {
            $graphics.DrawString('CP', $font, [Drawing.Brushes]::White, [Drawing.RectangleF]::new(0, 0, $Width, $Height), $format)
        }
        finally {
            $format.Dispose()
            $font.Dispose()
        }
        $bitmap.Save((Join-Path $OutputDirectory $Name), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

New-CopyPasteAsset 'Square44x44Logo.png' 44 44
New-CopyPasteAsset 'Square150x150Logo.png' 150 150
New-CopyPasteAsset 'StoreLogo.png' 50 50
