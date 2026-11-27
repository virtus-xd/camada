<#
.SYNOPSIS
  Store paketi için geçici simgeleri üretir (packaging/Assets).
.DESCRIPTION
  Koyu, köşeleri yuvarlatılmış kare üzerinde yatay, yarı saydam açık renkli bir hap: "cam ada". Son simge tasarımı
  yayına hazırlığın 5. işinde yapılacak; o zamana kadar Store ve Başlat menüsü bunu kullanır.
.EXAMPLE
  .\tools\make-icons.ps1
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$dir = Join-Path (Split-Path -Parent $PSScriptRoot) 'packaging\Assets'
New-Item -ItemType Directory -Force $dir | Out-Null

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

foreach ($icon in @(@{ Name = 'Square44x44Logo'; Size = 44 }, @{ Name = 'Square150x150Logo'; Size = 150 }, @{ Name = 'StoreLogo'; Size = 50 })) {
    $s = [float]$icon.Size
    $bmp = New-Object System.Drawing.Bitmap $icon.Size, $icon.Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    $bg = New-RoundedPath 0 0 $s $s ($s * 0.22)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 24, 28, 38))), $bg)

    # Cam ada: yatay hap, yarı saydam beyaz dolgu ve ince parlak kenar
    $w = $s * 0.66; $h = $s * 0.24
    $pill = New-RoundedPath (($s - $w) / 2) (($s - $h) / 2) $w $h ($h / 2)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 225, 236, 255))), $pill)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(230, 255, 255, 255)), ([Math]::Max(1.0, $s / 50))
    $g.DrawPath($pen, $pill)

    $g.Dispose()
    $path = Join-Path $dir ($icon.Name + '.png')
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "Yazıldı: $path ($($icon.Size)x$($icon.Size))"
}
