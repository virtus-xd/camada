# Plan 2: Görev Çubuğu İşlevleri — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Genişlemiş adayı Windows görev çubuğunun yerini tutacak hâle getirmek: sabitlenmiş ve açık uygulamalar, pencere önizlemeleri, arka plan (tepsi) simgeleri, Başlat ve arama paneli, güç işlemleri; ardından görev çubuğunu varsayılan olarak gizleyip uygulamayı oturum açılışında başlatmak.

**Architecture:** Saf mantık (`CamAda.Core`: sabitlenmiş uygulama çözümü, gruplama, tıklama kararı, taşma, arama sıralaması, onay kapısı, çökme günlüğü) birim testli. WPF tarafında ManagedShell'in `Tasks` (pencereler), `NotificationArea` (tepsi), `IconImageConverter`, `StoreAppHelper`, `PowerHelper` ve DWM canlı küçük resimleri kullanılır. Paneller adanın altında açılan, etkinleşebilen (klavye için) ayrı pencerelerdir; odağı kaybedince kapanır.

**Tech Stack:** .NET 10, C#, WPF, ManagedShell 0.0.374, xUnit, Microsoft.Extensions.TimeProvider.Testing.

**Spec:** `docs/superpowers/specs/2026-11-23-cam-ada-design.md` (özellikle §7.2, §7.3, §7.4 "Başlat ve ara", "Pencereler", "Arka planda çalışanlar", §7.6, §7.7, §8, §10). Görsel referans: tasarım tuvalindeki "Ada v2" artboard'u.

## Yol haritası (bu plan 2/4)

Plan 1 bitti (main). Bu plan: görev çubuğu işlevleri. Plan 3: medya ve plak çalar. Plan 4: sistem panelleri (hızlı ayarlar, ses/mikrofon, pano, takvim, bildirimler, kısa genişlemeler). Genişlemiş adada müzik, pano, Wi‑Fi, ses ve bildirim düğmeleri Plan 3–4'te eklenir; bu planda yerleri boş bırakılmaz, satır yalnızca bu plandaki öğeleri içerir.

Bilerek dışarıda bırakılan: spec §7.2 "Klavye odağı adaya gelince de genişler". Ada odak almayan bir pencere olduğu için Tab ile ulaşılamaz; adayı klavyeden açmak Plan 4'teki genel kısayol tuşlarıyla birlikte ele alınır. Başlat panelinin kendisi klavyeyle tam kullanılır (Görev 6).

## Global Constraints

- Plan 1'in tüm kısıtları geçerli (Windows 11 build 26300, `CamAda.Core` net10.0 WPF'siz, `CamAda` net10.0-windows10.0.22621.0, ManagedShell `0.0.374`, Explorer'a enjeksiyon yok, Türkçe arayüz, Manrope, "animasyonları azalt" desteği, commit sonu `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`).
- Ada: küçük 400×40 DIP; genişlemiş en az **770×46** DIP, uygulama sayısına göre genişler, en fazla **1100** DIP. Ada penceresi genişliği **1200** DIP olur (genişlemiş hap + gölge payı). Üst kenar tetikleyicisi bu planda değişmez: ortada **900** DIP genişlik (kullanıcının Plan 1 sonrası kararı bekleniyor).
- Genişlemiş satırın sırası (soldan sağa): Başlat, Ara | sabitlenmiş uygulamalar (ayar sırası) + 6 DIP boşluk + sabitlenmemiş açık uygulamalar | `^` (arka planda çalışanlar) | saat.
- Uygulama düğmesi 36×36 DIP, simge 26 DIP; açık uygulamanın altında 4×3 DIP nokta, iki ve üzeri pencerede iki nokta; dikkat isteyen (yanıp sönen) uygulamada vurgu rengi `#FFC27A` nokta; uygulamanın görev çubuğu rozeti (ör. Discord okunmamış) simgenin sağ üstünde gösterilir.
- Görünür uygulama düğmesi en fazla **10**; fazlası tek bir "+N" düğmesinde toplanır, tıklanınca kalanları listeleyen panel açılır.
- Paneller: adanın altında, üstten **62** DIP, yatayda ortalı; köşe yarıçapı **24** DIP; arka plan düz cam `#E014182A`, kenar `#2BFFFFFF`; açılırken 0,22 sn aşağı kayarak belirir. Odağı kaybedince ya da Esc ile kapanır; panel açıkken ada büyük kalır.
- Windows tuşu Windows'un kendi Başlat menüsünü açmaya devam eder; adadaki Başlat ve Ara düğmeleri kendi panelimizi açar.
- Kapat ve Yeniden başlat tek adımlık onay ister: ilk tıklamada düğme "Emin misin?" olur, 3 sn içinde ikinci tıklama işlemi yapar.
- Web araması varsayılan tarayıcıda `https://www.google.com/search?q=<sorgu>` açar.

## Review Focus

1. **Sabitlenmiş uygulama yüklü değilse ya da yolu silinmişse** (ör. Steam kaldırılmış) → varsayılanlarda hiç görünmez; kullanıcının ayarında varsa soluk görünür, tıklamak hiçbir şeyi çökertmez. (Görev 2 testleri)
2. **Bir uygulamanın birden fazla süreci ya da penceresi** (Chrome profilleri, `Chrome.exe` / `chrome.exe` büyük-küçük harfi, Dosya Gezgini'nin `explorer.exe` içinde çalışması) → hepsi doğru sabitlenmiş düğmenin altında toplanır. (Görev 2 testleri)
3. **Yönetici olarak çalışan bir pencere** (ör. yönetici Görev Yöneticisi) → öne getirme/küçültme Windows tarafından reddedilebilir; ada çökmez, sessizce dener. (Görev 4 elle kontrol adımı)
4. **Çok sayıda açık uygulama** → ada en fazla 1100 DIP olur, 10'dan fazlası "+N" altında toplanır. (Görev 3 testleri)
5. **Türkçe büyük/küçük harf ve noktalı İ/ı ile arama** ("ayar", "AYAR", "ıs", "İndir") → doğru sonuçlar. (Görev 6 testleri)

---

## Dosya yapısı

```
src/CamAda.Core/
  Diagnostics/CrashLog.cs
  Apps/PinnedApp.cs, Apps/PinnedAppDefaults.cs
  Apps/RunningWindow.cs, Apps/DockEntry.cs, Apps/DockGrouping.cs, Apps/TaskClickAction.cs, Apps/DockOverflow.cs
  Search/SearchItem.cs, Search/SearchRanker.cs, Search/SettingsPages.cs
  Power/ConfirmGate.cs
  Startup/AutostartCommand.cs
  Settings/AppSettings.cs (PinnedApps, StartWithWindows eklenir)
src/CamAda/
  Island/SmartHide.cs                 (IslandWindow'dan ayrılan akıllı gizlenme)
  Island/IslandBar.xaml(.cs)          (genişlemiş satır)
  Island/DockViewModel.cs, Island/DockItemViewModel.cs
  Panels/PanelWindow.cs               (tüm panellerin ortak penceresi)
  Panels/WindowsPanel.xaml(.cs), Panels/TaskThumbnail.cs
  Panels/TrayPanel.xaml(.cs)
  Panels/StartPanel.xaml(.cs), Panels/AppIndex.cs
  Interop/Native.cs (LockWorkStation, Run anahtarı)
tools/install.ps1
```

---

### Task 1: Hazırlık — ada penceresini bölme, çökme günlüğü, küçük düzeltmeler

Plan 1 son incelemesinden ertelenenler: M3 (genişleme olay sırası), M13 (çökme günlüğü), M14 (IslandWindow'u bölme), M11 (eski "56 px şerit" ifadeleri).

**Files:**
- Create: `src/CamAda.Core/Diagnostics/CrashLog.cs`, `src/CamAda/Island/SmartHide.cs`
- Modify: `src/CamAda/Island/IslandWindow.xaml.cs`, `src/CamAda/App.xaml.cs`, `src/CamAda/Shell/StripAppBar.xaml(.cs)`, `src/CamAda/Glass/WallpaperGlass.cs`, `src/CamAda.Core/Settings/AppSettings.cs` (yorumlar)
- Test: `tests/CamAda.Core.Tests/Diagnostics/CrashLogTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace CamAda.Core.Diagnostics;
  public static class CrashLog
  {
      public const long MaxBytes = 512 * 1024;
      // <directory>\crash.log dosyasına "yyyy-MM-dd HH:mm:ss" zaman damgası + exception.ToString() ekler.
      // Dosya MaxBytes'ı aşarsa önce crash.old.log olarak taşınır (eskisinin üzerine yazılır).
      public static void Append(string directory, Exception exception, DateTimeOffset now);
  }
  namespace CamAda.Island;
  public sealed class SmartHide : IDisposable
  {
      // IslandWindow'daki örtülme ve imleç yoklaması buraya taşınır; davranış değişmez.
      public SmartHide(Window island, HoverIntent hover, Func<(int X, int Y)?> pinnedPosition, Func<Border> pill);
      public bool IsOccluded { get; }
      public event EventHandler? StateChanged;   // örtülme değişince
      public void Start();
  }
  ```

- [ ] **Step 1: Başarısız test yaz** — `CrashLogTests`: (a) `Append` sonrası dosya zaman damgasını (`"2026-11-24 10:15:00"`) ve istisna mesajını içerir; (b) iki çağrı iki kayıt bırakır; (c) dosya `MaxBytes`'tan büyükken çağrılınca eski içerik `crash.old.log`'a geçer ve `crash.log` yalnız yeni kaydı içerir.
- [ ] **Step 2:** `dotnet test --filter "FullyQualifiedName~CrashLogTests"` → derleme hatası.
- [ ] **Step 3:** `CrashLog` uygula; testler geçsin (3 PASS).
- [ ] **Step 4:** `App`: `DispatcherUnhandledException`, `AppDomain.UnhandledException` ve `TaskScheduler.UnobservedTaskException` önce `CrashLog.Append(%LOCALAPPDATA%\CamAda, ...)` yazar (yazamazsa yutar), sonra görev çubuğunu geri getirir.
- [ ] **Step 5:** `SmartHide`'ı ayır; `IslandWindow` yalnızca yerleşim, saat, genişleme animasyonu, cam ve menüyü tutar. M3: `ExpandedChanged` işleyicisi olay argümanı yerine gönderim anında `_hover.IsExpanded`'i kullanır ve pencere kapandıysa hiçbir şey yapmaz. M11: kod yorumlarındaki "56 px şerit / alan ayırır" ifadeleri akıllı gizlenmeye göre düzeltilir.
- [ ] **Step 6: Elle doğrula:** Plan 1 Görev 8'in ölçüm betiği (ekranı kaplayan test penceresi: çizgi, üst kenarda açılma, tıklama geçişi, tam ekranda gizlenme) aynı sonuçları verir. `dotnet test` tamamı geçer.
- [ ] **Step 7: Commit** — `refactor: akıllı gizlenmeyi ayır, çökme günlüğü ekle`

---

### Task 2: Sabitlenmiş uygulamalar, gruplama ve tıklama kararı (Core)

**Files:**
- Create: `src/CamAda.Core/Apps/PinnedApp.cs`, `PinnedAppDefaults.cs`, `RunningWindow.cs`, `DockEntry.cs`, `DockGrouping.cs`, `TaskClickAction.cs`
- Modify: `src/CamAda.Core/Settings/AppSettings.cs` (`PinnedApps`)
- Test: `tests/CamAda.Core.Tests/Apps/DockTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace CamAda.Core.Apps;
  // ProcessName: eşleştirme için küçük harfli exe adı (ör. "chrome.exe").
  public sealed record PinnedApp(string Id, string Name, string LaunchPath, string Arguments, string ProcessName);
  public static class PinnedAppDefaults
  {
      // Sıra: Chrome, Dosya Gezgini, Steam, Discord. Yalnızca dosyası var olanlar döner.
      // Chrome: %ProgramFiles%\Google\Chrome\Application\chrome.exe, sonra %ProgramFiles(x86)%\..., sonra %LOCALAPPDATA%\Google\Chrome\Application\chrome.exe
      // Dosya Gezgini: %WINDIR%\explorer.exe (ProcessName "explorer.exe")
      // Steam: steamExeFromRegistry (HKCU\Software\Valve\Steam\SteamExe), yoksa %ProgramFiles(x86)%\Steam\steam.exe
      // Discord: %LOCALAPPDATA%\Discord\Update.exe, Arguments "--processStart Discord.exe", ProcessName "discord.exe"
      public static IReadOnlyList<PinnedApp> Resolve(Func<string, string> expandEnvironment, Func<string, bool> fileExists,
                                                     string? steamExeFromRegistry);
  }
  public sealed record RunningWindow(nint Handle, string ProcessName, string Title, bool IsActive, bool IsFlashing,
                                     bool IsMinimized, string? AppUserModelId, bool HasBadge);
  public sealed record DockEntry(string Key, PinnedApp? Pinned, IReadOnlyList<RunningWindow> Windows)
  {
      public bool IsRunning => Windows.Count > 0;
      public bool NeedsAttention => Windows.Any(w => w.IsFlashing);
      public bool HasBadge => Windows.Any(w => w.HasBadge);
  }
  public static class DockGrouping
  {
      // Önce sabitlenmişler (ayar sırası, açık olmasalar da), sonra sabitlenmemiş açık uygulamalar ilk görülme sırasıyla.
      // Eşleştirme ProcessName büyük/küçük harf duyarsız. Sabitlenmemişlerde anahtar AppUserModelId varsa o, yoksa ProcessName.
      public static IReadOnlyList<DockEntry> Group(IReadOnlyList<PinnedApp> pinned, IReadOnlyList<RunningWindow> windowsInOrder);
  }
  public enum ClickKind { Launch, Activate, Minimize, ShowWindows }
  public readonly record struct ClickDecision(ClickKind Kind, RunningWindow? Window);
  public static class TaskClickAction
  {
      // 0 pencere → Launch; 1 pencere: etkin ve küçültülmemiş → Minimize, değilse Activate; 2+ → ShowWindows.
      public static ClickDecision Decide(DockEntry entry);
  }
  // AppSettings: public IReadOnlyList<PinnedApp>? PinnedApps { get; init; } = null;  // null = henüz belirlenmedi
  ```

- [ ] **Step 1: Başarısız testleri yaz** (`DockTests`):
  - `Defaults_keep_order_and_skip_missing`: yalnızca chrome ve discord dosyaları "var" → `["chrome","discord"]` sırası; Steam ve Explorer yoksa listede yok.
  - `Steam_from_registry_wins`: kayıt defteri yolu verilince Steam o yolu kullanır.
  - `Discord_uses_update_exe_with_process_start`: `LaunchPath` `...\Discord\Update.exe`, `Arguments == "--processStart Discord.exe"`, `ProcessName == "discord.exe"`.
  - `Pinned_apps_appear_even_when_not_running`: 4 sabitlenmiş, pencere yok → 4 giriş, hepsi `IsRunning == false`.
  - `Windows_group_under_pinned_app_case_insensitively`: `Chrome.exe` ve `chrome.exe` pencereleri Chrome girişinde 2 pencere.
  - `Unpinned_running_apps_follow_pinned_in_first_seen_order`: pencereler [code.exe, chrome.exe, notepad.exe, code.exe] → girişler Chrome (sabit) …, sonra code (2 pencere), notepad.
  - `Unpinned_group_by_app_user_model_id_when_present`: aynı exe farklı AUMID → iki ayrı giriş.
  - `Attention_and_badge_flags`: yanıp sönen pencere → `NeedsAttention`; rozetli → `HasBadge`.
  - `Click_decisions`: 0 pencere → Launch; 1 etkin → Minimize; 1 etkin değil → Activate; 1 küçültülmüş ve etkin → Activate; 2 → ShowWindows.
  - `SettingsStoreTests.Missing_pinned_apps_stay_null` ve `Pinned_apps_round_trip`.
- [ ] **Step 2:** `dotnet test --filter "FullyQualifiedName~DockTests|FullyQualifiedName~SettingsStoreTests"` → derleme hatası.
- [ ] **Step 3:** Uygula; testler geçsin.
- [ ] **Step 4: Commit** — `feat: sabitlenmiş uygulamalar, gruplama ve tıklama kararı`

---

### Task 3: Genişlemiş satır: Başlat, Ara, uygulamalar, taşma

**Files:**
- Create: `src/CamAda.Core/Apps/DockOverflow.cs`, `src/CamAda/Island/IslandBar.xaml(.cs)`, `src/CamAda/Island/DockViewModel.cs`, `src/CamAda/Island/DockItemViewModel.cs`
- Modify: `src/CamAda.Core/Island/IslandLayout.cs` (`WindowWidth = 1200`, `ExpandedWidthFor`), `src/CamAda/Island/IslandWindow.xaml(.cs)`, `src/CamAda/Shell/ShellHost.cs` (`TaskIconSize = IconSize.Large`), `src/CamAda/App.xaml.cs` (ilk açılışta `PinnedAppDefaults.Resolve` → ayara kaydet)
- Test: `tests/CamAda.Core.Tests/Apps/DockOverflowTests.cs`, `tests/CamAda.Core.Tests/Island/IslandLayoutTests.cs`

**Interfaces:**
- Consumes: `DockGrouping.Group`, `DockEntry`, `TaskClickAction` (Task 2).
- Produces:
  ```csharp
  namespace CamAda.Core.Apps;
  public sealed record DockSplit(IReadOnlyList<DockEntry> Visible, IReadOnlyList<DockEntry> Overflow);
  public static class DockOverflow
  {
      public const int MaxVisible = 10;
      // Sabitlenmişler her zaman görünür kalır; taşma sabitlenmemişlerin sonundan alınır.
      public static DockSplit Split(IReadOnlyList<DockEntry> entries);
  }
  namespace CamAda.Core.Island;
  // IslandLayout: public const double WindowWidth = 1200; public const double MaxExpandedWidth = 1100;
  // 770 DIP; 5 uygulama düğmesine kadar sabit (Başlat, Ara, ^, saat ve 5 düğme sığar). Her ek düğme (+N dahil) +38 DIP, en fazla 1100.
  public static double ExpandedWidthFor(int appButtonCount);
  namespace CamAda.Island;
  public sealed class DockViewModel : IDisposable
  {
      // Tasks.GroupedWindows'u ve sabitlenmişleri izler; 150 ms'lik yığınlamayla Items'ı yeniden kurar.
      public DockViewModel(ManagedShell.ShellManager shell, Func<IReadOnlyList<PinnedApp>> pinnedApps, Dispatcher dispatcher);
      public ObservableCollection<DockItemViewModel> Items { get; }       // görünürler + gerekiyorsa "+N"
      public IReadOnlyList<DockEntry> Overflow { get; }
      public event EventHandler? Changed;
      // RunningWindow.Handle → ManagedShell penceresi (öne getirme, küçültme, kapatma ve simge için).
      public ManagedShell.WindowsTasks.ApplicationWindow? Find(nint handle);
  }
  ```

- [ ] **Step 1: Başarısız testleri yaz:** `DockOverflowTests`: 8 giriş → hepsi görünür, taşma boş; 4 sabit + 9 sabitlenmemiş → görünür 10 (4 sabit + ilk 6), taşma son 3; 12 sabit + 0 → hepsi görünür (sabitler kesilmez). `IslandLayoutTests`: `ExpandedWidthFor(0) == 770`, `(5) == 770`, `(6) == 808`, `(11) == 998`, `(30) == 1100`.
- [ ] **Step 2:** Testlerin başarısız olduğunu gör.
- [ ] **Step 3:** Uygula; testler geçsin.
- [ ] **Step 4: Satırı kur.** `IslandBar`: Başlat (halka + nokta, `#FFC27A`) ve Ara (büyüteç) 34×34 yuvarlak düğmeler; 1 DIP ayırıcı; `ItemsControl` ile uygulama düğmeleri (36×36, simge 26 DIP; sabitlenmişte simge `IconImageConverter.GetImageFromAssociatedIcon(LaunchPath, IconSize.Large)`, sabitlenmemişte pencerenin `Icon`'u; altında çalışma noktaları; dikkatte vurgu noktası; rozet varsa pencerenin `OverlayIcon`'u 14 DIP sağ üstte); sabitlenmemişlerden önce 6 DIP boşluk; "+N" düğmesi; ayırıcı; `^` düğmesi; saat. Her düğmede `AutomationProperties.Name` (ör. "Chrome, 2 pencere", "Discord, dikkat istiyor"). Dosya yolu artık yoksa simge %40 opak ve tıklama yalnızca adayı küçültür. Genişleme hedef genişliği `ExpandedWidthFor(görünür düğme sayısı)`; satır değişince açık ada yeni genişliğe 0,2 sn'de geçer. Plan 1'in genişlemiş katmanındaki tarih/saat bu satırla değişir (genişlemişte yalnızca saat).
- [ ] **Step 5: Elle doğrula:** ekran görüntüsünde genişlemiş adada Başlat, Ara, 4 sabit uygulama (gerçek simgeleriyle), açık sabitlenmemiş uygulamalar, `^`, saat görünür; Chrome'da iki pencere açıkken iki nokta; 12 Not Defteri açınca ada 1100'de durur ve "+N" belirir.
- [ ] **Step 6: Commit** — `feat: genişlemiş adada uygulamalar`

---

### Task 4: Paneller, uygulama tıklaması ve pencere önizlemeleri

**Files:**
- Create: `src/CamAda/Panels/PanelWindow.cs`, `src/CamAda/Panels/WindowsPanel.xaml(.cs)`, `src/CamAda/Panels/TaskThumbnail.cs`
- Modify: `src/CamAda/Island/IslandBar.xaml.cs`, `src/CamAda/Island/IslandWindow.xaml.cs`

**Interfaces:**
- Consumes: `TaskClickAction.Decide`, `DockEntry` (Task 2), `DockViewModel.Overflow` (Task 3), `HoverIntent.SetPanelOpen` (Plan 1).
- Produces:
  ```csharp
  namespace CamAda.Panels;
  // Tüm panellerin penceresi: WindowStyle None, AllowsTransparency True, Topmost, ShowInTaskbar False,
  // içerik Border (CornerRadius 24, Background #E014182A, BorderBrush #2BFFFFFF), adanın altında üstten 62 DIP, yatayda ortalı.
  // Açılınca etkinleşir (klavye için); Deactivated ya da Esc ile kapanır; açılış/kapanışta Opened/Closed olayları.
  public class PanelWindow : Window
  {
      public PanelWindow(FrameworkElement content, double width);
      public event EventHandler? PanelOpened;
      public event EventHandler? PanelClosed;
      public void ShowUnder(Window island);
  }
  public sealed class WindowsPanel : UserControl   // başlık (uygulama adı, "N pencere"), "Yeni pencere", önizleme ızgarası
  {
      public WindowsPanel(DockEntry entry, Action<RunningWindow> activate, Action<RunningWindow> close, Action launchNew);
  }
  public sealed class TaskThumbnail : FrameworkElement  // DwmRegisterThumbnail ile canlı önizleme, 236×140 DIP
  {
      public nint SourceWindow { get; set; }
  }
  ```

- [ ] **Step 1: Tıklamayı bağla.** Uygulama düğmesi `TaskClickAction.Decide` sonucuna göre: Launch → `ShellHelper.StartProcess(LaunchPath, Arguments)` (başarısızsa hiçbir şey olmaz) ve düğme simgesi 0,5 sn "zıplar"; Activate → `ApplicationWindow.BringToFront()`; Minimize → `ApplicationWindow.Minimize()`; ShowWindows → `WindowsPanel`. "+N" → kalan girişleri ad + simge listesi olarak gösteren panel (tıklanınca aynı karar uygulanır). Orta tık → Launch (yeni pencere). Tüm ManagedShell çağrıları `try/catch` ile sarılır; yönetici pencerelerinde reddedilirse sessiz kalır.
- [ ] **Step 2: Paneli ve önizlemeyi uygula.** `PanelWindow` açılınca `HoverIntent.SetPanelOpen(true)`, kapanınca `false`. Aynı anda tek panel: yenisi açılırken eskisi kapanır. `TaskThumbnail`: RetroBar'ın `TaskThumbnail` yaklaşımıyla `DwmRegisterThumbnail(hedef pencere, kaynak)`, `DwmUpdateThumbnailProperties` (kaynak oranını koruyarak, `fSourceClientAreaOnly = false`), `CompositionTarget.Rendering` ile konum güncellemesi, `Unloaded`'da `DwmUnregisterThumbnail`.
- [ ] **Step 3: Önizleme denemesi (karar adımı).** Chrome'da 2 pencere açıkken Chrome düğmesine tıkla, ekran görüntüsü al. Önizlemeler görünüyorsa katmanlı panel kalır. Görünmüyorsa (DWM küçük resmi katmanlı pencereye çizilmezse) `PanelWindow` `AllowsTransparency=False`, `Background=#14182A` ve `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2)` ile katmansız pencereye geçer; spec §9'a "Paneller katmansız, 8 px Windows köşeleri" satırı eklenir ve karar kayda geçer.
- [ ] **Step 4: Elle doğrula:**
  1. Kapalı Steam → tıklayınca başlar, düğmede nokta belirir.
  2. Tek pencereli Dosya Gezgini: öndeyken tıklama küçültür, değilken öne getirir.
  3. Chrome 2 pencere → panelde iki canlı önizleme; birine tıklayınca o pencere öne gelir ve panel kapanır; × penceresini kapatır.
  4. Panel açıkken fare adadan çıkınca ada küçülmez; panel dışına tıklayınca panel kapanır ve ada küçülür.
  5. Yönetici olarak açılmış Görev Yöneticisi'nin düğmesine tıklamak uygulamayı çökertmez.
- [ ] **Step 5: Commit** — `feat: uygulama tıklaması, paneller ve pencere önizlemeleri`

---

### Task 5: Arka planda çalışanlar (tepsi) paneli

**Files:**
- Create: `src/CamAda/Panels/TrayPanel.xaml(.cs)`
- Modify: `src/CamAda/Shell/ShellHost.cs` (`EnableTrayService = true`, `AutoStartTrayService = true`, `PinnedNotifyIcons = []`), `src/CamAda/Island/IslandBar.xaml.cs` (`^`)

**Interfaces:**
- Consumes: `PanelWindow` (Task 4), `ShellHost.Manager.NotificationArea`.
- Produces: `public sealed class TrayPanel : UserControl { public TrayPanel(ManagedShell.WindowsTray.NotificationArea area); }`

- [ ] **Step 1: Uygula.** Panel genişliği 380 DIP; başlık "Arka planda çalışanlar"; `NotificationArea.UnpinnedIcons`'tan `IsHidden == false` olanlar 4 sütunlu ızgarada (30 DIP simge + 11,5 DIP ad, ad `Title`'ın ilk satırı); alt satırda "Sol tık uygulamayı açar, sağ tık uygulamanın kendi menüsünü gösterir." Fare olayları RetroBar'daki gibi: `MouseEnter`'da `Placement` (simgenin ekran dikdörtgeni, fiziksel piksel) ve `IconMouseEnter(MouseHelper.GetCursorPositionParam())`; `MouseDown/Up`'ta `IconMouseDown/Up(e.ChangedButton, MouseHelper.GetCursorPositionParam(), SystemInformation.DoubleClickTime)`; `MouseLeave`'de `IconMouseLeave`. Panel açılırken `NotificationArea.SetTrayHostSizeData` üst kenar (`ABE_TOP`) ve adanın fiziksel dikdörtgeniyle çağrılır, böylece uygulamaların açılır pencereleri üstte belirir. Sağ tık menüsü açılınca panel kapanmaz (Deactivated, imleç paneldeyken yok sayılır).
- [ ] **Step 2: Elle doğrula:** Steam ve Discord simgeleri panelde; Discord'a sol tık penceresini açar, Steam'e sağ tık Steam'in menüsünü gösterir; Discord kapatılınca simgesi 2 sn içinde kaybolur; uygulamadan çıkınca Windows'un kendi tepsisinde simgeler yine görünür; Explorer yeniden başlayınca simgeler panelde yeniden belirir.
- [ ] **Step 3: Commit** — `feat: arka planda çalışanlar paneli`

---

### Task 6: Başlat ve ara paneli, güç işlemleri

**Files:**
- Create: `src/CamAda.Core/Search/SearchItem.cs`, `SearchRanker.cs`, `SettingsPages.cs`, `src/CamAda.Core/Power/ConfirmGate.cs`, `src/CamAda/Panels/StartPanel.xaml(.cs)`, `src/CamAda/Panels/AppIndex.cs`
- Modify: `src/CamAda/Island/IslandBar.xaml.cs` (Başlat, Ara), `src/CamAda/Interop/Native.cs` (`LockWorkStation`)
- Test: `tests/CamAda.Core.Tests/Search/SearchRankerTests.cs`, `tests/CamAda.Core.Tests/Power/ConfirmGateTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace CamAda.Core.Search;
  public enum SearchKind { App, Setting, File, Web }
  // Target: exe/.lnk yolu, "shell:AppsFolder\<AUMID>", "ms-settings:..." ya da URL.
  public sealed record SearchItem(string Name, SearchKind Kind, string Target);
  public static class SearchRanker
  {
      // tr-TR kültürüyle büyük/küçük harf duyarsız. Puan: ad sorguyla başlıyor 3, bir kelimesi sorguyla başlıyor 2, adın içinde geçiyor 1, yoksa elenir.
      // Eşitlikte tür sırası App > Setting > File, sonra ad. En fazla `limit` sonuç; sorgu boş değilse sona Web araması eklenir
      // (Name "Web'de ara: “<sorgu>”", Target "https://www.google.com/search?q=" + Uri.EscapeDataString(sorgu)).
      public static IReadOnlyList<SearchItem> Rank(string query, IReadOnlyList<SearchItem> items, int limit = 6);
  }
  public static class SettingsPages
  {
      // Türkçe adlarla ms-settings sayfaları: "Ses ayarları" ms-settings:sound, "Ekran ayarları" ms-settings:display,
      // "Bluetooth ve cihazlar" ms-settings:bluetooth, "Wi‑Fi" ms-settings:network-wifi, "Uygulamalar" ms-settings:appsfeatures,
      // "Kişiselleştirme" ms-settings:personalization, "Arka plan" ms-settings:personalization-background,
      // "Güç ve pil" ms-settings:powersleep, "Windows Update" ms-settings:windowsupdate, "Bildirimler" ms-settings:notifications,
      // "Fare" ms-settings:mousetouchpad, "Klavye" ms-settings:keyboard, "Depolama" ms-settings:storagesense, "Ayarlar" ms-settings:
      public static IReadOnlyList<SearchItem> All { get; }
  }
  namespace CamAda.Core.Power;
  public sealed class ConfirmGate
  {
      public ConfirmGate(TimeProvider time, TimeSpan window);   // window = 3 sn
      public bool IsArmed { get; }
      public bool Press();   // ilk basış silahlandırır ve false döner; süre içinde ikinci basış true döner ve sıfırlar
  }
  namespace CamAda.Panels;
  public sealed class AppIndex
  {
      // Başlat menüsü kısayolları (ShellHelper.UsersStartMenuPath ve AllUsersStartMenuPath altındaki .lnk; adı dosya adı,
      // kaldırıcı/yardım/belge kısayolları ad filtresiyle elenir: "Uninstall", "Kaldır", "Help", "Yardım", "Readme"),
      // Store uygulamaları (StoreAppHelper.AppList.FetchApps(), Target "shell:AppsFolder\<AUMID>"), SettingsPages.All,
      // son dosyalar (%APPDATA%\Microsoft\Windows\Recent içindeki en yeni .lnk'ler). Arka planda bir kez kurulur, 10 dk'da bir yenilenir.
      public Task<IReadOnlyList<SearchItem>> GetItemsAsync();
      public IReadOnlyList<SearchItem> RecentFiles(int count);
  }
  ```

- [ ] **Step 1: Başarısız testleri yaz:**
  - `SearchRankerTests`: `"ste"` → ilk sonuç "Steam"; `"AYAR"` → "Ayarlar" ve "Ses ayarları" eşleşir, "Ayarlar" önce (ad başlangıcı); `"ıs"` (noktasız) "Işık" adını bulur, `"İndir"` "indirilenler"i bulur (tr-TR); `"zzz"` → yalnızca Web satırı; boş sorgu → boş liste; aynı puanda App, Setting'den önce; `limit` aşılmaz ve Web satırı limitin dışında sona eklenir; Web hedefi `"https://www.google.com/search?q=ses%20ayar"`.
  - `ConfirmGateTests` (`FakeTimeProvider`): ilk basış false ve `IsArmed`; 2 sn sonra ikinci basış true ve `IsArmed == false`; 3,1 sn sonra ikinci basış false (yeniden silahlanır).
- [ ] **Step 2:** Testlerin başarısız olduğunu gör.
- [ ] **Step 3:** Uygula; testler geçsin.
- [ ] **Step 4: Paneli uygula** (genişlik 640 DIP, "Ada v2" artboard'undaki düzen). Arama kutusu açılınca odakta; Ara düğmesi aynı paneli açar. Boşken: "Sabitlenmiş" (ayardaki sabitlenmiş uygulamalar + "Ayarlar", 6 sütun), "Son açılanlar" (3 dosya), alt satırda "Tüm uygulamalar" (paneli alfabetik, kaydırılabilir uygulama listesine çevirir) ve Kilitle (`LockWorkStation`), Uyku (`PowerHelper.Sleep`), Yeniden başlat, Kapat (`ConfirmGate` ile; silahlıyken düğme etiketi "Emin misin?", sonra `PowerHelper.Reboot` / `PowerHelper.Shutdown`). Yazınca `SearchRanker.Rank` sonuçları; ilk sonuç seçili, ↑/↓ seçimi taşır, Enter seçileni açar (App/File: `ShellHelper.StartProcess(Target)`, `shell:AppsFolder\...` ve `ms-settings:` ve URL: `UseShellExecute` ile), Esc kapatır. Açılan her şeyden sonra panel kapanır.
- [ ] **Step 5: Elle doğrula:** Başlat'a tıkla → panel açılır ve yazmaya hazır; "ste" + Enter → Steam açılır; "ses" → "Ses ayarları" ilk sonuçlarda, Enter → Windows Ses ayarları; "Tüm uygulamalar" listesinde Store uygulamaları (ör. Hesap Makinesi) da var; Kapat'a bir kez tıklayınca "Emin misin?" görünür ve 3 sn bekleyince geri döner (bilgisayar kapatılmaz); Kilitle ekranı kilitler (kullanıcı onayıyla denenir).
- [ ] **Step 6: Commit** — `feat: başlat ve ara paneli, güç işlemleri`

---

### Task 7: Görev çubuğunu varsayılan gizleme, oturum açılışında başlama, kurulum

**Files:**
- Create: `src/CamAda.Core/Startup/AutostartCommand.cs`, `tools/install.ps1` (BOM'lu UTF-8)
- Modify: `src/CamAda.Core/Settings/AppSettings.cs` (`HideWindowsTaskbar = true`, `StartWithWindows = true`), `src/CamAda/App.xaml.cs`, `src/CamAda/Interop/Native.cs` (yok; Run anahtarı `Microsoft.Win32.Registry` ile), `tests/CamAda.Core.Tests/Settings/SettingsStoreTests.cs`
- Test: `tests/CamAda.Core.Tests/Startup/AutostartCommandTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace CamAda.Core.Startup;
  public static class AutostartCommand
  {
      public const string RunValueName = "CamAda";
      public static string Build(string exePath);   // "\"<exePath>\""
  }
  // AppSettings: HideWindowsTaskbar varsayılanı true; public bool StartWithWindows { get; init; } = true;
  //              public int SettingsVersion { get; init; } = 2;
  // SettingsStore.Load: dosyada "SettingsVersion" özelliği yoksa ya da 2'den küçükse bir kereye mahsus
  //                     HideWindowsTaskbar = true, SettingsVersion = 2 yapar ve dosyayı yeniden yazar (JsonDocument ile özelliğin varlığına bakılır).
  ```

- [ ] **Step 1: Başarısız testleri yaz:** `AutostartCommandTests`: boşluklu yol tırnaklanır (`C:\Users\a b\CamAda.exe` → `"C:\Users\a b\CamAda.exe"`). `SettingsStoreTests.Missing_file_returns_defaults_and_writes_file` beklentisi `HideWindowsTaskbar == true` ve `StartWithWindows == true` olur. Yeni: `Old_file_without_version_turns_on_taskbar_hiding_once` — `{ "HideWindowsTaskbar": false }` yüklenince `HideWindowsTaskbar == true`, dosyada `"SettingsVersion": 2`; `Versioned_file_keeps_users_choice` — `{ "HideWindowsTaskbar": false, "SettingsVersion": 2 }` yüklenince `false` kalır.
- [ ] **Step 2:** Testlerin başarısız olduğunu gör.
- [ ] **Step 3:** Uygula; testler geçsin.
- [ ] **Step 4: Uygulama.** Normal başlangıçta `StartWithWindows` true ise `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` altına `CamAda` = `AutostartCommand.Build(Environment.ProcessPath)` yazılır, false ise değer silinir. Sağ tık menüsüne "Windows açılınca başlat" (işaretli) eklenir. (Ayar sürümü geçişi Step 3'te `SettingsStore`'da yapılır.)
- [ ] **Step 5: `tools/install.ps1`:** `dotnet publish src/CamAda -c Release -r win-x64 --self-contained false -o "$env:LOCALAPPDATA\Programs\CamAda"`; önce çalışan `CamAda` süreçlerini `--restore-taskbar` ile kapatır; masaüstü kurtarma kısayolunu kurulu exe'ye göre yeniden oluşturur (`tools/create-restore-shortcut.ps1`); sonunda kurulu exe'yi başlatır. Kurtarma kısayolu artık `bin\Debug`'ı göstermez.
- [ ] **Step 6: Elle doğrula:**
  1. `tools/install.ps1` → uygulama `%LOCALAPPDATA%\Programs\CamAda`'dan çalışır, görev çubuğu gizli.
  2. Run anahtarında `CamAda` değeri kurulu exe'yi gösterir; değeri elle çalıştırmak (oturum açılışını taklit) adayı açar, ikinci örnek açılmaz.
  3. Menüden "Windows açılınca başlat" kaldırılınca Run değeri silinir.
  4. Masaüstü kurtarma kısayolu kurulu exe'yi gösterir ve çalışır.
  5. Plan 1'in güvenlik ağı ölçümleri (süreç ağacını öldürme → görev çubuğu geri gelir) kurulu sürümde tekrarlanır.
- [ ] **Step 7: Commit** — `feat: varsayılan gizleme, açılışta başlama ve kurulum betiği`

---

## Plan 2 bittiğinde

- `dotnet test` tamamı geçer.
- Görev çubuğu varsayılan olarak gizli; Chrome, Dosya Gezgini, Steam, Discord ve açık uygulamalar adadan açılır/öne gelir/küçülür; çok pencereli uygulamalarda canlı önizleme paneli; tepsi simgeleri panelde ve kendi menüleriyle çalışır; Başlat/Ara paneli uygulama, ayar, dosya ve web araması yapar; güç işlemleri onaylıdır.
- Uygulama `%LOCALAPPDATA%\Programs\CamAda`'dan kurulur ve oturum açılışında başlar; kurtarma kısayolu kurulu sürümü gösterir.
