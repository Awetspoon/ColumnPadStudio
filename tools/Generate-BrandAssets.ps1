Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"

$assetsRoot = Join-Path $PSScriptRoot "..\ColumnPadStudio\Assets"
$assetsRoot = [IO.Path]::GetFullPath($assetsRoot)

function New-Color([string]$Hex, [int]$Alpha = 255) {
    $clean = $Hex.TrimStart('#')
    return [System.Drawing.Color]::FromArgb(
        $Alpha,
        [Convert]::ToInt32($clean.Substring(0, 2), 16),
        [Convert]::ToInt32($clean.Substring(2, 2), 16),
        [Convert]::ToInt32($clean.Substring(4, 2), 16))
}

function New-RoundedPath([float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = [Math]::Min([Math]::Min($Width, $Height), $Radius * 2)
    if ($diameter -le 0) {
        $path.AddRectangle([System.Drawing.RectangleF]::new($X, $Y, $Width, $Height))
        return $path
    }

    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Use-Graphics([System.Drawing.Bitmap]$Bitmap) {
    $graphics = [System.Drawing.Graphics]::FromImage($Bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    return $graphics
}

function Fill-RoundedRect($Graphics, $Brush, [float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = New-RoundedPath $X $Y $Width $Height $Radius
    try { $Graphics.FillPath($Brush, $path) } finally { $path.Dispose() }
}

function Draw-RoundedRect($Graphics, $Pen, [float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = New-RoundedPath $X $Y $Width $Height $Radius
    try { $Graphics.DrawPath($Pen, $path) } finally { $path.Dispose() }
}

function Draw-BackgroundColumns($Graphics, [int]$Width, [int]$Height) {
    $columns = @(
        @{ X = $Width * 0.70; Y = $Height * 0.12; W = $Width * 0.08; H = $Height * 0.72; A = 38 },
        @{ X = $Width * 0.80; Y = $Height * 0.06; W = $Width * 0.08; H = $Height * 0.78; A = 54 },
        @{ X = $Width * 0.90; Y = $Height * 0.16; W = $Width * 0.07; H = $Height * 0.66; A = 32 }
    )

    foreach ($column in $columns) {
        $brush = New-Object System.Drawing.SolidBrush (New-Color '#FFFFFF' $column.A)
        try {
            Fill-RoundedRect $Graphics $brush $column.X $column.Y $column.W $column.H ($column.W * 0.45)
        }
        finally {
            $brush.Dispose()
        }
    }
}

function Draw-LogoSymbol($Graphics, [float]$X, [float]$Y, [float]$Size) {
    $shadowBrush = New-Object System.Drawing.SolidBrush (New-Color '#0F172A' 34)
    try {
        Fill-RoundedRect $Graphics $shadowBrush ($X + ($Size * 0.035)) ($Y + ($Size * 0.05)) $Size $Size ($Size * 0.24)
    }
    finally {
        $shadowBrush.Dispose()
    }

    $outerRect = [System.Drawing.RectangleF]::new($X, $Y, $Size, $Size)
    $outerBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($outerRect, (New-Color '#203246'), (New-Color '#37506B'), 55)
    $outerPen = New-Object System.Drawing.Pen (New-Color '#F8F2E8' 88), ($Size * 0.018)
    try {
        Fill-RoundedRect $Graphics $outerBrush $X $Y $Size $Size ($Size * 0.24)
        Draw-RoundedRect $Graphics $outerPen ($X + ($Size * 0.018)) ($Y + ($Size * 0.018)) ($Size * 0.964) ($Size * 0.964) ($Size * 0.21)
    }
    finally {
        $outerBrush.Dispose()
        $outerPen.Dispose()
    }

    $innerPadding = $Size * 0.17
    $contentX = $X + $innerPadding
    $contentY = $Y + $innerPadding
    $contentW = $Size - ($innerPadding * 2)

    $ruleBrush = New-Object System.Drawing.SolidBrush (New-Color '#F4EBDD' 255)
    try {
        Fill-RoundedRect $Graphics $ruleBrush $contentX $contentY $contentW ($Size * 0.10) ($Size * 0.05)
    }
    finally {
        $ruleBrush.Dispose()
    }

    $cardGap = $Size * 0.05
    $cardW = ($contentW - ($cardGap * 2)) / 3
    $cardY = $contentY + ($Size * 0.14)
    $cardH = $Size * 0.44
    $cardRadius = $Size * 0.06

    for ($i = 0; $i -lt 3; $i++) {
        $cardX = $contentX + ($i * ($cardW + $cardGap))
        $topOffset = if ($i -eq 1) { $Size * 0.02 } else { 0 }
        $cardRect = [System.Drawing.RectangleF]::new($cardX, $cardY + $topOffset, $cardW, $cardH)
        $shadow = New-Object System.Drawing.SolidBrush (New-Color '#0F172A' 28)
        try {
            Fill-RoundedRect $Graphics $shadow ($cardX + ($Size * 0.01)) ($cardY + $topOffset + ($Size * 0.018)) $cardW $cardH $cardRadius
        }
        finally {
            $shadow.Dispose()
        }

        if ($i -eq 1) {
            $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush($cardRect, (New-Color '#E0A06D'), (New-Color '#C67A47'), 90)
            $stroke = New-Object System.Drawing.Pen (New-Color '#F6D3B2' 180), ($Size * 0.01)
        }
        else {
            $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush($cardRect, (New-Color '#FBF7F0'), (New-Color '#E9E0D1'), 90)
            $stroke = New-Object System.Drawing.Pen (New-Color '#E6D9C5' 200), ($Size * 0.01)
        }

        try {
            Fill-RoundedRect $Graphics $fill $cardX ($cardY + $topOffset) $cardW $cardH $cardRadius
            Draw-RoundedRect $Graphics $stroke ($cardX + ($Size * 0.004)) ($cardY + $topOffset + ($Size * 0.004)) ($cardW - ($Size * 0.008)) ($cardH - ($Size * 0.008)) ($cardRadius * 0.9)
        }
        finally {
            $fill.Dispose()
            $stroke.Dispose()
        }
    }

    $accentBrush = New-Object System.Drawing.SolidBrush (New-Color '#E0A06D' 255)
    try {
        Fill-RoundedRect $Graphics $accentBrush ($contentX + $cardW + ($cardGap * 0.55)) ($Y + $Size * 0.80) ($cardW * 0.9) ($Size * 0.045) ($Size * 0.02)
    }
    finally {
        $accentBrush.Dispose()
    }
}

function Save-Png([System.Drawing.Bitmap]$Bitmap, [string]$Path) {
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function New-IconBitmap([int]$Size) {
    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = Use-Graphics $bitmap
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        Draw-LogoSymbol $graphics ($Size * 0.08) ($Size * 0.06) ($Size * 0.84)
        return $bitmap
    }
    finally {
        $graphics.Dispose()
    }
}

function Draw-Wordmark($Graphics, [float]$X, [float]$Y, [float]$Scale, [bool]$ShowSubtitle) {
    $titleFont = New-Object System.Drawing.Font('Segoe UI Semibold', (110 * $Scale), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $subtitleFont = New-Object System.Drawing.Font('Segoe UI', (40 * $Scale), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $titleBrush = New-Object System.Drawing.SolidBrush (New-Color '#162130')
    $subtitleBrush = New-Object System.Drawing.SolidBrush (New-Color '#425066')
    $accentBrush = New-Object System.Drawing.SolidBrush (New-Color '#D18B5A')
    try {
        $graphics.DrawString('ColumnPad', $titleFont, $titleBrush, $X, $Y)
        if ($ShowSubtitle) {
            $graphics.DrawString('Multi-column writing workspace', $subtitleFont, $subtitleBrush, $X + (6 * $Scale), $Y + (122 * $Scale))
            Fill-RoundedRect $Graphics $accentBrush ($X + (8 * $Scale)) ($Y + (186 * $Scale)) (160 * $Scale) (12 * $Scale) (6 * $Scale)
        }
    }
    finally {
        $titleFont.Dispose()
        $subtitleFont.Dispose()
        $titleBrush.Dispose()
        $subtitleBrush.Dispose()
        $accentBrush.Dispose()
    }
}

function New-WordmarkBitmap([int]$Width, [int]$Height, [float]$IconSize, [float]$TextScale, [bool]$Transparent, [bool]$ShowSubtitle) {
    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = Use-Graphics $bitmap
    try {
        if ($Transparent) {
            $graphics.Clear([System.Drawing.Color]::Transparent)
        }
        else {
            $backgroundRect = [System.Drawing.RectangleF]::new(0, 0, $Width, $Height)
            $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($backgroundRect, (New-Color '#F6F1E8'), (New-Color '#E8E0D5'), 20)
            try {
                $graphics.FillRectangle($backgroundBrush, $backgroundRect)
            }
            finally {
                $backgroundBrush.Dispose()
            }
        }

        if (-not $Transparent) {
            Draw-BackgroundColumns $graphics $Width $Height
        }

        $iconY = ($Height - $IconSize) / 2
        Draw-LogoSymbol $graphics 32 $iconY $IconSize
        Draw-Wordmark $graphics ($IconSize + 84) ($Height * 0.18) $TextScale $ShowSubtitle
        return $bitmap
    }
    finally {
        $graphics.Dispose()
    }
}

function New-SplashBitmap([int]$Width, [int]$Height) {
    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = Use-Graphics $bitmap
    try {
        $backgroundRect = [System.Drawing.RectangleF]::new(0, 0, $Width, $Height)
        $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($backgroundRect, (New-Color '#F8F4ED'), (New-Color '#E9E0D5'), 30)
        try {
            $graphics.FillRectangle($backgroundBrush, $backgroundRect)
        }
        finally {
            $backgroundBrush.Dispose()
        }

        Draw-BackgroundColumns $graphics $Width $Height

        $panelX = 110
        $panelY = 132
        $panelW = $Width - 220
        $panelH = $Height - 264
        $panelShadow = New-Object System.Drawing.SolidBrush (New-Color '#0F172A' 20)
        $panelBrush = New-Object System.Drawing.SolidBrush (New-Color '#FFFDF9' 230)
        $panelPen = New-Object System.Drawing.Pen (New-Color '#D7CBB8' 180), 3
        try {
            Fill-RoundedRect $graphics $panelShadow ($panelX + 10) ($panelY + 14) $panelW $panelH 42
            Fill-RoundedRect $graphics $panelBrush $panelX $panelY $panelW $panelH 42
            Draw-RoundedRect $graphics $panelPen ($panelX + 1.5) ($panelY + 1.5) ($panelW - 3) ($panelH - 3) 40
        }
        finally {
            $panelShadow.Dispose()
            $panelBrush.Dispose()
            $panelPen.Dispose()
        }

        Draw-LogoSymbol $graphics 170 255 240
        Draw-Wordmark $graphics 470 258 1.2 $true

        $bodyFont = New-Object System.Drawing.Font('Segoe UI', 28, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
        $bodyBrush = New-Object System.Drawing.SolidBrush (New-Color '#506079')
        try {
            $graphics.DrawString('Notes, lists, and drafts laid out side by side.', $bodyFont, $bodyBrush, 474, 515)
        }
        finally {
            $bodyFont.Dispose()
            $bodyBrush.Dispose()
        }

        return $bitmap
    }
    finally {
        $graphics.Dispose()
    }
}

function Write-Ico([string[]]$PngPaths, [string]$IcoPath) {
    $entries = New-Object System.Collections.Generic.List[object]
    foreach ($pngPath in $PngPaths) {
        $bytes = [IO.File]::ReadAllBytes($pngPath)
        $image = [System.Drawing.Image]::FromFile($pngPath)
        try {
            $entries.Add([PSCustomObject]@{
                Width = $image.Width
                Height = $image.Height
                Bytes = $bytes
            }) | Out-Null
        }
        finally {
            $image.Dispose()
        }
    }

    $stream = [IO.File]::Open($IcoPath, [IO.FileMode]::Create, [IO.FileAccess]::Write)
    $writer = New-Object IO.BinaryWriter($stream)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$entries.Count)

        $offset = 6 + (16 * $entries.Count)
        foreach ($entry in $entries) {
            $widthByte = if ($entry.Width -ge 256) { 0 } else { $entry.Width }
            $writer.Write([byte]$widthByte)
            $heightByte = if ($entry.Height -ge 256) { 0 } else { $entry.Height }
            $writer.Write([byte]$heightByte)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$entry.Bytes.Length)
            $writer.Write([UInt32]$offset)
            $offset += $entry.Bytes.Length
        }

        foreach ($entry in $entries) {
            $writer.Write($entry.Bytes)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

$iconSizes = 16, 24, 32, 48, 64, 128, 256, 512, 1024
foreach ($size in $iconSizes) {
    $bitmap = New-IconBitmap $size
    try {
        Save-Png $bitmap (Join-Path $assetsRoot ("icon_{0}.png" -f $size))
    }
    finally {
        $bitmap.Dispose()
    }
}

$titlebar = New-WordmarkBitmap 1100 220 146 0.58 $true $false
try {
    Save-Png $titlebar (Join-Path $assetsRoot 'titlebar_brand.png')
}
finally {
    $titlebar.Dispose()
}

$wordmark = New-WordmarkBitmap 1600 420 250 0.92 $true $true
try {
    Save-Png $wordmark (Join-Path $assetsRoot 'wordmark_bar.png')
}
finally {
    $wordmark.Dispose()
}

$splash = New-SplashBitmap 1400 800
try {
    Save-Png $splash (Join-Path $assetsRoot 'splash.png')
}
finally {
    $splash.Dispose()
}

$icoPngs = @(16, 24, 32, 48, 64, 128, 256) | ForEach-Object { Join-Path $assetsRoot ("icon_{0}.png" -f $_) }
Write-Ico $icoPngs (Join-Path $assetsRoot 'ColumnNotepad.ico')