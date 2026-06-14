# Generates Gui\app.ico (a multi-resolution Windows icon) from a source PNG.
# Re-run this whenever the source logo changes:
#   powershell -ExecutionPolicy Bypass -File Scripts\Build-GuiIcon.ps1
#
# Each size is stored as a PNG-compressed frame, which Windows 10/11 read fine
# and which keeps the alpha channel crisp at small sizes.

[CmdletBinding()]
param(
    [string]$InputPng,
    [string]$OutputIco
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$repoRoot = Split-Path -Parent $scriptDir
if (-not $InputPng) { $InputPng = Join-Path $repoRoot 'icon.png' }
if (-not $OutputIco) { $OutputIco = Join-Path $repoRoot 'Gui\app.ico' }

$inFull = [System.IO.Path]::GetFullPath($InputPng)
$outFull = [System.IO.Path]::GetFullPath($OutputIco)
if (-not (Test-Path -LiteralPath $inFull)) {
    throw "Source PNG not found: $inFull"
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$src = [System.Drawing.Image]::FromFile($inFull)
$frames = @()
try {
    foreach ($s in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $s, $s
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.DrawImage($src, 0, 0, $s, $s)
        $g.Dispose()

        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $frames += , ($ms.ToArray())
        $ms.Dispose()
    }
}
finally {
    $src.Dispose()
}

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
# ICONDIR header
$bw.Write([UInt16]0)              # reserved
$bw.Write([UInt16]1)              # type = icon
$bw.Write([UInt16]$sizes.Count)   # image count

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $data = $frames[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }   # 0 encodes 256 in the ICO directory
    $bw.Write([Byte]$dim)         # width
    $bw.Write([Byte]$dim)         # height
    $bw.Write([Byte]0)            # palette colors
    $bw.Write([Byte]0)            # reserved
    $bw.Write([UInt16]1)          # color planes
    $bw.Write([UInt16]32)         # bits per pixel
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($data in $frames) { $bw.Write($data) }
$bw.Flush()

[System.IO.File]::WriteAllBytes($outFull, $out.ToArray())
$bw.Dispose()
$out.Dispose()

Write-Host "[OK] Wrote $outFull ($($sizes.Count) sizes: $($sizes -join ', '))."
