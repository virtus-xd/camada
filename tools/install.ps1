<#
.SYNOPSIS
  CamAda'yı derleyip %LOCALAPPDATA%\Programs\CamAda klasörüne kurar ve başlatır.
.DESCRIPTION
  1. Çalışan CamAda'yı --restore-taskbar ile kapatır (görev çubuğu geri gelir, dosyalar serbest kalır).
  2. Release sürümünü kurulum klasörüne yayımlar (.NET 10 masaüstü çalışma zamanı gerekir).
  3. Masaüstündeki "Görev çubuğunu geri getir" kısayolunu kurulu exe'ye göre yeniden oluşturur.
  4. Kurulu CamAda'yı başlatır. Uygulama ilk açılışta oturum açılışında başlamak için kendini Run anahtarına yazar.
.EXAMPLE
  .\tools\install.ps1
#>
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$target = Join-Path $env:LOCALAPPDATA 'Programs\CamAda'
$exe = Join-Path $target 'CamAda.exe'

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
if (-not (Test-Path -LiteralPath $dotnet)) { throw '.NET SDK bulunamadı (dotnet).' }

# 1) Çalışan adayı güvenle kapat
$running = Get-Process -Name CamAda -ErrorAction SilentlyContinue | Where-Object { $_.Path } | Select-Object -First 1
if ($running) {
    Write-Output "Çalışan CamAda kapatılıyor: $($running.Path)"
    Start-Process -FilePath $running.Path -ArgumentList '--restore-taskbar' -Wait
    Start-Sleep -Milliseconds 500
}

# 2) Yayımla
Write-Output "Yayımlanıyor: $target"
& $dotnet publish (Join-Path $repo 'src\CamAda') -c Release -r win-x64 --self-contained false -o $target --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız oldu (çıkış kodu $LASTEXITCODE)." }

# 3) Kurtarma kısayolu kurulu exe'yi göstersin
& (Join-Path $PSScriptRoot 'create-restore-shortcut.ps1') -ExePath $exe

# 4) Başlat
Start-Process -FilePath $exe -WorkingDirectory $target
Write-Output "CamAda kuruldu ve başlatıldı: $exe"
