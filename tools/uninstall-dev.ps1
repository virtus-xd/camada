<#
.SYNOPSIS
  Geliştirme kurulumunu (tools/install.ps1) kaldırır; Store sürümüne geçerken kullanılır.
.DESCRIPTION
  Sırası:
    1. Çalışan CamAda'yı --restore-taskbar ile kapatır (görev çubuğu geri gelir).
    2. HKCU\...\Run altındaki 'CamAda' değerini yalnız Programs\CamAda'yı gösteriyorsa siler.
    3. Masaüstündeki 'Görev çubuğunu geri getir' kısayolunu siler.
    4. %LOCALAPPDATA%\Programs\CamAda klasörünü siler.
  DOKUNULMAYANLAR: %LOCALAPPDATA%\CamAda ve %APPDATA%\CamAda (ayarlar, Ada hafızası, anahtarlar, pano), Belgeler\Ada Notları,
  Resimler\Ekran Görüntüleri. -WhatIf ile yalnız ne yapacağını yazar.
.EXAMPLE
  .\tools\uninstall-dev.ps1 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param()
$ErrorActionPreference = 'Stop'

$target = Join-Path $env:LOCALAPPDATA 'Programs\CamAda'
$exe = Join-Path $target 'CamAda.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Görev çubuğunu geri getir.lnk'

if ((Test-Path -LiteralPath $exe) -and $PSCmdlet.ShouldProcess($exe, 'Görev çubuğunu geri getir ve CamAda''yı kapat (--restore-taskbar)')) {
    Start-Process -FilePath $exe -ArgumentList '--restore-taskbar' -Wait
    Start-Sleep -Milliseconds 500
}

$run = (Get-ItemProperty $runKey -Name CamAda -ErrorAction SilentlyContinue).CamAda
if ($run -and $run.IndexOf($target, [StringComparison]::OrdinalIgnoreCase) -ge 0 -and
    $PSCmdlet.ShouldProcess("$runKey\CamAda = $run", 'Açılışta başlatma değerini sil')) {
    Remove-ItemProperty $runKey -Name CamAda
}

if ((Test-Path -LiteralPath $shortcut) -and $PSCmdlet.ShouldProcess($shortcut, 'Kurtarma kısayolunu sil')) {
    Remove-Item -LiteralPath $shortcut
}

if ((Test-Path -LiteralPath $target) -and $PSCmdlet.ShouldProcess($target, 'Geliştirme kurulumu klasörünü sil')) {
    Remove-Item -LiteralPath $target -Recurse -Force
}

if (-not $WhatIfPreference) {
    Write-Output 'Geliştirme kurulumu kaldırıldı. Veriler yerinde: %LOCALAPPDATA%\CamAda, %APPDATA%\CamAda.'
}
