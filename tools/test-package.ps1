<#
.SYNOPSIS
  Paketli Camada'yı bu bilgisayarda, gerçek verilere dokunmadan dener (yalnız geliştirici).
.DESCRIPTION
  Adımlar ayrı çağrılır: Hazirla -> Otomatik ya da Elle -> Temizle. Temizle her zaman güvenle tekrar çalıştırılabilir.
  Gerekenler: tools/package.ps1 çıktısı (out/msix/layout) ve geçici olarak açık geliştirici modu (tools/dev-mode.ps1 -On,
  kullanıcı onayıyla). Kayıt ve ekran görüntüleri: out/package-test/<oturum>/log.txt.

  Hazirla : başlangıç durumunu kaydeder, geliştirme kurulumunu görev çubuğunu geri getirerek kapatır, gerçek veri
            klasörlerini (%LOCALAPPDATA%\CamAda, %APPDATA%\CamAda) yedek adına taşır.
  Otomatik: IlkAcilis, Gizle, Kurtarma, Guncelleme, GizliyKaldirma senaryoları; her biri BEKLENEN/BEKLENMEYEN yazar.
  Elle    : paketi kaydedip açar; kullanıcı hızlı ayarları, Wi-Fi listesini ve diğer panelleri dener.
  Temizle : paketi kaldırır, veri klasörlerini, Run değerini ve otomatik gizleme durumunu geri koyar, geliştirme kurulumunu açar.
.EXAMPLE
  .\tools\test-package.ps1 -Adim Hazirla
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Hazirla', 'Otomatik', 'Elle', 'Durum', 'Temizle')]
    [string]$Adim
)
$ErrorActionPreference = 'Continue'

$repo = Split-Path -Parent $PSScriptRoot
$layout = Join-Path $repo 'out\msix\layout'
$testRoot = Join-Path $repo 'out\package-test'
$pointer = Join-Path $testRoot 'son-oturum.txt'
$devExe = Join-Path $env:LOCALAPPDATA 'Programs\CamAda\CamAda.exe'
$dataDirs = @((Join-Path $env:LOCALAPPDATA 'CamAda'), (Join-Path $env:APPDATA 'CamAda'))
$backupSuffix = '.yedek-paket'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

if ($Adim -eq 'Hazirla') {
    $session = Join-Path $testRoot (Get-Date -Format 'yyyyMMdd-HHmmss')
    New-Item -ItemType Directory -Force $session | Out-Null
    Set-Content -LiteralPath $pointer -Value $session -Encoding UTF8
} else {
    if (-not (Test-Path -LiteralPath $pointer)) { throw 'Önce -Adim Hazirla çalıştırın.' }
    $session = (Get-Content -LiteralPath $pointer -Raw).Trim()
}
$logFile = Join-Path $session 'log.txt'
$stateFile = Join-Path $session 'baslangic.json'

function Write-Log([string]$message) {
    $line = (Get-Date).ToString('HH:mm:ss') + ' ' + $message
    $line | Out-File -Append -Encoding utf8 -LiteralPath $logFile
    Write-Output $line
}

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
if (-not ('CamAdaPackageTest' -as [type])) {
    Add-Type @"
using System; using System.Runtime.InteropServices;
public static class CamAdaPackageTest {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string c, string w);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] struct RECT { public int l, t, r, b; }
  [StructLayout(LayoutKind.Sequential)] struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
  [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA d);
  public static int State() { var d = new APPBARDATA(); d.cbSize = Marshal.SizeOf(d); return (int)SHAppBarMessage(4, ref d); }
  public static void SetState(int s) { var d = new APPBARDATA(); d.cbSize = Marshal.SizeOf(d); d.hWnd = FindWindow("Shell_TrayWnd", null); d.lParam = (IntPtr)s; SHAppBarMessage(10, ref d); }
  public static bool TrayVisible() { var h = FindWindow("Shell_TrayWnd", null); return h != IntPtr.Zero && IsWindowVisible(h); }
  [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
  [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetPackageFullName(IntPtr h, ref int len, System.Text.StringBuilder name);
  // Sürecin paket kimliği: ad, "" (paketsiz) ya da "?" (açılamadı).
  public static string PackageOf(int pid) {
    var h = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
    if (h == IntPtr.Zero) return "?";
    try {
      int len = 0;
      int rc = GetPackageFullName(h, ref len, null);
      if (rc == 15700) return "";
      var sb = new System.Text.StringBuilder(len);
      return GetPackageFullName(h, ref len, sb) == 0 ? sb.ToString() : "?";
    } finally { CloseHandle(h); }
  }
}
"@
}

function Save-Screenshot([string]$name) {
    try {
        $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
        $bmp.Save((Join-Path $session "$name.png"))
        $g.Dispose(); $bmp.Dispose()
    } catch { Write-Log ('  ekran görüntüsü alınamadı: ' + $_.Exception.Message) }
}

function Get-RunValue { (Get-ItemProperty $runKey -Name CamAda -ErrorAction SilentlyContinue).CamAda }
function Get-CamAdaProcesses { @(Get-CimInstance Win32_Process -Filter "Name='CamAda.exe'") }
function Get-PackagedProcesses { @(Get-CamAdaProcesses | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($repo, [StringComparison]::OrdinalIgnoreCase) }) }
function Get-IdentityName { ([xml](Get-Content -LiteralPath (Join-Path $layout 'AppxManifest.xml') -Raw)).Package.Identity.Name }
function Get-TestPackage { Get-AppxPackage -Name (Get-IdentityName) }

function Write-Snapshot([string]$label) {
    Write-Log "== $label"
    Write-Log ('  Görev çubuğu: ' + $(if ([CamAdaPackageTest]::TrayVisible()) { 'görünür' } else { 'GİZLİ' }) + '   otomatik gizleme: ' + [CamAdaPackageTest]::State())
    $procs = Get-CamAdaProcesses
    Write-Log ('  CamAda süreçleri: ' + $procs.Count)
    foreach ($p in $procs) {
        $identity = [CamAdaPackageTest]::PackageOf([int]$p.ProcessId)
        $shown = if ($identity -eq '') { 'PAKETSİZ' } else { $identity }
        Write-Log ('    pid ' + $p.ProcessId + ' [kimlik: ' + $shown + '] : ' + $p.CommandLine)
    }
    Write-Log ('  Gerçek Run değeri: ' + $(if (Get-RunValue) { Get-RunValue } else { 'yok' }))
    $pkg = Get-TestPackage
    if ($pkg) {
        $data = Join-Path $env:LOCALAPPDATA ('Packages\' + $pkg.PackageFamilyName)
        $task = "HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\$($pkg.PackageFamilyName)\CamAdaStartup"
        Write-Log ('  Paket: ' + $pkg.Version + '   başlangıç görevi State=' + (Get-ItemProperty $task -ErrorAction SilentlyContinue).State + '   veri klasörü: ' + (Test-Path $data))
    } else {
        Write-Log '  Paket: kurulu değil'
    }
}

function Write-Verdict([string]$name, [bool]$ok, [string]$detail) {
    Write-Log ('  >> ' + $name + ': ' + $(if ($ok) { 'BEKLENEN' } else { 'BEKLENMEYEN' }) + ' - ' + $detail)
}

function Assert-DevMode {
    $v = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
    if ($v -ne 1) { throw 'Geliştirici modu kapalı; tools/dev-mode.ps1 -On ile (kullanıcı onayıyla) açın.' }
}

function Register-Layout([string]$path) {
    Add-AppxPackage -Register (Join-Path $path 'AppxManifest.xml') -ForceApplicationShutdown -ErrorAction Stop
    return Get-TestPackage
}

function Start-App([string]$appId) { Start-Process ('shell:AppsFolder\' + (Get-TestPackage).PackageFamilyName + '!' + $appId) }

# Paketten çalışan her CamAda süreci (bekçi ve yeniden başlayan örnek dahil) paket kimliği taşımalı.
function Test-AllPackaged {
    $procs = Get-PackagedProcesses
    return ($procs.Count -gt 0) -and -not ($procs | Where-Object { [CamAdaPackageTest]::PackageOf([int]$_.ProcessId) -in @('', '?') })
}

function Get-MainProcess { Get-PackagedProcesses | Where-Object { $_.CommandLine -notmatch '--watchdog' } | Select-Object -First 1 }

switch ($Adim) {
    'Hazirla' {
        foreach ($d in $dataDirs) { if (Test-Path -LiteralPath ($d + $backupSuffix)) { throw "Yedek zaten var: $d$backupSuffix — önce -Adim Temizle." } }
        $state = [ordered]@{
            TrayVisible = [CamAdaPackageTest]::TrayVisible(); AbmState = [CamAdaPackageTest]::State(); RunValue = (Get-RunValue)
            Files = @($dataDirs | ForEach-Object { if (Test-Path -LiteralPath $_) { @(Get-ChildItem -LiteralPath $_ -Recurse -File).Count } else { -1 } })
        }
        $state | ConvertTo-Json | Out-File -Encoding utf8 -LiteralPath $stateFile
        Write-Snapshot 'Başlangıç'
        if (Test-Path -LiteralPath $devExe) { Start-Process -FilePath $devExe -ArgumentList '--restore-taskbar' -Wait }
        Start-Sleep -Seconds 2
        if ((Get-CamAdaProcesses).Count) { throw 'CamAda hâlâ çalışıyor; deneme başlatılmadı.' }
        foreach ($d in $dataDirs) {
            if (Test-Path -LiteralPath $d) { Rename-Item -LiteralPath $d -NewName ((Split-Path $d -Leaf) + $backupSuffix) -ErrorAction Stop; Write-Log "  $d yedeğe alındı" }
        }
        Write-Snapshot 'Hazırlık sonrası (geliştirme kurulumu kapalı, veriler yedekte)'
    }

    'Otomatik' {
        Assert-DevMode
        $runBefore = Get-RunValue
        $pkg = Register-Layout $layout
        Write-Log ('Paket kaydı: ' + $pkg.PackageFullName)

        # 1) İlk açılış: yeni kullanıcı, görev çubuğu gizlenmez, izin penceresi çıkmaz
        Start-App 'CamAda'; Start-Sleep -Seconds 25
        Write-Snapshot '1) İlk açılıştan 25 sn sonra'; Save-Screenshot '1-ilk-acilis'
        Write-Verdict 'IlkAcilis' (((Get-PackagedProcesses).Count -ge 2) -and [CamAdaPackageTest]::TrayVisible() -and ((Get-RunValue) -eq $runBefore)) 'CamAda + bekçi çalışıyor, görev çubuğu görünür, Run değişmedi (izin penceresi için ekran görüntüsüne bakın)'
        Write-Verdict 'Kimlik' (Test-AllPackaged) 'ana süreç ve bekçi paket kimliği taşıyor'

        # 2) Gizle: kurtarma girişiyle kapat, ayarda gizlemeyi aç, yeniden başlat
        Start-App 'Kurtar'; Start-Sleep -Seconds 8
        $settings = Join-Path $env:LOCALAPPDATA ('Packages\' + $pkg.PackageFamilyName + '\LocalCache\Roaming\CamAda\settings.json')
        $json = Get-Content -LiteralPath $settings -Raw -Encoding UTF8 | ConvertFrom-Json
        $json.HideWindowsTaskbar = $true
        [IO.File]::WriteAllText($settings, ($json | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding $false))
        Start-App 'CamAda'; Start-Sleep -Seconds 25
        Write-Snapshot '2) Gizleme açılıp yeniden başlatıldıktan sonra'
        Write-Verdict 'Gizle' (-not [CamAdaPackageTest]::TrayVisible()) 'görev çubuğu gizli'

        # 2b) Çökme/öldürme: yalnız ana süreç ölürse bekçi görev çubuğunu geri getirmeli
        $main = Get-MainProcess
        if ($main -and -not [CamAdaPackageTest]::TrayVisible()) {
            Stop-Process -Id $main.ProcessId -Force
            Start-Sleep -Seconds 6
            Write-Snapshot '2b) Yalnız ana süreç öldürüldükten 6 sn sonra'; Save-Screenshot '2b-ana-surec-olduruldu'
            Write-Verdict 'AnaSurecOldurme' ([CamAdaPackageTest]::TrayVisible()) 'bekçi görev çubuğunu geri getirdi'
        } else {
            Write-Verdict 'AnaSurecOldurme' $false 'ön koşul yok (ana süreç bulunamadı ya da görev çubuğu gizli değil)'
        }
        Get-PackagedProcesses | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        Start-App 'CamAda'; Start-Sleep -Seconds 25

        # 3) Kurtarma girişi
        Start-App 'Kurtar'; Start-Sleep -Seconds 10
        Write-Snapshot '3) Kurtarma girişinden 10 sn sonra'; Save-Screenshot '3-kurtarma'
        Write-Verdict 'Kurtarma' ([CamAdaPackageTest]::TrayVisible() -and (Get-CamAdaProcesses).Count -eq 0) 'görev çubuğu görünür, süreç yok'

        # 4) Güncelleme: görev çubuğu gizliyken daha yüksek sürüm kaydı
        Start-App 'CamAda'; Start-Sleep -Seconds 25
        $updated = Join-Path $session 'layout-guncelleme'
        Copy-Item -LiteralPath $layout -Destination $updated -Recurse -Force
        $manifestPath = Join-Path $updated 'AppxManifest.xml'
        $m = [IO.File]::ReadAllText($manifestPath)
        $old = ([xml]$m).Package.Identity.Version
        $v = [version]$old
        $new = '{0}.{1}.{2}.0' -f $v.Major, $v.Minor, ($v.Build + 1)
        [IO.File]::WriteAllText($manifestPath, $m.Replace("Version=""$old""", "Version=""$new"""), (New-Object Text.UTF8Encoding $false))
        $strandedAt = $null
        $job = Start-Job -ScriptBlock { param($p) Add-AppxPackage -Register $p -ForceApplicationShutdown } -ArgumentList $manifestPath
        for ($i = 0; $i -lt 20; $i++) {
            Start-Sleep -Seconds 2
            if (-not [CamAdaPackageTest]::TrayVisible() -and (Get-CamAdaProcesses).Count -eq 0 -and -not $strandedAt) { $strandedAt = (Get-Date).ToString('HH:mm:ss') }
        }
        Receive-Job $job -Wait -ErrorAction SilentlyContinue | Out-Null; Remove-Job $job -Force
        Write-Snapshot '4) Güncellemeden 40 sn sonra'; Save-Screenshot '4-guncelleme'
        $restarted = @(Get-CamAdaProcesses | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($updated, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
        Write-Verdict 'Guncelleme-gorev-cubugu' (-not $strandedAt) $(if ($strandedAt) { "görev çubuğu CamAda'sız gizli kaldı ($strandedAt)" } else { "görev çubuğu hiçbir örnekte CamAda'sız gizli değil" })
        Write-Verdict 'Guncelleme-yeniden-acilma' $restarted $(if ($restarted) { 'CamAda yeni sürümden yeniden açıldı' } else { 'yeniden açılmadı' })
        Write-Verdict 'Guncelleme-kimlik' (Test-AllPackaged) 'yeniden açılan örnek ve bekçisi paket kimliği taşıyor'

        # 5) Görev çubuğu gizliyken kaldırma
        if (-not (Get-PackagedProcesses).Count) { Start-App 'CamAda'; Start-Sleep -Seconds 25 }
        $pkg = Get-TestPackage
        Remove-AppxPackage -Package $pkg.PackageFullName
        Start-Sleep -Seconds 10
        Write-Snapshot '5) Gizliyken kaldırdıktan 10 sn sonra'; Save-Screenshot '5-kaldirma'
        $dataGone = -not (Test-Path (Join-Path $env:LOCALAPPDATA ('Packages\' + $pkg.PackageFamilyName)))
        Write-Verdict 'GizliyKaldirma' ([CamAdaPackageTest]::TrayVisible() -and (Get-CamAdaProcesses).Count -eq 0 -and ((Get-RunValue) -eq $runBefore) -and $dataGone) 'görev çubuğu görünür, süreç yok, Run değişmedi, paket verisi silindi'
    }

    'Elle' {
        Assert-DevMode
        $pkg = Register-Layout $layout
        Start-App 'CamAda'
        Write-Log ('Elle deneme: paket ' + $pkg.Version + ' açıldı. Hızlı ayarları ve Wi-Fi listesini açın; bitince -Adim Temizle.')
    }

    'Durum' {
        Write-Snapshot ('Durum (elle deneme sırasında)')
        Write-Verdict 'Kimlik' (Test-AllPackaged) 'paketten çalışan tüm CamAda süreçleri paket kimliği taşıyor'
    }

    'Temizle' {
        $state = if (Test-Path -LiteralPath $stateFile) { Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8 | ConvertFrom-Json } else { $null }
        Write-Log '== TEMİZLİK'
        Get-AppxPackage -Name (Get-IdentityName) | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName; Write-Log ('  paket kaldırıldı: ' + $_.PackageFullName) }
        Get-PackagedProcesses | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue; Write-Log ('  deneme süreci durduruldu: ' + $_.ProcessId) }
        foreach ($d in $dataDirs) {
            $b = $d + $backupSuffix
            if (Test-Path -LiteralPath $b) {
                if (Test-Path -LiteralPath $d) {
                    $aside = (Split-Path $d -Leaf) + '.paket-artik-' + (Get-Date -Format 'HHmmss')
                    Rename-Item -LiteralPath $d -NewName $aside; Write-Log "  deneme sırasında oluşan $d kenara alındı: $aside"
                }
                Rename-Item -LiteralPath $b -NewName (Split-Path $d -Leaf); Write-Log "  $d geri kondu"
            }
        }
        if ($state -and $state.RunValue -and (Get-RunValue) -ne $state.RunValue) { Set-ItemProperty $runKey -Name CamAda -Value $state.RunValue; Write-Log '  Run değeri geri kondu' }
        if (Test-Path -LiteralPath $devExe) { Start-Process -FilePath $devExe -ArgumentList '--restore-taskbar' -Wait }
        if ($state -and [CamAdaPackageTest]::State() -ne [int]$state.AbmState) { [CamAdaPackageTest]::SetState([int]$state.AbmState); Write-Log '  otomatik gizleme durumu geri kondu' }
        if (Test-Path -LiteralPath $devExe) { Start-Process -FilePath $devExe -WorkingDirectory (Split-Path $devExe); Start-Sleep -Seconds 8 }
        Write-Snapshot 'Temizlik sonrası'
        if ($state) {
            $files = @($dataDirs | ForEach-Object { if (Test-Path -LiteralPath $_) { @(Get-ChildItem -LiteralPath $_ -Recurse -File).Count } else { -1 } })
            Write-Verdict 'Temizlik' (($files -join ',') -eq ($state.Files -join ',') -and (Get-RunValue) -eq $state.RunValue) ('dosya sayıları ' + ($files -join '/') + ' (başlangıç ' + ($state.Files -join '/') + ')')
        }
    }
}
