# THE PHIZZ LABEL (owner, 2026-11-23): the owner's PHIZZ splash logo on top and their hand-drawn BANANA SODA smaller below it, as on their can
# design, stacked into one transparent picture for the soda bottle (BottleShape: the soda bottle's decal).
#   powershell -ExecutionPolicy Bypass -File phizz_label.ps1
# Reads PhizzLogo.png and PhizzBananaSoda.png beside it (the owner's, transparent backgrounds), writes
# CityX/Assets/CityX/Weapons/Bottles/PhizzLabel.png. Each is cropped to what is drawn on it first.
param([string]$Out = "")
Add-Type -AssemblyName System.Drawing
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Out -eq "") { $Out = Join-Path $here "..\..\Assets\CityX\Weapons\Bottles\PhizzLabel.png" }

# How wide the wording is against the logo (narrower than the word PHIZZ, about half the splash: owner, "it shouldnt be wider than the phizz text"), the gap between them (less than none: the wording sits up among the splash's drips, as on the can), and the margin round it all, as shares of the logo's width.
$WordingWidth = 0.44
$Gap = -0.12
$Margin = 0.03
$Size = 1024
# The wording nudged right of the middle, pixels of the picture (owner: "it looks off the left a little bit. move it ... like 3 pixels", then "3 more", then "2 more").
$WordingShift = 8

function Read-Drawn([string]$path) {
    $bitmap = [System.Drawing.Bitmap]::FromFile((Resolve-Path $path))
    $left = $bitmap.Width; $top = $bitmap.Height; $right = -1; $bottom = -1
    $data = $bitmap.LockBits((New-Object System.Drawing.Rectangle 0, 0, $bitmap.Width, $bitmap.Height),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($data.Stride * $bitmap.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $bitmap.UnlockBits($data)
    for ($y = 0; $y -lt $bitmap.Height; $y++) {
        $row = $y * $data.Stride
        for ($x = 0; $x -lt $bitmap.Width; $x++) {
            if ($bytes[$row + $x * 4 + 3] -gt 24) {
                if ($x -lt $left) { $left = $x }
                if ($x -gt $right) { $right = $x }
                if ($y -lt $top) { $top = $y }
                if ($y -gt $bottom) { $bottom = $y }
            }
        }
    }
    return @{ Bitmap = $bitmap; Rect = (New-Object System.Drawing.Rectangle $left, $top, ($right - $left + 1), ($bottom - $top + 1)) }
}

$logo = Read-Drawn (Join-Path $here "PhizzLogo.png")
$wording = Read-Drawn (Join-Path $here "PhizzBananaSoda.png")

# Laid out in the logo's own pixels, then scaled to fit the square picture.
$logoW = $logo.Rect.Width; $logoH = $logo.Rect.Height
$wordW = $logoW * $WordingWidth
$wordH = $wordW * $wording.Rect.Height / $wording.Rect.Width
$margin = $logoW * $Margin
$totalW = $logoW + 2 * $margin
$totalH = $logoH + $logoW * $Gap + $wordH + 2 * $margin
$scale = $Size / [Math]::Max($totalW, $totalH)

$label = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($label)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$x0 = ($Size - $totalW * $scale) / 2
$y0 = ($Size - $totalH * $scale) / 2
$g.DrawImage($logo.Bitmap, (New-Object System.Drawing.RectangleF ([single]($x0 + $margin * $scale)), ([single]($y0 + $margin * $scale)),
    ([single]($logoW * $scale)), ([single]($logoH * $scale))), $logo.Rect, [System.Drawing.GraphicsUnit]::Pixel)
$g.DrawImage($wording.Bitmap, (New-Object System.Drawing.RectangleF ([single]($x0 + ($totalW - $wordW) / 2 * $scale + $WordingShift)),
    ([single]($y0 + ($margin + $logoH + $logoW * $Gap) * $scale)), ([single]($wordW * $scale)), ([single]($wordH * $scale))),
    $wording.Rect, [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$label.Save((Resolve-Path (Split-Path -Parent $Out)).Path + "\" + (Split-Path -Leaf $Out), [System.Drawing.Imaging.ImageFormat]::Png)
$label.Dispose(); $logo.Bitmap.Dispose(); $wording.Bitmap.Dispose()
Write-Output ("LABEL drawn part {0:N3} wide by {1:N3} tall of the square (width over height {2:N3})" -f ($totalW * $scale / $Size), ($totalH * $scale / $Size), ($totalW / $totalH))
