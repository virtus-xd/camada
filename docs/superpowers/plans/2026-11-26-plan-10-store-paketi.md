# Plan 10 — Store Paketi ve Güvenli Kaldırma Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** CamAda'yı Store'a gönderilebilir, .NET'i içinde taşıyan bir MSIX paketine çevirmek; paketli uygulamada açılışta
başlama, kurtarma girişi, güncellemeden sonra yeniden açılma ve sürprizsiz ilk açılışı sağlamak; paketi Store'a gizli
gönderip gerçek kurulumda doğrulamak.

**Architecture:** Kararlar (başlangıç görevi durumu → eylem, Wi‑Fi izninin ne zaman isteneceği) `CamAda.Core`'da saf ve
testli. Uygulamada küçük sarmalayıcılar: paket algısı, `StartupTask`/Run anahtarı servisi, güncelleme sonrası yeniden
başlatma kaydı. Paket `packaging/` altındaki manifest şablonu ve `tools/package.ps1` ile `out/msix/` altında üretilir.
Doğrulama önce yerelde (geçici geliştirici modu, gevşek kayıt), sonra Store'un gizli yayınıyla.

**Tech Stack:** .NET 10 WPF (+ Windows Forms derlemeleri), WinRT `Windows.ApplicationModel.StartupTask`,
`Windows.Devices.WiFi`, Windows SDK `makeappx.exe` (10.0.26100.0), PowerShell 5.1, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-26-store-paketi-design.md`

## Global Constraints

- Kullanıcıya dönük hiçbir adım sertifika, güven deposu, geliştirici modu ya da yönetici izni istemez. Betikler geliştirici
  modunu kendiliğinden açmaz; yalnız `tools/dev-mode.ps1` açar/kapatır ve yalnız kullanıcı onayıyla çalıştırılır.
- Store adı ve manifest `DisplayName`: **Camada**. Kurtarma girişi: `Görev çubuğunu geri getir`,
  `uap10:Parameters="--restore-taskbar"`. Uygulama kimlikleri `CamAda` ve `Kurtar`. Başlangıç görevi `TaskId="CamAdaStartup"`.
- Kimlik `packaging/store-identity.json` → `{ "Name": "...", "Publisher": "CN=...", "PublisherDisplayName": "..." }`.
  Dosya yoksa `CamAda.Dev` / `CN=CamAdaDev` / `Camada (geliştirme)` ve "bu paket Store'a yüklenemez" uyarısı.
- Sürüm `src/CamAda/CamAda.csproj` `<Version>1.0.0</Version>` → manifest `1.0.0.0` (dördüncü hane hep 0).
- Manifest: `TargetDeviceFamily Windows.Desktop MinVersion="10.0.22621.0" MaxVersionTested="10.0.26100.0"`, x64, `tr-TR`.
  Yetenekler: `rescap:runFullTrust`, `microphone`, `location`, `wiFiControl`, `bluetooth`, `radios`.
- Paket çıktısı yalnız depoda `out/` altında (git dışı); `%TEMP%` kullanılmaz.
- Paketliyken `HKCU\...\Run` yazılmaz; paketsizde (geliştirme kurulumu) bugünkü Run davranışı aynen kalır.
- Mikrofon açan otomatik deneme yok; Ada'nın paketli mikrofon denemesi kullanıcıyla elle.
- Yorumlar ve metinler Türkçe. Testler: `dotnet test tests/CamAda.Core.Tests`. Derleme: `dotnet build src/CamAda -c Release`
  0 uyarı.

## Review Focus

1. **Kullanıcı başlangıcı Görev Yöneticisi'nden kapatmış (`DisabledByUser`)**: uygulama zorla açmaz, menü işaretsiz görünür,
   menüye tıklanınca `ms-settings:startupapps` açılır. Görev 1 `Decide` tablosu (`DisabledByUser` satırları).
2. **Wi‑Fi adaptörü olmayan masaüstü bilgisayarda paketli sürüm**: konum izni hiç sorulmaz, Wi‑Fi listesi oku görünmez.
   Görev 2 `Packaged_without_wifi_radio_never_asks`.
3. **Paket katmanında eksik çalışma zamanı dosyası** (26.11'deki Windows Forms çökmesi gibi): paketleme durur, eksik dosyayı
   söyler. Görev 3 Adım 6.
4. **Görev çubuğu gizliyken güncelleme**: görev çubuğu hiçbir an CamAda'sız gizli kalmaz; CamAda yeniden açılır. Görev 4
   `Guncelleme` senaryosu.
5. **Geliştirme ve Store sürümü aynı anda kurulu**: ikinci açılan sessizce kapanır, kurtarma girişi ikisini de kapatır.
   Görev 6 Adım 7.

---

### Görev 1: Paketli yaşam döngüsü — paket algısı, açılışta başlama, güncellemeden sonra yeniden açılma

**Files:**
- Create: `src/CamAda.Core/Startup/StartupTaskDecision.cs`
- Test: `tests/CamAda.Core.Tests/Startup/StartupTaskDecisionTests.cs`
- Create: `src/CamAda/Startup/PackageContext.cs`
- Create: `src/CamAda/Startup/AutostartService.cs`
- Create: `src/CamAda/Startup/AppRestart.cs`
- Modify: `src/CamAda/App.xaml.cs` (`StartNormalAsync` ~satır 112–131, `SetStartWithWindows`/`ApplyAutostart` ~1040–1069, `IslandCommands` kurulumu ~169)
- Modify: `src/CamAda/Island/IslandCommands.cs:12` (belge yorumu)

**Interfaces:**
- Produces:
  - `public enum StartupTaskStatus { Disabled = 0, DisabledByUser = 1, Enabled = 2, DisabledByPolicy = 3, EnabledByPolicy = 4 }`
    (değerler `Windows.ApplicationModel.StartupTaskState` ile aynı; uygulama doğrudan dönüştürür)
  - `public enum StartupTaskAction { None, RequestEnable, Disable, BlockedByUser, BlockedByPolicy }`
  - `public static StartupTaskAction StartupTaskDecision.Decide(bool wanted, StartupTaskStatus status)`
  - `public static bool StartupTaskDecision.IsOn(StartupTaskStatus status)`
  - `internal static class PackageContext { public static bool IsPackaged { get; } }`
  - `internal sealed class AutostartService(bool packaged) { public const string TaskId = "CamAdaStartup"; public bool IsOn { get; } public Task<StartupTaskAction> ApplyAsync(bool wanted); }`
  - `internal static class AppRestart { public static void RegisterAfterUpdate(); }`

- [ ] **Adım 1: Başarısız testleri yaz** — `StartupTaskDecisionTests` (`[Theory]`):

```csharp
[Theory]
[InlineData(true,  StartupTaskStatus.Disabled,         StartupTaskAction.RequestEnable)]
[InlineData(true,  StartupTaskStatus.Enabled,          StartupTaskAction.None)]
[InlineData(true,  StartupTaskStatus.EnabledByPolicy,  StartupTaskAction.None)]
[InlineData(true,  StartupTaskStatus.DisabledByUser,   StartupTaskAction.BlockedByUser)]
[InlineData(true,  StartupTaskStatus.DisabledByPolicy, StartupTaskAction.BlockedByPolicy)]
[InlineData(false, StartupTaskStatus.Enabled,          StartupTaskAction.Disable)]
[InlineData(false, StartupTaskStatus.EnabledByPolicy,  StartupTaskAction.BlockedByPolicy)]
[InlineData(false, StartupTaskStatus.Disabled,         StartupTaskAction.None)]
[InlineData(false, StartupTaskStatus.DisabledByUser,   StartupTaskAction.None)]
[InlineData(false, StartupTaskStatus.DisabledByPolicy, StartupTaskAction.None)]
public void Decide_maps_wish_and_state(bool wanted, StartupTaskStatus status, StartupTaskAction expected) =>
    Assert.Equal(expected, StartupTaskDecision.Decide(wanted, status));

[Theory]
[InlineData(StartupTaskStatus.Enabled, true)]
[InlineData(StartupTaskStatus.EnabledByPolicy, true)]
[InlineData(StartupTaskStatus.Disabled, false)]
[InlineData(StartupTaskStatus.DisabledByUser, false)]
[InlineData(StartupTaskStatus.DisabledByPolicy, false)]
public void IsOn_only_for_enabled_states(StartupTaskStatus status, bool expected) =>
    Assert.Equal(expected, StartupTaskDecision.IsOn(status));

[Fact]
public void Status_values_match_windows_startup_task_state() =>
    Assert.Equal([0, 1, 2, 3, 4], Enum.GetValues<StartupTaskStatus>().Select(s => (int)s));
```

- [ ] **Adım 2: Çalıştır, derleme hatasıyla başarısız olduğunu gör**

Run: `dotnet test tests/CamAda.Core.Tests --filter StartupTaskDecisionTests`
Expected: FAIL (`StartupTaskDecision` tanımlı değil)

- [ ] **Adım 3: `StartupTaskDecision.cs`'i yaz** (iki enum + iki statik yöntem, yukarıdaki imzalar).

- [ ] **Adım 4: Testleri çalıştır**

Run: `dotnet test tests/CamAda.Core.Tests --filter StartupTaskDecisionTests`
Expected: PASS (16 test)

- [ ] **Adım 5: Uygulama sarmalayıcılarını yaz**
  - `PackageContext.IsPackaged`: `kernel32!GetCurrentPackageFullName(ref int length, char[]? name)` sıfır uzunlukla çağrılır;
    dönüş `15700` (`APPMODEL_ERROR_NO_PACKAGE`) ise paketsiz, `122` (`ERROR_INSUFFICIENT_BUFFER`) ise paketli. Bir kez
    hesaplanır.
  - `AutostartService.ApplyAsync`: paketliyse `StartupTask.GetAsync(TaskId)` → `Decide` → `RequestEnableAsync()` /
    `Disable()`; sonunda `IsOn = StartupTaskDecision.IsOn(son durum)`; COM hatası `App.LogCrash`'e yazılır, `None` döner.
    Paketsizse bugünkü `ApplyAutostart` gövdesi buraya taşınır (`AutostartCommand`), `IsOn = wanted` (yazma başarılıysa).
  - `AppRestart.RegisterAfterUpdate`: `kernel32!RegisterApplicationRestart("--restarting", 1 | 2 | 8)`
    (`RESTART_NO_CRASH | RESTART_NO_HANG | RESTART_NO_REBOOT`: yalnız paket güncellemesinden sonra yeniden açılır).
- [ ] **Adım 6: `App.xaml.cs`'e bağla**
  - `StartNormalAsync`: tek örnek kilidinden sonra `if (PackageContext.IsPackaged) AppRestart.RegisterAfterUpdate();`
    `ApplyAutostart(...)` yerine `_autostart = new AutostartService(PackageContext.IsPackaged); _ = _autostart.ApplyAsync(_settings.StartWithWindows);`
  - `IsStartWithWindows: () => _autostart!.IsOn`.
  - `SetStartWithWindows(bool enabled)`: ayarı kaydeder, `ApplyAsync(enabled)` bekler; sonuç `BlockedByUser` ya da
    `BlockedByPolicy` ise `ms-settings:startupapps` açılır (`UseShellExecute = true`).
  - Eski `ApplyAutostart` silinir; `IslandCommands` belge yorumu "paketliyse başlangıç görevini, değilse Run anahtarını" olur.
- [ ] **Adım 7: Derle ve tüm testleri çalıştır**

Run: `dotnet build src/CamAda -c Release` → 0 uyarı, 0 hata. `dotnet test tests/CamAda.Core.Tests` → hepsi PASS.

- [ ] **Adım 8: Commit**

```bash
git add src/CamAda.Core/Startup/StartupTaskDecision.cs tests/CamAda.Core.Tests/Startup/StartupTaskDecisionTests.cs src/CamAda/Startup src/CamAda/App.xaml.cs src/CamAda/Island/IslandCommands.cs
git commit -m "feat: paketliyken başlangıç görevi ve güncellemeden sonra yeniden açılma"
```

---

### Görev 2: Sürprizsiz ilk açılış — görev çubuğu varsayılanı ve Wi‑Fi izninin ertelenmesi

**Files:**
- Modify: `src/CamAda.Core/Settings/AppSettings.cs:27` (`HideWindowsTaskbar` varsayılanı `false`)
- Modify: `tests/CamAda.Core.Tests/Settings/SettingsStoreTests.cs:15`
- Create: `src/CamAda.Core/Network/WifiStartup.cs`
- Test: `tests/CamAda.Core.Tests/Network/WifiStartupTests.cs`
- Modify: `src/CamAda/QuickSettings/WifiService.cs` (yeni fabrika)
- Modify: `src/CamAda/App.xaml.cs:627-641` (`CreateWifiAsync`)

**Interfaces:**
- Consumes: `PackageContext.IsPackaged` (Görev 1), `RadioService.HasWifi` (mevcut; `_radios` Wi‑Fi kurulmadan önce hazır).
- Produces:
  - `public enum WifiStartupMode { None, RequestNow, Deferred }`
  - `public static WifiStartupMode WifiStartup.Decide(bool packaged, bool hasWifiRadio)`
  - `public static WifiService WifiService.CreateDeferred()` (adaptörsüz kurar; izin ilk taramada, yani Wi‑Fi listesi ilk
    açıldığında istenir — mevcut `EnsureAdapterAsync` ve `WifiListView` konum uyarısı aynen kullanılır)

- [ ] **Adım 1: Testleri yaz/güncelle**
  - `SettingsStoreTests.Missing_file_returns_defaults_and_writes_file`: `Assert.True(s.HideWindowsTaskbar)` →
    `Assert.False(s.HideWindowsTaskbar)`. `Old_file_without_version_turns_on_taskbar_hiding_once` ve
    `Versioned_file_keeps_users_choice` olduğu gibi kalır (mevcut kullanıcı etkilenmez).
  - `WifiStartupTests`:

```csharp
[Fact] public void Unpackaged_keeps_asking_at_startup() =>
    Assert.Equal(WifiStartupMode.RequestNow, WifiStartup.Decide(packaged: false, hasWifiRadio: true));
[Fact] public void Packaged_with_wifi_radio_defers_until_list_opens() =>
    Assert.Equal(WifiStartupMode.Deferred, WifiStartup.Decide(packaged: true, hasWifiRadio: true));
[Fact] public void Packaged_without_wifi_radio_never_asks() =>
    Assert.Equal(WifiStartupMode.None, WifiStartup.Decide(packaged: true, hasWifiRadio: false));
```

- [ ] **Adım 2: Çalıştır, başarısız olduğunu gör**

Run: `dotnet test tests/CamAda.Core.Tests --filter "WifiStartupTests|SettingsStoreTests"`
Expected: FAIL (`WifiStartup` yok; varsayılan testi `True` buluyor)

- [ ] **Adım 3: Uygula** — `AppSettings.HideWindowsTaskbar = false`; `WifiStartup.Decide`; `WifiService.CreateDeferred()`;
  `CreateWifiAsync` kararı uygular: `RequestNow` → bugünkü `WifiService.CreateAsync()`, `Deferred` → `CreateDeferred()`,
  `None` → `null`. `IsAbsentForTest("wifi")` denetimi en başta kalır.
- [ ] **Adım 4: Testleri çalıştır**

Run: `dotnet test tests/CamAda.Core.Tests` → hepsi PASS. `dotnet build src/CamAda -c Release` → 0 uyarı.

- [ ] **Adım 5: Commit**

```bash
git add src/CamAda.Core/Settings/AppSettings.cs tests/CamAda.Core.Tests/Settings/SettingsStoreTests.cs src/CamAda.Core/Network/WifiStartup.cs tests/CamAda.Core.Tests/Network/WifiStartupTests.cs src/CamAda/QuickSettings/WifiService.cs src/CamAda/App.xaml.cs
git commit -m "feat: yeni kullanıcıda görev çubuğu açık, paketliyken Wi-Fi izni liste açılınca"
```

---

### Görev 3: Paket şablonu ve `tools/package.ps1`

**Files:**
- Modify: `src/CamAda/CamAda.csproj` (`<UseWindowsForms>true</UseWindowsForms>`, `<Version>1.0.0</Version>`,
  `<ItemGroup><Using Remove="System.Windows.Forms" /><Using Remove="System.Drawing" /></ItemGroup>`)
- Create: `packaging/Package.appxmanifest` (şablon; `{Name}`, `{Publisher}`, `{PublisherDisplayName}`, `{Version}`)
- Create: `tools/make-icons.ps1` → `packaging/Assets/Square44x44Logo.png`, `Square150x150Logo.png`, `StoreLogo.png` (50×50)
  (geçici simge: koyu yuvarlak kare üzerinde yatay, yarı saydam açık renkli bir hap — "cam ada"; son simge 5. işte)
- Create: `tools/package.ps1`
- Modify: `.gitignore` (`out/`)

**Interfaces:**
- Produces: `tools/package.ps1 [-SkipPublish] [-Register]` → `out/msix/layout/` (katman) ve
  `out/msix/Camada_<Version>_x64.msix`; son satırda msix yolunu yazar. Görev 4 katmanı ve sürümü buradan alır.

- [ ] **Adım 1: Proje ayarlarını değiştir**, `dotnet build src/CamAda -c Release` → 0 uyarı (ad çakışması kalmamalı).
- [ ] **Adım 2: Manifest şablonunu yaz** — `packaging/Package.appxmanifest` (UTF-8; 26.11 denemesinde çalışan yapı):

```xml
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
  xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap uap10 desktop rescap">
  <Identity Name="{Name}" Publisher="{Publisher}" Version="{Version}" ProcessorArchitecture="x64" />
  <Properties><DisplayName>Camada</DisplayName><PublisherDisplayName>{PublisherDisplayName}</PublisherDisplayName><Logo>Assets\StoreLogo.png</Logo></Properties>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.22621.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
  <Resources><Resource Language="tr-TR" /></Resources>
  <Applications>
    <Application Id="CamAda" Executable="CamAda.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="Camada" Description="Görev çubuğunun yerine geçen cam ada" BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png" Square44x44Logo="Assets\Square44x44Logo.png" />
      <Extensions>
        <desktop:Extension Category="windows.startupTask" Executable="CamAda.exe" EntryPoint="Windows.FullTrustApplication">
          <desktop:StartupTask TaskId="CamAdaStartup" Enabled="true" DisplayName="Camada" />
        </desktop:Extension>
      </Extensions>
    </Application>
    <Application Id="Kurtar" Executable="CamAda.exe" EntryPoint="Windows.FullTrustApplication" uap10:Parameters="--restore-taskbar">
      <uap:VisualElements DisplayName="Görev çubuğunu geri getir" Description="Camada'yı kapatır ve Windows görev çubuğunu geri getirir"
        BackgroundColor="transparent" Square150x150Logo="Assets\Square150x150Logo.png" Square44x44Logo="Assets\Square44x44Logo.png" />
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
    <DeviceCapability Name="microphone" />
    <DeviceCapability Name="location" />
    <DeviceCapability Name="wiFiControl" />
    <DeviceCapability Name="bluetooth" />
    <DeviceCapability Name="radios" />
  </Capabilities>
</Package>
```
- [ ] **Adım 3: Simgeleri üret** — `powershell -File tools/make-icons.ps1`; üç PNG `packaging/Assets` altında, doğru boyutlarda.
- [ ] **Adım 4: `package.ps1`'i yaz** — sırası:
  1. `<Version>`'ı csproj'dan oku (yoksa hata).
  2. `-SkipPublish` yoksa `out/msix/layout`'u sil ve `dotnet publish src/CamAda -c Release -r win-x64 --self-contained true -o out/msix/layout`.
  3. `*.pdb` sil.
  4. Katman denetimi: `hostfxr.dll`, `coreclr.dll`, `System.Private.CoreLib.dll`, `PresentationFramework.dll`,
     `System.Windows.Forms.dll`, `sherpa-onnx-c-api.dll`, `onnxruntime.dll`, `Assets\WakeWord\` — eksik varsa
     `throw "Paket eksik: <ad>"`.
  5. Kimliği `packaging/store-identity.json`'dan oku (yoksa yer tutucu + uyarı); şablonu doldurup
     `out/msix/layout/AppxManifest.xml` yaz; `packaging/Assets`'i `layout/Assets`'e kopyala (mevcut `Fonts`, `Speaker`,
     `WakeWord` korunur).
  6. `makeappx.exe`'yi `${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\` altındaki en yeni sürümden bul (yoksa hata);
     `pack /d out/msix/layout /p out/msix/Camada_<Version>_x64.msix /o`.
  7. `-Register`: `HKLM:\...\AppModelUnlock` `AllowDevelopmentWithoutDevLicense` 1 değilse
     `throw "Geliştirici modu kapalı; tools/dev-mode.ps1 -On ile (kullanıcı onayıyla) açın."`; açıksa
     `Add-AppxPackage -Register out/msix/layout/AppxManifest.xml`.
- [ ] **Adım 5: Paketle**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/package.ps1`
Expected: kimlik uyarısı (henüz `store-identity.json` yok), "Package creation succeeded", `out/msix/Camada_1.0.0_x64.msix`
(~100–110 MB).

- [ ] **Adım 6: Eksik dosya denetimini dene** — `out/msix/layout/System.Windows.Forms.dll`'i geçici olarak yeniden adlandır,
  `tools/package.ps1 -SkipPublish` → `Paket eksik: System.Windows.Forms.dll` hatası; adı geri al.
- [ ] **Adım 7: Commit**

```bash
git add src/CamAda/CamAda.csproj packaging tools/make-icons.ps1 tools/package.ps1 .gitignore
git commit -m "build: Store paketi şablonu ve paketleme betiği"
```

---

### Görev 4: Yerel paket denemesi (betik + deneme)

**Files:**
- Create: `tools/dev-mode.ps1` (`-On` / `-Off`; yönetici olarak çalışır; `-Off` değeri siler — bu bilgisayarda başlangıçta değer yoktu)
- Create: `tools/test-package.ps1` (26.11 `spike-b` betiklerinin kalıcı hâli: `ortak.ps1` + `deney.ps1` + `temizle.ps1`)

**Interfaces:**
- Consumes: `tools/package.ps1` çıktısı `out/msix/layout` ve `<Version>` (Görev 3).
- Produces: `tools/test-package.ps1 -Adim <Hazirla|Otomatik|Elle|Temizle>`; kayıt ve ekran görüntüleri
  `out/package-test/<yyyyMMdd-HHmmss>/`. `Temizle` her zaman çalıştırılabilir ve tekrar edilebilir.

- [ ] **Adım 1: `test-package.ps1`'i yaz** — adımlar:
  - `Hazirla`: başlangıç durumunu `baslangic.json`'a yaz (görev çubuğu görünürlüğü, `ABM_GETSTATE`, Run değeri, veri
    klasörleri ve dosya sayıları); geliştirme kurulumunu `--restore-taskbar` ile kapat; `%LOCALAPPDATA%\CamAda` ve
    `%APPDATA%\CamAda`'yı `.yedek-paket` adına taşı (yedek zaten varsa dur).
  - `Otomatik` (her senaryodan sonra durum kaydı + ekran görüntüsü):
    1. `IlkAcilis`: kaydet, `shell:AppsFolder\<PFN>!CamAda`, 25 sn → beklenen: CamAda + bekçi çalışıyor, görev çubuğu
       **görünür**, gerçek Run değeri değişmemiş, başlangıç görevi `State=2`, ekranda izin penceresi yok.
    2. Uygulamayı kapat, paketin `LocalCache\Roaming\CamAda\settings.json`'ında `HideWindowsTaskbar: true` yap, aç → gizli.
    3. `Kurtarma`: `!Kurtar` → 10 sn → görünür, CamAda süreci yok.
    4. `Guncelleme`: aç (gizli); katmanı `out/package-test/<zaman>/layout-guncelleme`'ye kopyala, manifest sürümünü bir
       yama artır, `Add-AppxPackage -Register` → 25 sn → beklenen: görev çubuğu hiçbir kayıtta CamAda'sız gizli değil;
       CamAda yeni katmandan yeniden açılmış (yeniden açılmadıysa sonuç "yeniden açılmadı" olarak raporlanır — kapanış
       koşulu, spec §7.7).
    5. `GizliyKaldirma`: gizliyken `Remove-AppxPackage` → 10 sn → görünür, süreç yok, Run değeri değişmemiş, paket veri
       klasörü silinmiş.
  - `Elle`: paketi yeniden kaydedip açar ve bekler; kullanıcı hızlı ayarları ve Wi‑Fi listesini açar.
  - `Temizle`: kalan paketi kaldır, deneme süreçlerini durdur, veri klasörlerini geri koy (araya oluşan klasörü
    `.paket-artik-<saat>` adıyla kenara al), Run değerini ve otomatik gizleme durumunu `baslangic.json`'daki hâline getir,
    geliştirme kurulumunu başlat, son durumu kaydet.
- [ ] **Adım 2: Kullanıcıya sor, onay gelirse geliştirici modunu aç** — `Start-Process powershell -Verb RunAs -Wait ... tools/dev-mode.ps1 -On`.
- [ ] **Adım 3: `package.ps1` → `test-package.ps1 -Adim Hazirla`, `-Adim Otomatik`, `-Adim Temizle`** ve kaydı oku.
  Beklenen: beş senaryonun hepsi yukarıdaki sonuçlarla; `Temizle` sonrası dosya sayıları ve Run değeri başlangıçla aynı.
- [ ] **Adım 4: Elle deneme (kullanıcıyla)** — `-Adim Hazirla`, `-Adim Elle`; kullanıcı Wi‑Fi listesini açar → konum izni
  **şimdi** sorulur; izin verince liste gelir; reddedince listedeki "konum izni" uyarısı görünür. Kullanıcı ayrıca Bluetooth,
  ses ve parlaklık kaydırıcılarını, uygulama aramayı (son kullanılanlar dahil), panoyu, ekran görüntüsünü, takvimi ve medya
  kartını dener. Sonra `-Adim Temizle`.
- [ ] **Adım 5: Geliştirici modunu kapat** — `tools/dev-mode.ps1 -Off` (kullanıcı onayıyla); değerin silindiğini doğrula.
- [ ] **Adım 6: Sonuçları spec §8'e işaretle ve commit**

```bash
git add tools/dev-mode.ps1 tools/test-package.ps1 docs/superpowers/specs/2026-11-26-store-paketi-design.md
git commit -m "test: yerel paket denemesi betiği ve sonuçları"
```

---

### Görev 5: Geliştirme kurulumunu kaldırma betiği

**Files:**
- Create: `tools/uninstall-dev.ps1` (`[CmdletBinding(SupportsShouldProcess)]`)

**Interfaces:**
- Produces: `tools/uninstall-dev.ps1 [-WhatIf]` — Görev 6 geçişte kullanır.

- [ ] **Adım 1: Yaz** — sırası: `%LOCALAPPDATA%\Programs\CamAda\CamAda.exe --restore-taskbar` (`-Wait`); Run `CamAda` değeri
  yalnız `Programs\CamAda` yolunu gösteriyorsa silinir; masaüstündeki `Görev çubuğunu geri getir.lnk` silinir;
  `Programs\CamAda` silinir. `%LOCALAPPDATA%\CamAda`, `%APPDATA%\CamAda`, Belgeler ve Resimler'e **dokunulmaz** (betik başında
  yorum olarak yazılır).
- [ ] **Adım 2: Dene**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/uninstall-dev.ps1 -WhatIf`
Expected: dört "What if:" satırı (kurtarma, Run değeri, kısayol, klasör); hiçbir şey değişmemiş (Run değeri ve klasör yerinde).

- [ ] **Adım 3: Commit**

```bash
git add tools/uninstall-dev.ps1
git commit -m "build: geliştirme kurulumunu kaldırma betiği"
```

---

### Görev 6: Store'a gizli gönderim ve gerçek kurulum doğrulaması (kullanıcıyla, birkaç gün)

**Files:**
- Create: `packaging/store-identity.json`
- Create: `site/gizlilik.html` (geçici gizlilik sayfası)
- Modify: `docs/superpowers/specs/2026-11-26-store-paketi-design.md` (§8 sonuçları)

- [ ] **Adım 1: Kullanıcı Partner Center'da "Camada" adını ayırır** (Uygulamalar ve oyunlar → Yeni ürün → MSIX veya PWA
  uygulaması). Ad alınmışsa kullanıcıya yeni ad sorulur, spec §10 güncellenir.
- [ ] **Adım 2: Kimlik** — kullanıcı "Ürün kimliği" sayfasındaki `Package/Identity/Name`, `Package/Identity/Publisher`,
  `Package/Properties/PublisherDisplayName` değerlerini verir → `packaging/store-identity.json`; `tools/package.ps1` uyarısız
  çalışır.
- [ ] **Adım 3: Geçici gizlilik sayfası** — `site/gizlilik.html`, Türkçe + İngilizce, kısa ve dürüst: mikrofon yalnız Ada
  açıkken uyandırma sözcüğünü cihazda dinler; sözcükten sonra konuşma Google Gemini'ye gönderilir; Ada kapalıyken hiçbir ses
  gönderilmez; ayarlar, hafıza ve anahtarlar yalnız cihazda (DPAPI); hava için konum değil şehir adı kullanılır; iletişim
  adresi **kullanıcıya sorulur**. Yayın yeri kullanıcıya sorulur (önerilen: Vercel); yayın dış dünyaya açık olduğu için
  kullanıcı onayı olmadan yayımlanmaz.
- [ ] **Adım 4: Gönderim** — `tools/package.ps1`; kullanıcıyla Partner Center'da: fiyat ücretsiz, görünürlük **özel hedef
  kitle** (yalnız kullanıcının hesabı), kategori Kişiselleştirme, gizlilik adresi, en az bir ekran görüntüsü,
  `runFullTrust` gerekçesi ve sertifikasyon notu (spec §7.11 metinleri), paket `out/msix/Camada_1.0.0_x64.msix`.
- [ ] **Adım 5: Sertifikasyon sonucu** — geçerse Adım 6; reddedilirse nedeni spec'e yazılır, düzeltilir, sürüm bir yama
  artırılıp yeniden gönderilir.
- [ ] **Adım 6: Geçiş** — `tools/uninstall-dev.ps1 -WhatIf`, kullanıcı onayıyla gerçek çalıştırma; Store bağlantısından kurulum.
- [ ] **Adım 7: Gerçek kurulum doğrulaması** (spec §8 "Gerçek Store kurulumu" listesi): kurulum uyarısız; bekçi `WindowsApps`
  altından çalışıyor (`Win32_Process` komut satırı); oturum açılışında başlıyor; menüden başlangıç kapat/aç; gizliyken
  Store'dan kaldır → görev çubuğu geri; yeniden kur; mevcut ayarlar, Ada hafızası ve anahtar görünüyor mu (görünmüyorsa
  tek seferlik içe aktarma görevi açılır — spec §7.10); Ada elle (kullanıcı): ilk mikrofon izni, "Hey Ada", bir komut.
  Review Focus 5: geliştirme sürümü kuruluyken Store sürümünü aç → ikincisi sessizce kapanır; kurtarma girişi ikisini de kapatır.
- [ ] **Adım 7b: Store güncellemesi** — `<Version>` `1.0.1` yapılır, paketlenip aynı gizli kitleye gönderilir. Güncelleme
  gelince (görev çubuğu gizliyken): görev çubuğu CamAda'sız gizli kalmaz, CamAda yeniden açılır (spec §7.7 kapanış koşulu).
- [ ] **Adım 8: Sonuçları spec §8'e yaz, commit, hafızayı güncelle**

```bash
git add packaging/store-identity.json site/gizlilik.html docs/superpowers/specs/2026-11-26-store-paketi-design.md
git commit -m "docs: Store gizli yayın doğrulaması"
```
