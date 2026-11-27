<#
.SYNOPSIS
  Camada'nın Store paketini (MSIX, .NET içinde) üretir.
.DESCRIPTION
  1. Sürümü src/CamAda/CamAda.csproj <Version> özelliğinden okur (manifestte A.B.C.0).
  2. -SkipPublish yoksa out/msix/layout'u silip .NET içinde (self-contained) win-x64 yayımlar.
  3. .pdb dosyalarını siler ve katmanda gerekli dosyaları denetler; eksik varsa durur.
  4. Kimliği packaging/store-identity.json'dan okur (Partner Center > Ürün kimliği). Dosya yoksa yer tutucu kullanır;
     o paket Store'a yüklenemez. Manifesti doldurur, simgeleri kopyalar.
  5. packaging/Strings'teki .resw metinlerini makepri ile resources.pri'ye derler (Başlat girişleri İngilizce/Türkçe),
     Windows SDK'daki makeappx ile out/msix/Camada_<sürüm>_x64.msix üretir ve son satırda yolunu yazar.
  -Register: (yalnız geliştirici denemesi) katmanı gevşek kayıtla kurar. Geliştirici modu kapalıysa durur; kendisi açmaz.
  Çıktı bilerek depodaki out/ altında: %TEMP% yeniden başlatmada temizlenebiliyor.
.EXAMPLE
  .\tools\package.ps1
.EXAMPLE
  .\tools\package.ps1 -SkipPublish
#>
[CmdletBinding()]
param(
    [switch]$SkipPublish,
    [switch]$Register
)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repo 'src\CamAda\CamAda.csproj'
$out = Join-Path $repo 'out\msix'
$layout = Join-Path $out 'layout'

# 1) Sürüm
$version = ([xml](Get-Content -LiteralPath $csproj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version -or $version -notmatch '^\d+\.\d+\.\d+$') { throw 'CamAda.csproj içinde A.B.C biçiminde <Version> yok.' }
$packageVersion = "$version.0"

# 2) Yayımla
if (-not $SkipPublish) {
    if (Test-Path -LiteralPath $layout) { Remove-Item -LiteralPath $layout -Recurse -Force }
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if (-not $dotnet) { $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
    & $dotnet publish (Join-Path $repo 'src\CamAda') -c Release -r win-x64 --self-contained true -o $layout --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız oldu (çıkış kodu $LASTEXITCODE)." }
}
if (-not (Test-Path -LiteralPath $layout)) { throw "Katman yok: $layout (-SkipPublish olmadan çalıştırın)." }

# 3) Hata ayıklama dosyaları pakete girmez
Get-ChildItem -LiteralPath $layout -Filter *.pdb -Recurse | Remove-Item -Force

# 4) Katman denetimi: .NET, WPF, Windows Forms (ManagedShell) ve Ada'nın yerel kütüphaneleri
$required = @('hostfxr.dll', 'coreclr.dll', 'System.Private.CoreLib.dll', 'PresentationFramework.dll',
    'System.Windows.Forms.dll', 'sherpa-onnx-c-api.dll', 'onnxruntime.dll', 'Assets\WakeWord',
    'tr\CamAda.Core.resources.dll') # Türkçe arayüz metinleri (uydu derleme)
foreach ($item in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $layout $item))) { throw "Paket eksik: $item" }
}

# 5) Kimlik, manifest ve simgeler
$identityFile = Join-Path $repo 'packaging\store-identity.json'
if (Test-Path -LiteralPath $identityFile) {
    $id = Get-Content -LiteralPath $identityFile -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($key in 'Name', 'Publisher', 'PublisherDisplayName') {
        if (-not $id.$key) { throw "store-identity.json içinde '$key' boş." }
    }
} else {
    Write-Warning 'packaging/store-identity.json yok: yer tutucu kimlik kullanılıyor; bu paket Store''a yüklenemez.'
    $id = [pscustomobject]@{ Name = 'CamAda.Dev'; Publisher = 'CN=CamAdaDev'; PublisherDisplayName = 'Camada (geliştirme)' }
}
$escape = { param($s) [Security.SecurityElement]::Escape([string]$s) }
$template = Get-Content -LiteralPath (Join-Path $repo 'packaging\Package.appxmanifest') -Raw -Encoding UTF8
$manifest = $template.Replace('{Name}', (& $escape $id.Name)).Replace('{Publisher}', (& $escape $id.Publisher)).
    Replace('{PublisherDisplayName}', (& $escape $id.PublisherDisplayName)).Replace('{Version}', $packageVersion)
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))
Copy-Item -Path (Join-Path $repo 'packaging\Assets\*') -Destination (Join-Path $layout 'Assets') -Force

# 6) Paketle
$sdkBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
function Find-SdkTool([string]$name) {
    Get-ChildItem -LiteralPath $sdkBin -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\$name" } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
}
$makeappx = Find-SdkTool 'makeappx.exe'
if (-not $makeappx) { throw "Windows SDK makeappx.exe bulunamadı ($sdkBin\<sürüm>\x64)." }
$makepri = Find-SdkTool 'makepri.exe'
if (-not $makepri) { throw "Windows SDK makepri.exe bulunamadı ($sdkBin\<sürüm>\x64)." }

New-Item -ItemType Directory -Force $out | Out-Null

# 6a) Manifestteki ms-resource: metinleri (Başlat menüsü girişleri, açıklama) resources.pri'ye; varsayılan dil en-US.
$strings = Join-Path $layout 'Strings'
if (Test-Path -LiteralPath $strings) { Remove-Item -LiteralPath $strings -Recurse -Force }
Copy-Item -LiteralPath (Join-Path $repo 'packaging\Strings') -Destination $strings -Recurse
Get-ChildItem -LiteralPath $layout -Filter 'resources*.pri' | Remove-Item -Force # önceki (bölünmüş) çıktılar kalmasın
$priConfig = Join-Path $repo 'packaging\priconfig.xml' # kökten dizinler, dilleri ayrı paketlere bölmez (dosyadaki açıklama)
$priLog = & $makepri new /pr $layout /cf $priConfig /mn (Join-Path $layout 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o 2>&1
if ($LASTEXITCODE -ne 0) { $priLog | Select-Object -Last 15 | ForEach-Object { Write-Output $_ }; throw "makepri new başarısız oldu." }
Remove-Item -LiteralPath $strings -Recurse -Force # metinler artık resources.pri içinde
$msix = Join-Path $out "Camada_${version}_x64.msix"
$log = & $makeappx pack /d $layout /p $msix /o 2>&1
if ($LASTEXITCODE -ne 0) {
    $log | Select-Object -Last 15 | ForEach-Object { Write-Output $_ }
    throw "makeappx başarısız oldu (çıkış kodu $LASTEXITCODE)."
}
$log | Where-Object { $_ -match 'succeeded' } | ForEach-Object { Write-Output $_ }

# 7) İsteğe bağlı: geliştirici denemesi için gevşek kayıt
if ($Register) {
    $devMode = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
    if ($devMode -ne 1) { throw 'Geliştirici modu kapalı; tools/dev-mode.ps1 -On ile (kullanıcı onayıyla) açın.' }
    Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml')
    Write-Output 'Katman gevşek kayıtla kuruldu.'
}

Write-Output $msix
