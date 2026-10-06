# SPDX-License-Identifier: GPL-3.0-or-later
# Draws the application icon, "the curtain" (ADR 0030), and writes ImageVisualCompare.ico beside
# this script: a dim star field left of the turquoise divider, a brighter one right of it, the
# divider's handle in the middle. The drawing is on a 64-unit grid; each size of the icon is
# drawn at its own pixel size, not scaled from a larger one, so the divider stays an even number
# of whole pixels on the centre line and the stars stay visible at 16 px.
#
#   powershell -NoProfile -File src/ImageVisualCompare.Wpf/Assets/make-icon.ps1 [-PreviewFolder <dir>]
#
# -PreviewFolder also writes each size as a PNG there, to look at. The colours are Theme.xaml's.
param([string] $PreviewFolder)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

function Brush([string] $hex) {
    $b = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString($hex))
    $b.Freeze()
    $b
}

$darkSide = Brush '#0A0C10'
$lightSide = Brush '#232B37'
$accent = Brush '#3FE0C8'
$knob = Brush '#12151A'
$edge = Brush '#2C3340'

# x, y, radius on the 64 grid, and the colour; the left ones dim, the right ones bright.
$stars = @(
    @(13, 15, 1.3, '#5C6575'), @(22, 44, 1.8, '#6B7484'), @(9, 50, 1.0, '#4A5260'), @(20, 26, 1.0, '#4A5260'),
    @(44, 18, 1.8, '#F2F4F8'), @(53, 40, 1.3, '#C9D0DB'), @(41, 50, 1.0, '#AEB6C3'), @(50, 27, 1.0, '#AEB6C3')
)
# Below 32 px only the two largest stars of each side are drawn, one whole pixel each.
$smallStars = @(@(13, 15, '#6B7484'), @(22, 44, '#6B7484'), @(44, 18, '#F2F4F8'), @(53, 40, '#C9D0DB'))

function Render([int] $size) {
    $s = $size / 64.0
    $half = $size / 2
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $dc = $visual.RenderOpen()

    $radius = 13 * $s
    $tile = [System.Windows.Media.RectangleGeometry]::new([System.Windows.Rect]::new(0, 0, $size, $size), $radius, $radius)
    $dc.PushClip($tile)
    $dc.DrawRectangle($darkSide, $null, [System.Windows.Rect]::new(0, 0, $half, $size))
    $dc.DrawRectangle($lightSide, $null, [System.Windows.Rect]::new($half, 0, $half, $size))

    if ($size -ge 32) {
        foreach ($star in $stars) {
            $r = [Math]::Max($star[2] * $s, 0.7)
            $dc.DrawEllipse((Brush $star[3]), $null, [System.Windows.Point]::new($star[0] * $s, $star[1] * $s), $r, $r)
        }
    }
    else {
        foreach ($star in $smallStars) {
            $x = [Math]::Floor($star[0] * $s)
            $y = [Math]::Floor($star[1] * $s)
            $dc.DrawRectangle((Brush $star[2]), $null, [System.Windows.Rect]::new($x, $y, 1, 1))
        }
    }

    # The divider: an even width, so it sits on whole pixels either side of the centre line.
    $line = 2 * [Math]::Max(1, [Math]::Round(1.5 * $s))
    $dc.DrawRectangle($accent, $null, [System.Windows.Rect]::new($half - $line / 2, 0, $line, $size))

    if ($size -ge 32) {
        # The handle: a dark knob ringed in the accent, with the chevrons of the real one.
        $ring = [Math]::Max(3 * $s, 1.5)
        $pen = [System.Windows.Media.Pen]::new($accent, $ring)
        $dc.DrawEllipse($knob, $pen, [System.Windows.Point]::new($half, $half), 8 * $s, 8 * $s)
        # Geometry.Parse returns a frozen geometry; a scale pushed on the context draws it at size.
        $chevrons = [System.Windows.Media.Geometry]::Parse(
            "M30,29 L27,32 L30,35 M34,29 L37,32 L34,35")
        $dc.PushTransform([System.Windows.Media.ScaleTransform]::new($s, $s))
        $chevronPen = [System.Windows.Media.Pen]::new($accent, [Math]::Max(1.8, 1.2 / $s))
        $chevronPen.StartLineCap = 'Round'; $chevronPen.EndLineCap = 'Round'; $chevronPen.LineJoin = 'Round'
        $dc.DrawGeometry($null, $chevronPen, $chevrons)
        $dc.Pop()
    }
    else {
        # Too small for a ring with a hole: a solid knob, an even number of pixels across.
        $d = [Math]::Max(4, 2 * [Math]::Round(4 * $s))
        $dc.DrawEllipse($accent, $null, [System.Windows.Point]::new($half, $half), $d / 2, $d / 2)
    }

    $dc.Pop()
    # The outline keeps the tile's edge visible on a dark taskbar, where #0A0C10 alone vanishes.
    $border = [Math]::Max(1, [Math]::Round($s))
    $inset = $border / 2
    $outline = [System.Windows.Rect]::new($inset, $inset, $size - $border, $size - $border)
    $dc.DrawRoundedRectangle($null, [System.Windows.Media.Pen]::new($edge, $border), $outline, $radius - $inset, $radius - $inset)
    $dc.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    # The icon formats want straight alpha, not premultiplied.
    $straight = [System.Windows.Media.Imaging.FormatConvertedBitmap]::new($bitmap, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $pixels = [byte[]]::new($size * $size * 4)
    $straight.CopyPixels($pixels, $size * 4, 0)
    [pscustomobject]@{ Size = $size; Bitmap = $straight; Pixels = $pixels }
}

function Png($bitmapSource) {
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmapSource))
    $stream = [System.IO.MemoryStream]::new()
    $encoder.Save($stream)
    , $stream.ToArray()  # the comma keeps PowerShell from unrolling the bytes
}

# An icon entry below 256 px is a DIB: the header with twice the height, the rows bottom-up, then
# the 1-bit AND mask, all zero since the alpha channel carries the transparency. 256 px is a PNG.
function Dib($image) {
    $n = $image.Size
    $stream = [System.IO.MemoryStream]::new()
    $w = [System.IO.BinaryWriter]::new($stream)
    $maskRow = [int]([Math]::Ceiling($n / 32.0) * 4)
    $w.Write([int]40); $w.Write([int]$n); $w.Write([int](2 * $n)); $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]0); $w.Write([int]($n * $n * 4 + $maskRow * $n)); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
    for ($y = $n - 1; $y -ge 0; $y--) { $w.Write($image.Pixels, $y * $n * 4, $n * 4) }
    $w.Write([byte[]]::new($maskRow * $n))
    $w.Flush()
    , $stream.ToArray()  # the comma keeps PowerShell from unrolling the bytes
}

$images = foreach ($size in $sizes) { Render $size }
$payloads = foreach ($image in $images) { if ($image.Size -ge 256) { , [byte[]](Png $image.Bitmap) } else { , [byte[]](Dib $image) } }

$out = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($out)
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $edge = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$edge); $w.Write([byte]$edge); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$payloads[$i].Length); $w.Write([int]$offset)
    $offset += $payloads[$i].Length
}
foreach ($payload in $payloads) { $w.Write([byte[]]$payload) }
$w.Flush()
$icoPath = Join-Path $PSScriptRoot 'ImageVisualCompare.ico'
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
Write-Output "$icoPath ($($out.Length) bytes, sizes $($sizes -join ', '))"

if ($PreviewFolder) {
    New-Item -ItemType Directory -Force $PreviewFolder | Out-Null
    foreach ($image in $images) {
        [System.IO.File]::WriteAllBytes((Join-Path $PreviewFolder "icon-$($image.Size).png"), (Png $image.Bitmap))
    }
}
