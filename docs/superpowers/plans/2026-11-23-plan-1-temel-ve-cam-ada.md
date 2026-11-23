# Plan 1: Temel, Güvenlik Ağı ve Cam Ada — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ana ekranın üstünde 56 px'lik ayrılmış şerit içinde, fareyle genişleyip küçülen cam bir ada gösteren; Windows görev çubuğunu güvenle gizleyip her durumda geri getirebilen çalışan bir uygulama.

**Architecture:** İki proje: saf mantık `CamAda.Core` (birim testli) ve WPF uygulaması `CamAda` (Windows entegrasyonu ve arayüz). ManagedShell'in `AppBarWindow`'u görünmez bir şerit olarak alanı ayırır ve tam ekranı algılar; ada ayrı, saydam ve her zaman üstte duran bir penceredir. Aynı exe `--watchdog` ve `--restore-taskbar` modlarıyla güvenlik ağı görevi görür.

**Tech Stack:** .NET 10 (LTS, SDK 10.0.4xx), C#, WPF, ManagedShell 0.0.374 (Apache-2.0), xUnit, Microsoft.Extensions.TimeProvider.Testing, System.Text.Json.

**Spec:** `docs/superpowers/specs/2026-11-23-cam-ada-design.md`

## Yol haritası (bu plan 1/4)

1. **Plan 1 (bu belge):** Ortam, ayarlar, görev çubuğu güvenlik ağı, üst şerit, cam ada (küçük hâl + fareyle genişleme), cam görünümü denemesi.
2. **Plan 2: Görev çubuğu işlevleri.** Sabitlenmiş uygulamalar, açık pencereler ve önizlemeler, arka plan simgeleri (tepsi), Başlat ve arama paneli, güç işlemleri, oturum açılışında başlama. Bu plan bitince görev çubuğu varsayılan olarak gizlenir.
3. **Plan 3: Medya ve plak çalar.** Windows medya oturumu, adada müzik, Müzik paneli, gerçekçi plak çalar, Spotify Web API (beğen, sıradaki).
4. **Plan 4: Sistem panelleri.** Hızlı ayarlar, Ses ve mikrofon (Ctrl+Shift+M), Pano ve ekran görüntüleri (Ctrl+Shift+V), Takvim, Bildirimler (seyrek MSIX paketi), kendiliğinden kısa genişlemeler.

Plan 2–4, Plan 1'in cam görünümü kararı (Görev 7) netleşince yazılır.

## Global Constraints

- Hedef: Windows 11 Pro, build 26300; ana ekran şu an 1536×864 DIP (%125 ölçek). Kod DPI farkında olmalı (PerMonitorV2).
- `CamAda.Core`: `net10.0`, Windows ya da WPF tipi kullanmaz. `CamAda`: `net10.0-windows10.0.22621.0`, `UseWPF`.
- ManagedShell sürümü tam olarak `0.0.374`. Explorer'a kod enjekte eden hiçbir yöntem kullanılmaz.
- Üst şerit: yalnızca ana ekranda, üst kenarda, yükseklik **56** DIP.
- Ada (küçük): **400×40** DIP, köşe yarıçapı 20, şeridin üstünden **8** DIP aşağıda, yatayda ortalı.
- Ada (genişlemiş): **770×46** DIP, köşe yarıçapı 23.
- Fareyle açılma gecikmesi **120 ms**, kapanma gecikmesi **350 ms**; panel açıkken ada büyük kalır.
- Genişleme animasyonu ~**0,4 sn**, hafif esneyen (aşan) yumuşatma; küçük içerik 0,16 sn'de söner, geniş içerik 0,22 sn'de (0,06 sn gecikmeyle) belirir.
- Cam renkleri: ada tonu rgba(20,24,42,0.46) → WPF `#7514182A`; kenar `#2BFFFFFF`; üst parıltı `#33FFFFFF`; vurgu `#FFC27A`, vurgu üstü metin `#2A1A08`; metin `#F4F6FB`, ikincil metin `#BDE8ECF7`.
- Yazı tipi: Manrope (OFL), 400/600/700/800 statik TTF olarak uygulamayla birlikte.
- Arayüz metinleri Türkçe; tarih/saat `tr-TR`.
- "Animasyonları azalt" (Windows'ta `SystemParameters.ClientAreaAnimation == false`) açıkken animasyon süreleri 0.
- Her durumda Windows görev çubuğu geri gelebilmeli: normal çıkış, çökme, süreç öldürme, oturum kapatma.
- Commit mesajları şu satırla biter: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`

## Review Focus

1. **Elle düzenlenmiş ya da bozuk `settings.json`** → uygulama çökmeden varsayılanlarla açılır ve bozuk dosyayı `settings.corrupt.json` olarak saklar. (Görev 2 testi)
2. **Explorer yeniden başlarsa** (`explorer.exe` çöker ya da yeniden başlatılır) → görev çubuğu gizliyse birkaç saniye içinde yeniden gizlenir, şerit hâlâ alan ayırır. (Görev 4 elle kontrol adımı)
3. **Duvar kağıdı değişirse** (slayt gösterisi, Spotlight, elle değişim) → cam en geç ~2 sn içinde yeni duvar kağıdını gösterir. (Görev 7 elle kontrol adımı)
4. **Ölçek/çözünürlük değişirse ya da monitör takılıp çıkarılırsa** → şerit ana ekranda kalır, ada ana ekranda ortalı kalır. (Görev 5 elle kontrol adımı)
5. **Uyku/hazırda bekleme sonrası** → saat uyanınca hemen doğru dakikayı gösterir, kaymaz. (Görev 5 testi: `DelayToNextMinute`, uyanma olayında yenileme)

---

## Dosya yapısı

```
CamAda.slnx
src/CamAda.Core/
  Startup/StartupMode.cs
  Settings/AppSettings.cs, Settings/SettingsStore.cs
  Safety/TaskbarSafetyState.cs, Safety/TaskbarSafetyStore.cs, Safety/WatchdogDecision.cs
  Island/IslandLayout.cs, Island/HoverIntent.cs
  Time/ClockFormatter.cs, Time/MinuteTicker.cs
  Glass/Geometry.cs, Glass/WallpaperStyle.cs, Glass/WallpaperLayout.cs, Glass/GlassImageProcessor.cs
src/CamAda/
  App.xaml, App.xaml.cs, app.manifest
  Assets/Fonts/Manrope-{Regular,SemiBold,Bold,ExtraBold}.ttf, Assets/Fonts/OFL.txt
  Theme/Glass.xaml
  Shell/ShellHost.cs, Shell/StripAppBar.xaml(.cs)
  Safety/TaskbarGuard.cs, Safety/WatchdogRunner.cs, Safety/RestoreRunner.cs
  Island/IslandWindow.xaml(.cs), Island/HalfHeightRadiusConverter.cs
  Glass/WallpaperSource.cs, Glass/WallpaperGlass.cs, Glass/AcrylicGlass.cs
  Interop/Native.cs
tests/CamAda.Core.Tests/  (her Core dosyası için *Tests.cs)
tools/fetch-fonts.py, tools/create-restore-shortcut.ps1
```

---

### Task 1: Ortam, proje iskeleti ve başlangıç modları

**Files:**
- Create: `CamAda.slnx`, `src/CamAda.Core/CamAda.Core.csproj`, `src/CamAda/CamAda.csproj`, `tests/CamAda.Core.Tests/CamAda.Core.Tests.csproj`, `.gitignore`
- Create: `src/CamAda.Core/Startup/StartupMode.cs`
- Test: `tests/CamAda.Core.Tests/Startup/StartupModeTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace CamAda.Core.Startup;
  public abstract record StartupMode
  {
      public sealed record Normal : StartupMode;
      public sealed record Watchdog(int ParentProcessId) : StartupMode;
      public sealed record RestoreTaskbar : StartupMode;
      public static StartupMode Parse(IReadOnlyList<string> args);
  }
  ```

- [ ] **Step 1: .NET 10 SDK'yı kur (kullanıcı onayıyla)**

Run: `winget install --id Microsoft.DotNet.SDK.10 --exact --source winget --accept-package-agreements --accept-source-agreements`
Ardından yeni bir kabukta: `dotnet --version`
Expected: `10.0.` ile başlayan sürüm. (PATH yenilenmediyse `"C:\Program Files\dotnet\dotnet.exe"` kullan.)

- [ ] **Step 2: Git deposu ve çözüm iskeleti**

Proje kökünde (`C:\Users\<kullanıcı>\Desktop\windows_customize`):
```bash
git init -b main
dotnet new gitignore
dotnet new sln -n CamAda
dotnet new classlib -n CamAda.Core -o src/CamAda.Core -f net10.0
dotnet new wpf -n CamAda -o src/CamAda -f net10.0
dotnet new xunit -n CamAda.Core.Tests -o tests/CamAda.Core.Tests -f net10.0
dotnet sln add src/CamAda.Core src/CamAda tests/CamAda.Core.Tests
dotnet add src/CamAda reference src/CamAda.Core
dotnet add tests/CamAda.Core.Tests reference src/CamAda.Core
dotnet add src/CamAda package ManagedShell --version 0.0.374
dotnet add tests/CamAda.Core.Tests package Microsoft.Extensions.TimeProvider.Testing
```
Şablonun `Class1.cs` ve `UnitTest1.cs` dosyalarını sil. `src/CamAda/CamAda.csproj` içinde `<TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<ApplicationManifest>app.manifest</ApplicationManifest>` olsun. `app.manifest` içinde `<dpiAwareness>PerMonitorV2</dpiAwareness>` ve Windows 10/11 `supportedOS` kimliği bulunsun.

Run: `dotnet build`
Expected: `Build succeeded` ve 0 hata.

- [ ] **Step 3: Başarısız testleri yaz**

```csharp
using CamAda.Core.Startup;
namespace CamAda.Core.Tests.Startup;

public class StartupModeTests
{
    [Fact]
    public void No_arguments_means_normal() =>
        Assert.IsType<StartupMode.Normal>(StartupMode.Parse([]));

    [Fact]
    public void Watchdog_with_parent_pid() =>
        Assert.Equal(new StartupMode.Watchdog(4242), StartupMode.Parse(["--watchdog", "4242"]));

    [Fact]
    public void Restore_taskbar_flag() =>
        Assert.IsType<StartupMode.RestoreTaskbar>(StartupMode.Parse(["--restore-taskbar"]));

    [Theory]
    [InlineData(new object[] { new[] { "--watchdog" } })]
    [InlineData(new object[] { new[] { "--watchdog", "abc" } })]
    public void Broken_watchdog_arguments_fail_safe_to_restore(string[] args) =>
        Assert.IsType<StartupMode.RestoreTaskbar>(StartupMode.Parse(args));

    [Fact]
    public void Unknown_arguments_are_ignored() =>
        Assert.IsType<StartupMode.Normal>(StartupMode.Parse(["--bilinmeyen"]));
}
```

- [ ] **Step 4: Testlerin başarısız olduğunu gör**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~StartupModeTests"`
Expected: Derleme hatası (`StartupMode` tanımsız).

- [ ] **Step 5: `StartupMode.Parse` uygula** (`src/CamAda.Core/Startup/StartupMode.cs`)

Bozuk `--watchdog` argümanı güvenli tarafta kalır: `RestoreTaskbar`.

- [ ] **Step 6: Testler geçsin**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~StartupModeTests"`
Expected: 6 test PASS.

- [ ] **Step 7: Commit** (spec ve bu plan da ilk commit'e girer)

```bash
git add .
git commit -m "chore: proje iskeleti ve başlangıç modları" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Ayarlar dosyası

**Files:**
- Create: `src/CamAda.Core/Settings/AppSettings.cs`, `src/CamAda.Core/Settings/SettingsStore.cs`
- Test: `tests/CamAda.Core.Tests/Settings/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace CamAda.Core.Settings;
  public enum GlassMode { Wallpaper, Acrylic, Solid }
  public sealed record AppSettings
  {
      public bool HideWindowsTaskbar { get; init; } = false; // Plan 2 bitene kadar varsayılan kapalı
      public GlassMode GlassMode { get; init; } = GlassMode.Wallpaper;
      public double StripHeight { get; init; } = 56;
      public int HoverEnterDelayMs { get; init; } = 120;
      public int HoverLeaveDelayMs { get; init; } = 350;
  }
  public sealed class SettingsStore
  {
      public SettingsStore(string directory);            // ör. %APPDATA%\CamAda
      public string FilePath { get; }                    // <directory>\settings.json
      public AppSettings Load();                         // yoksa varsayılanı yazar ve döner
      public void Save(AppSettings settings);
  }
  ```

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
using CamAda.Core.Settings;
namespace CamAda.Core.Tests.Settings;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("camada-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Missing_file_returns_defaults_and_writes_file()
    {
        var store = new SettingsStore(_dir);
        var s = store.Load();
        Assert.False(s.HideWindowsTaskbar);
        Assert.Equal(GlassMode.Wallpaper, s.GlassMode);
        Assert.Equal(56, s.StripHeight);
        Assert.Equal(120, s.HoverEnterDelayMs);
        Assert.Equal(350, s.HoverLeaveDelayMs);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var store = new SettingsStore(_dir);
        store.Save(new AppSettings { HideWindowsTaskbar = true, GlassMode = GlassMode.Acrylic });
        var s = store.Load();
        Assert.True(s.HideWindowsTaskbar);
        Assert.Equal(GlassMode.Acrylic, s.GlassMode);
    }

    [Fact]
    public void Corrupt_file_returns_defaults_and_keeps_backup()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ bozuk");
        var s = new SettingsStore(_dir).Load();
        Assert.Equal(new AppSettings(), s);
        Assert.Equal("{ bozuk", File.ReadAllText(Path.Combine(_dir, "settings.corrupt.json")));
    }

    [Fact]
    public void Unknown_properties_ignored_and_missing_ones_default()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "GlassMode": "Solid", "Gelecek": 1 }""");
        var s = new SettingsStore(_dir).Load();
        Assert.Equal(GlassMode.Solid, s.GlassMode);
        Assert.Equal(56, s.StripHeight);
    }
}
```

- [ ] **Step 2: Testlerin başarısız olduğunu gör**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~SettingsStoreTests"`
Expected: Derleme hatası.

- [ ] **Step 3: `AppSettings` ve `SettingsStore` uygula**

`System.Text.Json`: enum'lar metin olarak (`JsonStringEnumConverter`), girintili yazım, büyük/küçük harf duyarsız okuma. Bozuk JSON'da dosya `settings.corrupt.json` olarak kopyalanır (varsa üzerine yazılır), varsayılanlar döner ve `settings.json` varsayılanlarla yeniden yazılır.

- [ ] **Step 4: Testler geçsin**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~SettingsStoreTests"`
Expected: 4 test PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CamAda.Core/Settings tests/CamAda.Core.Tests/Settings
git commit -m "feat: JSON ayar dosyası" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Görev çubuğu güvenlik ağı

**Files:**
- Create: `src/CamAda.Core/Safety/TaskbarSafetyState.cs`, `TaskbarSafetyStore.cs`, `WatchdogDecision.cs`
- Create: `src/CamAda/Safety/TaskbarGuard.cs`, `WatchdogRunner.cs`, `RestoreRunner.cs`, `src/CamAda/Shell/ShellHost.cs`
- Modify: `src/CamAda/App.xaml` (`StartupUri` kaldırılır), `src/CamAda/App.xaml.cs`
- Create: `tools/create-restore-shortcut.ps1`
- Test: `tests/CamAda.Core.Tests/Safety/TaskbarSafetyTests.cs`

**Interfaces:**
- Consumes: `StartupMode.Parse` (Task 1), `SettingsStore` (Task 2).
- Produces:
  ```csharp
  namespace CamAda.Core.Safety;
  public sealed record TaskbarSafetyState(bool HiddenByUs, bool CleanShutdown, int OriginalTaskbarState);
  public sealed class TaskbarSafetyStore
  {
      public const int DefaultTaskbarState = 2;                // ABS_ALWAYSONTOP: otomatik gizleme kapalı
      public TaskbarSafetyStore(string filePath);              // %LOCALAPPDATA%\CamAda\taskbar-state.json
      public TaskbarSafetyState? Load();                       // yoksa null; bozuksa (true, false, 2)
      public void Save(TaskbarSafetyState state);
  }
  public static class WatchdogDecision
  {
      public static bool ShouldRestore(TaskbarSafetyState? state); // HiddenByUs && !CleanShutdown
  }

  namespace CamAda.Shell;
  public sealed class ShellHost : IDisposable
  {
      public ShellHost();                     // ShellManager: EnableTasksService=true, EnableTrayService=false
      public ManagedShell.ShellManager Manager { get; }
      public void Shutdown();                 // AppBarManager.SignalGracefulShutdown + Dispose
  }

  namespace CamAda.Safety;
  public sealed class TaskbarGuard
  {
      public TaskbarGuard(ManagedShell.AppBar.ExplorerHelper explorer, TaskbarSafetyStore store);
      public void Hide();                     // durumu kaydet (OriginalTaskbarState=GetTaskbarState), sonra HideExplorerTaskbar=true
      public void ShowAndMarkClean();         // HideExplorerTaskbar=false, Save(false, true, original)
  }
  public static class WatchdogRunner { public static Task RunAsync(int parentPid, TaskbarSafetyStore store); }
  public static class RestoreRunner { public static void Run(TaskbarSafetyStore store); }
  ```

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
using CamAda.Core.Safety;
namespace CamAda.Core.Tests.Safety;

public class TaskbarSafetyTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("camada-").FullName;
    private string FilePath => Path.Combine(_dir, "taskbar-state.json");
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void Restore_only_when_hidden_and_not_clean(bool hidden, bool clean, bool expected) =>
        Assert.Equal(expected, WatchdogDecision.ShouldRestore(new TaskbarSafetyState(hidden, clean, 2)));

    [Fact]
    public void No_state_means_no_restore() =>
        Assert.False(WatchdogDecision.ShouldRestore(null));

    [Fact]
    public void Missing_file_loads_null() =>
        Assert.Null(new TaskbarSafetyStore(FilePath).Load());

    [Fact]
    public void Save_then_load_round_trips()
    {
        var store = new TaskbarSafetyStore(FilePath);
        store.Save(new TaskbarSafetyState(true, false, 1));
        Assert.Equal(new TaskbarSafetyState(true, false, 1), store.Load());
    }

    [Fact]
    public void Corrupt_file_fails_safe_to_restore()
    {
        File.WriteAllText(FilePath, "xx");
        var state = new TaskbarSafetyStore(FilePath).Load();
        Assert.Equal(new TaskbarSafetyState(true, false, TaskbarSafetyStore.DefaultTaskbarState), state);
        Assert.True(WatchdogDecision.ShouldRestore(state));
    }
}
```

- [ ] **Step 2: Testlerin başarısız olduğunu gör**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~TaskbarSafetyTests"`
Expected: Derleme hatası.

- [ ] **Step 3: Core sınıflarını uygula, testler geçsin**

`Save` önce geçici dosyaya yazıp `File.Move(..., overwrite: true)` ile değiştirir (yarım yazılmış dosya kalmasın).
Run: aynı komut. Expected: 8 test PASS.

- [ ] **Step 4: `ShellHost`, `TaskbarGuard`, `WatchdogRunner`, `RestoreRunner` ve `App` başlangıcını uygula**

- `App.OnStartup`: `StartupMode.Parse(e.Args)` ile dallanır.
  - `Normal`: `Local\CamAda.Main` adlı mutex alınamazsa hemen çıkar. Önce kendi kendini iyileştirir: `WatchdogDecision.ShouldRestore(store.Load())` true ise `RestoreRunner` mantığıyla görev çubuğunu geri getirir. Sonra `Process.Start(Environment.ProcessPath!, $"--watchdog {Environment.ProcessId}")` ile bekçiyi başlatır. Ayarlarda `HideWindowsTaskbar` true ise `TaskbarGuard.Hide()` çağrılır.
  - `Watchdog(pid)`: pencere açmaz. `Process.GetProcessById(pid).WaitForExitAsync()` bekler (süreç yoksa hemen devam eder). `ShouldRestore` true ise UI iş parçacığında `new ExplorerHelper()` oluşturup `SetTaskbarState((NativeMethods.ABState)original)` ve `SetTaskbarVisibility((int)NativeMethods.SetWindowPosFlags.SWP_SHOWWINDOW)` çağırır (`ManagedShell.Interop.NativeMethods`). `TaskbarGuard.Hide` da `OriginalTaskbarState` için `(int)explorer.GetTaskbarState()` kaydeder. Ardından `Save(false, true, original)` ve çıkış.
  - `RestoreTaskbar`: kendi dışındaki tüm `CamAda` süreçlerini sonlandırır, görev çubuğunu bekçideki gibi geri getirir, durumu temiz olarak kaydeder ve çıkar.
- Normal çıkış yolu (Task 5'teki "Çıkış" menüsü), `SessionEnding` ve `Exit`: `TaskbarGuard.ShowAndMarkClean()`, ardından `ShellHost.Shutdown()`.
- `DispatcherUnhandledException` ve `AppDomain.UnhandledException`: en iyi çabayla `ShowAndMarkClean()`. Bekçi zaten yedek.
- `tools/create-restore-shortcut.ps1`: masaüstüne `Görev çubuğunu geri getir.lnk` oluşturur; hedef `CamAda.exe`, argüman `--restore-taskbar` (exe yolu parametre olarak verilir, `WScript.Shell` COM ile).

Run: `dotnet build`
Expected: 0 hata.

- [ ] **Step 5: Elle doğrula (gerçek Windows'ta)**

1. `%APPDATA%\CamAda\settings.json` içinde `"HideWindowsTaskbar": true` yap, `dotnet run --project src/CamAda` → görev çubuğu kaybolur.
2. Görev Yöneticisi'nden ana `CamAda` sürecini sonlandır → görev çubuğu ~2 sn içinde geri gelir; `taskbar-state.json` içinde `CleanShutdown: true`.
3. Tekrar başlat, sonra `tools/create-restore-shortcut.ps1 -ExePath <bin yolu>\CamAda.exe` ile oluşan kısayola çift tıkla → uygulama kapanır, görev çubuğu geri gelir.
4. Ana süreci ve bekçiyi aynı anda sonlandır, uygulamayı yeniden başlat → başlarken görev çubuğu önce geri getirilir (kendi kendini iyileştirme), sonra ayara göre yeniden gizlenir.
5. Ayarı `false` yap.
Expected: Her adımda görev çubuğu sonunda görünür ve otomatik gizleme ayarı ilk hâlinde.

- [ ] **Step 6: Commit**

```bash
git add src tests tools
git commit -m "feat: görev çubuğu güvenlik ağı (bekçi, kurtarma kısayolu)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Üst şerit (AppBar) ve tam ekran algılama

**Files:**
- Create: `src/CamAda/Shell/StripAppBar.xaml`, `src/CamAda/Shell/StripAppBar.xaml.cs`
- Modify: `src/CamAda/App.xaml.cs`

**Interfaces:**
- Consumes: `ShellHost.Manager` (Task 3), `AppSettings.StripHeight` (Task 2).
- Produces:
  ```csharp
  namespace CamAda.Shell;
  public partial class StripAppBar : ManagedShell.AppBar.AppBarWindow
  {
      public StripAppBar(ShellHost host, double height);
      // base(host.Manager.AppBarManager, host.Manager.ExplorerHelper, host.Manager.FullScreenHelper,
      //      AppBarScreen.FromPrimaryScreen(), AppBarEdge.Top, AppBarMode.Normal, height)
      public bool IsFullScreenActive { get; }
      public event EventHandler<bool>? FullScreenChanged;   // OnFullScreenEnter/Leave override'larından
  }
  ```

- [ ] **Step 1: `StripAppBar` uygula**

Pencere boş ve saydam: `AllowsTransparency=True`, `Background=Transparent`, içerik yok. Çıkışta `AllowClose` ManagedShell'in kapatma sinyaliyle gelir; `CustomClosing` görev çubuğuna dokunmaz (bu iş `TaskbarGuard`'ın). `OnFullScreenEnter` ve `OnFullScreenLeave` önce `base` çağrısını yapar, sonra olayı tetikler. `App` normal modda şeridi gösterir.

- [ ] **Step 2: Elle doğrula**

1. `dotnet run --project src/CamAda`, bir pencereyi ekranı kaplayacak şekilde büyüt → üst kenarı ekranın 56 DIP aşağısından başlar (%125'te 70 piksel).
2. Chrome'da F11 ya da bir videoyu tam ekran yap → olay `true` gelir (Debug çıktısında görünür); çıkınca `false`.
3. Ayarda görev çubuğu gizliyken PowerShell'de `Stop-Process -Name explorer` → Explorer yeniden başlar; en geç ~5 sn içinde görev çubuğu yeniden gizlenir, büyütülmüş pencereler hâlâ 56 DIP aşağıdan başlar.
4. Görev çubuğu gizliyken Windows tuşuna bas ve bir uygulamadan bildirim gelmesini sağla → Başlat menüsünün ve bildirim baloncuğunun ekranın neresinde açıldığını not et; spec §9 risk 2'ye bir satır olarak yaz (Plan 2 bu bilgiyle yazılacak).
5. Uygulamayı kapat → çalışma alanı eski hâline döner.
Expected: Beş madde de gözlenir; §9 notu eklenmiş.

- [ ] **Step 3: Commit**

```bash
git add src/CamAda
git commit -m "feat: ana ekranda 56 DIP üst şerit ve tam ekran algılama" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Cam ada penceresi, küçük hâl, saat ve yazı tipi

**Files:**
- Create: `src/CamAda.Core/Island/IslandLayout.cs`, `src/CamAda.Core/Time/ClockFormatter.cs`, `src/CamAda.Core/Time/MinuteTicker.cs`
- Create: `src/CamAda/Island/IslandWindow.xaml(.cs)`, `src/CamAda/Island/HalfHeightRadiusConverter.cs`, `src/CamAda/Theme/Glass.xaml`, `src/CamAda/Interop/Native.cs`
- Create: `tools/fetch-fonts.py`, `src/CamAda/Assets/Fonts/*`
- Test: `tests/CamAda.Core.Tests/Time/ClockTests.cs`

**Interfaces:**
- Consumes: `StripAppBar.FullScreenChanged` (Task 4), `TaskbarGuard.ShowAndMarkClean`, `ShellHost.Shutdown` (Task 3).
- Produces:
  ```csharp
  namespace CamAda.Core.Island;
  public readonly record struct PillSize(double Width, double Height);
  public static class IslandLayout
  {
      public static readonly PillSize Compact = new(400, 40);
      public static readonly PillSize Expanded = new(770, 46);
      public const double TopMargin = 8;
      public const double WindowWidth = 900;   // ada penceresi; gölge ve ileride kısa genişlemeler için pay
      public const double WindowHeight = 160;
  }
  namespace CamAda.Core.Time;
  public static class ClockFormatter
  {
      public static string Time(DateTime local);   // "HH:mm"
      public static string Date(DateTime local);   // tr-TR "ddd, d MMMM"
  }
  public static class MinuteTicker
  {
      public static TimeSpan DelayToNextMinute(DateTime local);
  }
  namespace CamAda.Island;
  public sealed record IslandCommands(
      Func<bool> IsTaskbarHidden,
      Action<bool> SetTaskbarHidden,   // ayarı kaydeder, TaskbarGuard.Hide / ShowAndMarkClean çağırır
      Action Exit);                    // ShowAndMarkClean + ShellHost.Shutdown + Application.Shutdown
                                       // Task 7 iki alan ekler: Func<GlassMode> GetGlassMode, Action<GlassMode> SetGlassMode
  public partial class IslandWindow : Window
  {
      public IslandWindow(StripAppBar strip, AppSettings settings, IslandCommands commands);
      public Border Pill { get; }      // Task 6 ve 7 bunu kullanır
  }
  ```

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
using CamAda.Core.Time;
namespace CamAda.Core.Tests.Time;

public class ClockTests
{
    [Fact] public void Time_is_24h_with_leading_zero() =>
        Assert.Equal("09:05", ClockFormatter.Time(new DateTime(2026, 11, 23, 9, 5, 0)));

    [Fact] public void Date_is_turkish_short_day_and_month() =>
        Assert.Equal("Pzt, 23 Kasım", ClockFormatter.Date(new DateTime(2026, 11, 23, 14, 32, 0)));

    [Fact] public void Delay_to_next_minute_mid_minute() =>
        Assert.Equal(TimeSpan.FromSeconds(39.5),
            MinuteTicker.DelayToNextMinute(new DateTime(2026, 11, 23, 14, 32, 20, 500)));

    [Fact] public void Delay_on_exact_minute_is_full_minute() =>
        Assert.Equal(TimeSpan.FromMinutes(1),
            MinuteTicker.DelayToNextMinute(new DateTime(2026, 11, 23, 14, 33, 0)));
}
```

- [ ] **Step 2: Testlerin başarısız olduğunu gör**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~ClockTests"`
Expected: Derleme hatası.

- [ ] **Step 3: `IslandLayout`, `ClockFormatter`, `MinuteTicker` uygula, testler geçsin**

Run: aynı komut. Expected: 4 test PASS.

- [ ] **Step 4: Manrope'u indir**

`tools/fetch-fonts.py` (Python, `fontTools`): 400/600/700/800 ağırlıklarının her biri için `https://fonts.googleapis.com/css2?family=Manrope:wght@<w>` adresini şu User-Agent ile ister: `Mozilla/5.0 (Windows NT 6.1) AppleWebKit/534.30 (KHTML, like Gecko) Chrome/12.0.742.122 Safari/534.30`. Dönen CSS'teki `.woff` adresini indirir, `TTFont(...).flavor = None` ile TTF'ye çevirir, `Manrope-{Regular,SemiBold,Bold,ExtraBold}.ttf` olarak `src/CamAda/Assets/Fonts/` altına yazar. Her dosyada `şŞğĞıİçÇöÖüÜ` karakterlerinin bulunduğunu doğrular, yoksa hata verir. `OFL.txt` dosyasını `https://raw.githubusercontent.com/google/fonts/main/ofl/manrope/OFL.txt` adresinden indirir.
Run: `python -I tools/fetch-fonts.py`
Expected: 4 TTF + OFL.txt, "Türkçe karakterler tamam" çıktısı.
Fontlar `CamAda.csproj`'a `<Resource>` olarak eklenir; `Glass.xaml` içinde `FontFamily="pack://application:,,,/Assets/Fonts/#Manrope"`.

- [ ] **Step 5: `IslandWindow` ve küçük hâli uygula**

- Pencere: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`, `Topmost=True`, `ShowInTaskbar=False`, `ResizeMode=NoResize`, boyut `IslandLayout.WindowWidth × WindowHeight`. `SourceInitialized`'da `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE` eklenir (`Native.cs`: `GetWindowLongPtr`/`SetWindowLongPtr`). Ada, tıklanınca odağı başka pencereden çalmaz.
- Konum: ana ekranın sınırları (`AppBarScreen.FromPrimaryScreen().Bounds`, piksel) pencerenin DPI ölçeğine bölünerek DIP'e çevrilir; `Left = ekranSol + (ekranGenişlik - WindowWidth) / 2`, `Top = ekranÜst`. `DisplaySettingsChanged` ve `DpiChanged`'de yeniden hesaplanır.
- `Pill` bir `Border`: ilk boyutu `Compact`, üstte `TopMargin`, yatayda ortalı. `CornerRadius`, `HalfHeightRadiusConverter` ile her zaman yüksekliğin yarısı. Ton `#7514182A`, kenar 1 DIP `#2BFFFFFF`, içte üst kenarda 1 DIP `#33FFFFFF` çizgi, gölge `DropShadowEffect` (BlurRadius 30, ShadowDepth 8, yön 270, renk `#04061A`, opaklık 0.45).
- Küçük içerik (Plan 1'de müzik ve bildirim olmadığı için "Boşta" hâli): solda tarih (`ClockFormatter.Date`, 12,5 DIP, SemiBold, `#D1E8ECF7`), sağda saat (`ClockFormatter.Time`, 13 DIP, ExtraBold, tablo rakamları). Yenileme `MinuteTicker.DelayToNextMinute` ile kurulan tek seferlik `DispatcherTimer` ile; her tetiklemede yeniden kurulur. `SystemEvents.PowerModeChanged` (Resume) ve `TimeChanged` olaylarında hemen yenilenir.
- Sağ tık menüsü (cam görünümlü `ContextMenu`): "Görev çubuğunu gizle" (işaret durumu `IsTaskbarHidden`, tıklayınca `SetTaskbarHidden`), "Çıkış" (`Exit`).
- `StripAppBar.FullScreenChanged(true)` → ada `Hide()`, `false` → `Show()`.

- [ ] **Step 6: Elle doğrula**

1. Çalıştır → üst ortada 400×40 cam hap, solda "Pzt, 23 Kasım" biçiminde tarih, sağda saat; dakika değişince saat 1 sn içinde güncellenir.
2. Adanın dışındaki saydam alana tıklayınca alttaki pencere tıklanır (tıklama geçer).
3. Ada Alt+Tab listesinde görünmez; tıklanınca o an önde olan pencere odağını kaybetmez.
4. Windows ölçeğini %125 → %100 yap (ya da ikinci monitör tak/çıkar) → ada ana ekranda ortalı kalır.
5. Bilgisayarı uykuya alıp uyandır → saat hemen doğru.
6. Sağ tık → "Çıkış" → ada ve şerit kapanır, görev çubuğu (gizliyse) geri gelir.
Expected: Altısı da gözlenir.

- [ ] **Step 7: Commit**

```bash
git add src tests tools
git commit -m "feat: cam ada penceresi, küçük hâl, Türkçe saat ve Manrope" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Fareyle genişleme ve küçülme

**Files:**
- Create: `src/CamAda.Core/Island/HoverIntent.cs`
- Modify: `src/CamAda/Island/IslandWindow.xaml(.cs)`
- Test: `tests/CamAda.Core.Tests/Island/HoverIntentTests.cs`

**Interfaces:**
- Consumes: `IslandLayout` (Task 5), `AppSettings.HoverEnterDelayMs/HoverLeaveDelayMs` (Task 2).
- Produces:
  ```csharp
  namespace CamAda.Core.Island;
  public sealed class HoverIntent : IDisposable
  {
      public HoverIntent(TimeProvider time, TimeSpan enterDelay, TimeSpan leaveDelay);
      public bool IsExpanded { get; }
      public event EventHandler<bool>? ExpandedChanged;  // zamanlayıcı iş parçacığından gelebilir
      public void PointerEntered();
      public void PointerExited();
      public void SetPanelOpen(bool open);  // açıkken küçülmez; kapanırken imleç dışarıdaysa hemen küçülür
  }
  ```

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
using CamAda.Core.Island;
using Microsoft.Extensions.Time.Testing;
namespace CamAda.Core.Tests.Island;

public class HoverIntentTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly List<bool> _events = new();
    private HoverIntent Make()
    {
        var h = new HoverIntent(_time, TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(350));
        h.ExpandedChanged += (_, v) => _events.Add(v);
        return h;
    }
    private void Ms(int ms) => _time.Advance(TimeSpan.FromMilliseconds(ms));

    [Fact]
    public void Expands_exactly_after_enter_delay()
    {
        var h = Make(); h.PointerEntered();
        Ms(119); Assert.False(h.IsExpanded);
        Ms(1);   Assert.True(h.IsExpanded);
        Assert.Equal(new[] { true }, _events);
    }

    [Fact]
    public void Leaving_before_enter_delay_cancels()
    {
        var h = Make(); h.PointerEntered(); Ms(80); h.PointerExited(); Ms(1000);
        Assert.False(h.IsExpanded); Assert.Empty(_events);
    }

    [Fact]
    public void Collapses_exactly_after_leave_delay()
    {
        var h = Make(); h.PointerEntered(); Ms(120); h.PointerExited();
        Ms(349); Assert.True(h.IsExpanded);
        Ms(1);   Assert.False(h.IsExpanded);
        Assert.Equal(new[] { true, false }, _events);
    }

    [Fact]
    public void Reentering_within_leave_delay_keeps_expanded_without_events()
    {
        var h = Make(); h.PointerEntered(); Ms(120); h.PointerExited(); Ms(200); h.PointerEntered(); Ms(1000);
        Assert.True(h.IsExpanded); Assert.Equal(new[] { true }, _events);
    }

    [Fact]
    public void Open_panel_holds_expanded_and_closing_it_outside_collapses_immediately()
    {
        var h = Make(); h.PointerEntered(); Ms(120); h.SetPanelOpen(true); h.PointerExited(); Ms(5000);
        Assert.True(h.IsExpanded);
        h.SetPanelOpen(false);
        Assert.False(h.IsExpanded);
    }

    [Fact]
    public void Closing_panel_while_pointer_inside_stays_expanded()
    {
        var h = Make(); h.PointerEntered(); Ms(120); h.SetPanelOpen(true); h.SetPanelOpen(false); Ms(5000);
        Assert.True(h.IsExpanded);
    }
}
```

- [ ] **Step 2: Testlerin başarısız olduğunu gör**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~HoverIntentTests"`
Expected: Derleme hatası.

- [ ] **Step 3: `HoverIntent` uygula** (`TimeProvider.CreateTimer` ile; bekleyen zamanlayıcı her yeni olayda iptal edilir). Testler geçsin.

Run: aynı komut. Expected: 6 test PASS.

- [ ] **Step 4: Adaya bağla ve animasyonu uygula**

- `Pill.MouseEnter/MouseLeave` → `PointerEntered/PointerExited`; `ExpandedChanged` → `Dispatcher.BeginInvoke`.
- Genişleme: `Width` 400→770 ve `Height` 40→46, 0,4 sn, `BackEase { EasingMode = EaseOut, Amplitude = 0.15 }`; küçülme 0,3 sn `CubicEase EaseOut`. Köşe yarıçapı dönüştürücüyle kendiliğinden izler.
- İçerik: iki katman (`CompactContent`, `ExpandedContent`) üst üste; genişlerken küçük katmanın opaklığı 0,16 sn'de 0'a, geniş katman 0,06 sn gecikmeyle 0,22 sn'de 1'e. Plan 1'de geniş katman yalnızca tarih (solda), saat (sağda) ve ortada boş bir alan içerir; Plan 2 doldurur. Görünmeyen katman `IsHitTestVisible=False`.
- "Animasyonları azalt" açıksa süreler 0.

- [ ] **Step 5: Elle doğrula**

1. Fareyi adanın üstüne getir → ~0,12 sn sonra 770 genişliğe hafif esneyerek açılır; çek → ~0,35 sn sonra küçülür.
2. Fareyi adanın üzerinden hızla geçir (Chrome sekmesine giderken olduğu gibi) → açılmaz.
3. Windows'ta "Animasyon efektleri"ni kapat → açılma/kapanma anında olur.
Expected: Üçü de gözlenir.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat: adanın fareyle genişleyip küçülmesi" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Cam görünümü denemesi (duvar kağıdı / akrilik / düz) ve karar

**Files:**
- Create: `src/CamAda.Core/Glass/Geometry.cs`, `WallpaperStyle.cs`, `WallpaperLayout.cs`, `GlassImageProcessor.cs`
- Create: `src/CamAda/Glass/WallpaperSource.cs`, `WallpaperGlass.cs`, `AcrylicGlass.cs`
- Modify: `src/CamAda/Island/IslandWindow.xaml(.cs)` (sağ tık menüsüne "Cam görünümü" alt menüsü), `docs/superpowers/specs/2026-11-23-cam-ada-design.md` (§9 karar notu)
- Test: `tests/CamAda.Core.Tests/Glass/WallpaperTests.cs`, `tests/CamAda.Core.Tests/Glass/GlassImageProcessorTests.cs`

**Interfaces:**
- Consumes: `IslandWindow.Pill` (Task 5), `AppSettings.GlassMode` (Task 2).
- Produces:
  ```csharp
  namespace CamAda.Core.Glass;
  public readonly record struct SizeD(double Width, double Height);
  public readonly record struct RectD(double X, double Y, double Width, double Height);
  public enum WallpaperStyle { Fill, Fit, Stretch, Center }
  public static class WallpaperStyleParser
  {
      public static WallpaperStyle Parse(string? wallpaperStyle, string? tileWallpaper);
      // "10"→Fill, "6"→Fit, "2"→Stretch, "0"+"0"→Center; Tile ("0"+"1"), Span ("22") ve bilinmeyen→Fill
  }
  public static class WallpaperLayout
  {
      // Ekrandaki bir bölgeyi (ekran sol üstüne göre DIP/piksel, aynı birim) duvar kağıdı görüntüsü pikseline çevirir.
      // Fill: s=max(W/iw,H/ih); Fit: s=min(...); Center: s=1; görüntü ((W-iw*s)/2,(H-ih*s)/2) noktasına yerleşir;
      // sonuç = (bölge - yerleşim) / s. Stretch: x ve y ayrı ölçeklenir. Sonuç görüntü dışına taşabilir.
      public static RectD MapToImage(RectD regionOnScreen, SizeD screen, SizeD image, WallpaperStyle style);
  }
  public static class GlassImageProcessor
  {
      // BGRA, stride = width*4. Önce ayrılabilir kutu bulanıklığı (3 geçiş, kenarda sıkıştırma),
      // sonra doygunluk: L = 0.2126R + 0.7152G + 0.0722B; c' = clamp(round(L + (c - L) * factor)). Alfa değişmez.
      public static void Process(Span<byte> bgra, int width, int height, int blurRadius, double saturation);
  }
  namespace CamAda.Glass;
  public interface IGlassBackdrop : IDisposable { void Attach(Window host, Border pill); }
  public sealed class WallpaperGlass : IGlassBackdrop { }  // varsayılan
  public sealed class AcrylicGlass  : IGlassBackdrop { }
  // Düz mod: ayrı sınıf yok; ton #C714182A (opaklık 0.78), bulanıklık yok.
  ```

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
using CamAda.Core.Glass;
namespace CamAda.Core.Tests.Glass;

public class WallpaperTests
{
    [Theory]
    [InlineData("10", "0", WallpaperStyle.Fill)]
    [InlineData("6", "0", WallpaperStyle.Fit)]
    [InlineData("2", "0", WallpaperStyle.Stretch)]
    [InlineData("0", "0", WallpaperStyle.Center)]
    [InlineData("0", "1", WallpaperStyle.Fill)]
    [InlineData("22", "0", WallpaperStyle.Fill)]
    [InlineData(null, null, WallpaperStyle.Fill)]
    public void Parses_registry_style(string? style, string? tile, WallpaperStyle expected) =>
        Assert.Equal(expected, WallpaperStyleParser.Parse(style, tile));

    private static void Near(RectD expected, RectD actual)
    {
        Assert.Equal(expected.X, actual.X, 3); Assert.Equal(expected.Y, actual.Y, 3);
        Assert.Equal(expected.Width, actual.Width, 3); Assert.Equal(expected.Height, actual.Height, 3);
    }
    private static readonly SizeD Screen = new(1536, 864);

    [Fact] public void Fill_same_aspect() =>
        Near(new RectD(710, 10, 500, 50),
             WallpaperLayout.MapToImage(new RectD(568, 8, 400, 40), Screen, new SizeD(1920, 1080), WallpaperStyle.Fill));

    [Fact] public void Fill_wide_image_is_cropped_horizontally() =>
        Near(new RectD(1268.519, 0, 462.963, 64.815),
             WallpaperLayout.MapToImage(new RectD(568, 0, 400, 56), Screen, new SizeD(3000, 1000), WallpaperStyle.Fill));

    [Fact] public void Stretch_full_screen_maps_to_full_image() =>
        Near(new RectD(0, 0, 1000, 1000),
             WallpaperLayout.MapToImage(new RectD(0, 0, 1536, 864), Screen, new SizeD(1000, 1000), WallpaperStyle.Stretch));

    [Fact] public void Fit_region_left_of_image_is_outside() =>
        Near(new RectD(-388.889, 0, 115.741, 115.741),
             WallpaperLayout.MapToImage(new RectD(0, 0, 100, 100), Screen, new SizeD(1000, 1000), WallpaperStyle.Fit));

    [Fact] public void Center_unscaled() =>
        Near(new RectD(0, 0, 10, 10),
             WallpaperLayout.MapToImage(new RectD(268, 182, 10, 10), Screen, new SizeD(1000, 500), WallpaperStyle.Center));
}

public class GlassImageProcessorTests
{
    private static byte[] Solid(int w, int h, byte b, byte g, byte r) =>
        Enumerable.Range(0, w * h).SelectMany(_ => new[] { b, g, r, (byte)255 }).ToArray();

    [Fact]
    public void Uniform_image_is_unchanged_by_blur()
    {
        var px = Solid(8, 8, 30, 20, 10);
        GlassImageProcessor.Process(px, 8, 8, blurRadius: 2, saturation: 1.0);
        Assert.Equal(Solid(8, 8, 30, 20, 10), px);
    }

    [Fact]
    public void Blur_spreads_a_bright_pixel()
    {
        var px = Solid(9, 9, 0, 0, 0);
        int c = (4 * 9 + 4) * 4; px[c] = px[c + 1] = px[c + 2] = 255;
        GlassImageProcessor.Process(px, 9, 9, blurRadius: 1, saturation: 1.0);
        Assert.InRange(px[c], 1, 254);
        Assert.True(px[c + 4] > 0);              // sağ komşu aydınlandı
    }

    [Fact]
    public void Saturation_keeps_gray()
    {
        var px = Solid(1, 1, 128, 128, 128);
        GlassImageProcessor.Process(px, 1, 1, blurRadius: 0, saturation: 1.7);
        Assert.Equal(new byte[] { 128, 128, 128, 255 }, px);
    }

    [Fact]
    public void Saturation_pushes_color_away_from_gray_and_keeps_alpha()
    {
        var px = new byte[] { 200, 100, 50, 255 };   // B, G, R, A
        GlassImageProcessor.Process(px, 1, 1, blurRadius: 0, saturation: 2.0);
        Assert.Equal(new byte[] { 255, 103, 3, 255 }, px);
    }
}
```

- [ ] **Step 2: Testlerin başarısız olduğunu gör**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~Glass"`
Expected: Derleme hatası.

- [ ] **Step 3: Core sınıflarını uygula, testler geçsin**

Run: aynı komut. Expected: 16 test PASS.

- [ ] **Step 4: Üç cam modunu uygula**

- `WallpaperSource`: duvar kağıdı yolunu `SystemParametersInfo(SPI_GETDESKWALLPAPER)` ile, stili `HKCU\Control Panel\Desktop` altındaki `WallpaperStyle` ve `TileWallpaper` değerlerinden okur. `SystemEvents.UserPreferenceChanged` (Desktop kategorisi) ve `DisplaySettingsChanged` olaylarında `Changed` olayını tetikler. Duvar kağıdı tek renk ya da okunamıyorsa düz mod gibi davranır.
- `WallpaperGlass`: ada penceresinin ekrandaki dikdörtgenini, kenarlarda bulanıklık yarıçapının 3 katı kadar payla birlikte `WallpaperLayout.MapToImage` ile görüntüye eşler. Bu kesiti cihaz pikselinde kırpar (görüntü dışı siyah). `GlassImageProcessor.Process` ile işler: yarıçap = `round(26 × DPI ölçeği)`, doygunluk 1.7. Payı atıp sonucu pencere boyutunda bir `BitmapSource` yapar. Pill'in arka planı bir `ImageBrush`'tır (`ViewboxUnits=Absolute`, `Viewbox` = pill'in penceredeki dikdörtgeni, `Stretch=Fill`) ve üstte `#7514182A` ton katmanı bulunur. Pill'in `SizeChanged`'inde (animasyon sırasında her karede) `Viewbox` güncellenir. Duvar kağıdı değişince bitmap yeniden üretilir.
- `AcrylicGlass`: ManagedShell'in `WindowHelper.SetWindowBlur(hwnd, true)` çağrısı ve pill'in dikdörtgenine eşit, köşe yarıçapı yüksekliğe eşit bir `CreateRoundRectRgn` + `SetWindowRgn`. Bölge `SizeChanged`'de güncellenir. Bu modda gölge bölgenin dışında kaldığı için görünmez.
- Düz: `#C714182A`, bulanıklık yok.
- `IslandCommands` kaydına `GetGlassMode` ve `SetGlassMode` eklenir. Sağ tık menüsüne "Cam görünümü ▸ Duvar kağıdı / Akrilik / Düz" gelir; seçim anında uygulanır (eski `IGlassBackdrop` `Dispose` edilir, yenisi `Attach` edilir) ve `settings.json`'a kaydedilir.

- [ ] **Step 5: Elle doğrula**

1. Her üç modda adayı küçük ve genişlemiş hâlde gözle: köşelerde dikdörtgen taşma, tırtıklı kenar ya da animasyon sırasında titreme var mı?
2. Duvar kağıdını değiştir (Ayarlar › Kişiselleştirme) → duvar kağıdı modunda cam ~2 sn içinde yeni görüntüyü gösterir.
3. Görev Yöneticisi'nde boştayken işlemci kullanımı ~%0, animasyon sırasında kısa bir artış.
Expected: Gözlemler not edilir.

- [ ] **Step 6: Kullanıcıyla karar (kontrol noktası)**

Kullanıcıya üç modu canlı göster (sağ tık menüsünden geçiş). Seçtiği modu `AppSettings.GlassMode` varsayılanı yap (Task 2'deki varsayılan değer ve testteki beklenen değer birlikte güncellenir). Spec'in §9 bölümüne "Cam görünümü kararı: …, gerekçe: …" satırını ekle. Plan 2–4 bu karara göre yazılır (paneller ve plak kartı için de aynı yöntem kullanılır mı, kullanılmaz mı).

- [ ] **Step 7: Testler ve commit**

Run: `dotnet test`
Expected: Tüm testler PASS.
```bash
git add src tests docs
git commit -m "feat: cam görünümü modları ve seçilen varsayılan" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Plan 1 bittiğinde

- `dotnet test` tüm testleri geçer.
- Uygulama çalışırken ana ekranın üstünde 56 DIP'lik şerit ayrılır, ortada cam ada vardır: tarih ve saat gösterir, fareyle genişler ve küçülür, tam ekranda gizlenir.
- Görev çubuğu isteğe bağlı gizlenir ve normal çıkış, çökme, süreç öldürme ve oturum kapatmada geri gelir; masaüstünde kurtarma kısayolu vardır.
- Cam görünümü kararı verilmiş ve spec'e yazılmıştır.
