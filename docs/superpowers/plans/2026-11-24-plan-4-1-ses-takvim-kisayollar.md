# Plan 4.1: Ses, Mikrofon, Takvim ve Kısayol Tuşu — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Genişlemiş adaya ses düğmesi ve "Ses ve mikrofon" panelini, saate tıklayınca açılan takvimi, küçük adaya mikrofon kapalı işaretini, ses değişince ada içinde kısa göstergeyi ve her yerden mikrofonu kapatıp açan Ctrl+Shift+M kısayolunu eklemek.

**Architecture:** Ses tek bir `AudioService`'ten gelir (Windows Core Audio, NAudio.Wasapi üzerinden; varsayılan cihaz değiştirmek için küçük bir `IPolicyConfig` COM arayüzü). Core Audio geri çağrıları UI iş parçacığına aktarılır; panel, ada düğmesi, küçük adadaki işaret ve gösterge aynı servisi dinler. Saf mantık `CamAda.Core`'da birim testli (takvim ızgarası, hoparlör düzeyi ve yüzde, uygulama oturumlarını gruplama, kısayol ayrıştırma, adanın görünürlük kararı). Kısayol `RegisterHotKey` ile mesaj penceresinde alınır.

**Tech Stack:** .NET 10, C#, WPF, ManagedShell 0.0.374, **NAudio.Wasapi 2.2.1** (MIT; Core Audio sarmalayıcısı), xUnit.

**Spec:** `docs/superpowers/specs/2026-11-23-cam-ada-design.md` — §7.1 (küçük adada mikrofon işareti), §7.2 (satır sırası: `^`, …, ses (+ mikrofon kapalıysa işaret), …, saat), §7.4 "Ses ve mikrofon" ve "Takvim", §7.5 "Ses … değişti", §7.9, §8 "Ses", §10 (takvim ızgarası testi), §11 (Ctrl+Shift+M).

## Yol haritası (Plan 4, 1/4)

Plan 1–3 ve "plağı gizle" main'de. Kullanıcı Plan 4'ün dört parçaya bölünmesini ve sırasını onayladı (24.11.2026): **1) ses + mikrofon + takvim + kısayol tuşu (bu plan)**, 2) pano + ekran görüntüleri (Ctrl+Shift+V, ekran görüntüsü önizlemesi), 3) hızlı ayarlar (+ parlaklık göstergesi), 4) bildirimler (+ bildirim genişlemesi, küçük adada bildirim sayısı). Pano, Wi‑Fi ve bildirim düğmeleri ilgili parçalarda ses düğmesinin soluna eklenir. Adayı klavyeden açacak bir kısayol spec'te tanımlı değil; bu planda yok.

## Global Constraints

- Plan 1–3'ün kısıtları geçerli: Windows 11 build 26300; `CamAda.Core` net10.0 ve WPF'siz; Explorer'a enjeksiyon yok; arayüz Türkçe; yazı Manrope, simgeler `Segoe Fluent Icons, Segoe MDL2 Assets`; derleme uyarısız; commit sonu `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Renkler: vurgu `#FFC27A` (`AccentBrush`), vurgu üstü metin `#2A1A08`, metin `#F4F6FB`; "kırmızımsı" mikrofon işareti `#E0607E` (yeni `WarningBrush`, `Theme/Glass.xaml`).
- Simgeler (Segoe Fluent Icons): sessiz `E74F`, ses 0 `E992`, düşük `E993`, orta `E994`, yüksek `E995`; mikrofon `E720`, mikrofon kapalı `EC54`; onay `E73E`; önceki/sonraki ay `E76B`/`E76C`.
- Mikrofonu kapat/aç **tüm uygulamalar için varsayılan kayıt cihazına** uygulanır: konsol (eConsole) ve iletişim (eCommunications) varsayılanı farklı cihazlarsa ikisine de. Hedef durum = konsol varsayılanının şimdiki durumunun tersi.
- Çıkış cihazı seçimi seçilen cihazı **üç rolün** (eConsole, eMultimedia, eCommunications) varsayılanı yapar.
- Kısayol: varsayılan **Ctrl+Shift+M**, ayardan değişir (`Hotkeys.MicToggle`). Win+V'ye dokunulmaz.
- Boştayken ~%0 işlemci: Core Audio olay tabanlı dinlenir; yoklama yalnız Ses paneli açıkken (uygulama listesi, saniyede bir).
- "Animasyonları azalt" açıksa göstergenin belirip kaybolma geçişi yok (anında).
- Tam ekran uygulamada (oyun, tam ekran video) ada hiçbir gösterge için görünmez (spec §4 anti-hedef).
- Canlı denemeler kullanıcının ses düzeyini, varsayılan cihazını ve mikrofon durumunu değiştirirse deneme sonunda **eski hâline döndürülür**; uygulama oturumu denemesi kullanıcının uygulamalarıyla değil scratchpad'de derlenen, sessiz WAV döngüsü çalan bir deneme exe'siyle yapılır.

## Review Focus

1. **Varsayılan çıkış ya da kayıt cihazı değişir, kulaklık/USB mikrofon takılıp çıkarılır** → ses düğmesi, panel, mikrofon işareti ve gösterge yeni cihazı izler; eski cihazın olay aboneliği bırakılır; çökme yok. (Görev 2 ve 3 elle adımları: `SetDefaultOutput` ile değiştirip geri almak)
2. **Hiç kayıt cihazı yok** → panelde "Mikrofon bulunamadı", Ctrl+Shift+M "Mikrofon bulunamadı" gösterir, küçük adada işaret yok, çökme yok. (Görev 3 `MicState` testi + Görev 5 elle adım)
3. **Ses CamAda'nın kendi kaydırıcısıyla değişir** → ada göstergesi çıkmaz (panel zaten gösteriyor); klavye ses tuşları ya da başka uygulama değiştirince çıkar. (Görev 4 elle adım, olay bağlamı Guid'i)
4. **Ctrl+Shift+M başka bir uygulamaya kayıtlı** (`RegisterHotKey` başarısız) → CamAda çalışmaya devam eder, açılışta adada bir kez "Ctrl+Shift+M kullanılamıyor" görünür. (Görev 5 elle adım: kısayolu önce deneme exe'sine kaydettirerek)
5. **Ses paneli açıkken bir kaydırıcı sürüklenirken liste yenilenir ya da ses çıkaran uygulama kapanır** → sürüklenen kaydırıcı geri zıplamaz; kapanan uygulamanın satırı en geç 1 sn'de kalkar. (Görev 3 elle adım)

---

### Task 1: Takvim paneli

**Files:**
- Create: `src/CamAda.Core/Calendar/MonthGrid.cs`, `src/CamAda/Panels/CalendarPanel.xaml(.cs)`
- Modify: `src/CamAda/Island/IslandBar.xaml(.cs)` (saat → düğme), `src/CamAda/Island/IslandWindow.xaml.cs` (paneli aç)
- Test: `tests/CamAda.Core.Tests/Calendar/MonthGridTests.cs`

**Interfaces:**
- Produces: `public sealed record CalendarDay(DateOnly Date, bool InMonth, bool IsToday)`; `public static class MonthGrid` → `IReadOnlyList<CalendarDay> Build(int year, int month, DateOnly today)` (her zaman 42 gün, ilk gün ayın 1'ini içeren haftanın pazartesisi), `string Title(int year, int month)` (`"Kasım 2026"`), `IReadOnlyList<string> WeekdayHeaders` (`Pzt Sal Çar Per Cum Cmt Paz`), `(int Year, int Month) Shift(int year, int month, int delta)`. `IslandBar.ClockClicked` olayı. `CalendarPanel(DateOnly today)`, `CalendarPanel.PanelWidth = 300`.

- [ ] **Step 1: Testleri yaz** — `MonthGridTests`:
  - `November_2026_grid_starts_on_monday_october_26`: `Build(2026, 11, new(2026, 11, 24))` → 42 gün; `[0]` 2026-10-26 `InMonth=false`; `[6]` 2026-11-01 `InMonth=true`; `[41]` 2026-12-06; yalnız `[29]` (2026-11-24) `IsToday`.
  - `Month_starting_on_monday_has_no_leading_days`: `Build(2026, 6, new(2026, 11, 24))[0]` == 2026-06-01, `InMonth`.
  - `Today_outside_the_grid_is_not_marked`: `Build(2027, 1, new(2026, 11, 24))` → hiçbiri `IsToday`.
  - `Shift_wraps_years`: `Shift(2026, 12, 1)` == (2027, 1); `Shift(2027, 1, -1)` == (2026, 12); `Shift(2026, 11, -13)` == (2025, 10).
  - `Titles_and_headers_are_turkish`: `Title(2026, 11)` == `"Kasım 2026"`; `WeekdayHeaders` == `["Pzt","Sal","Çar","Per","Cum","Cmt","Paz"]`.
- [ ] **Step 2: Çalıştır** — `dotnet test CamAda.slnx --nologo --filter MonthGridTests` → Expected: derleme hatası (`MonthGrid` yok).
- [ ] **Step 3: `MonthGrid`'i yaz** (`CamAda.Core.Calendar`; `tr-TR` kültürü, `ClockFormatter` ile aynı).
- [ ] **Step 4: Çalıştır** → Expected: 5 test PASS.
- [ ] **Step 5: Saat düğmesi** — `IslandBar.xaml`'daki `TimeText`, `IslandIconButton` stilinde `ClockButton`'ın içine alınır (`AutomationProperties.Name="Saat ve takvim"`, yükseklik 34, iç kenar 8,0); `ClockClicked` olayı. `IslandWindow`: `ExpandedContent.ClockClicked += … _panels.Toggle("calendar", () => new CalendarPanel(DateOnly.FromDateTime(DateTime.Now)), CalendarPanel.PanelWidth)`.
- [ ] **Step 6: `CalendarPanel`** — üstte `‹  Kasım 2026  ›` (önceki/sonraki ay düğmeleri `PanelButton` stilinde, adları "Önceki ay"/"Sonraki ay"; başlık 14 kalın), altında gün başlıkları (11,5, `TextSecondaryBrush`), 7×6 `UniformGrid`, hücre 36×32 DIP: ay dışı günler %40 opak; bugün 28 DIP `AccentBrush` daire içinde `OnAccentBrush` kalın metin. Panel hep bugünün ayıyla açılır; ay değişince ızgara `Build` ile yeniden kurulur. Etkinlik yok (spec §7.4).
- [ ] **Step 7: Derle, test et, kur, canlı dene** — `dotnet build CamAda.slnx --nologo` → `0 Uyarı`; `dotnet test CamAda.slnx --nologo` → `Başarısız: 0`; `powershell -File tools/install.ps1`. UIA ile: genişlemiş adada "Saat ve takvim" Invoke → panel başlığı "Kasım 2026", 24 vurgulu (ekran görüntüsü); "Sonraki ay" → "Aralık 2026", 1 Aralık salı sütununda; ikinci "Saat ve takvim" Invoke paneli kapatır.
- [ ] **Step 8: Commit** — `feat: saate tıklayınca takvim`

### Task 2: Ses servisi, ses düğmesi ve Ses panelinin ana bölümü

**Files:**
- Create: `src/CamAda.Core/Audio/SpeakerLevel.cs`, `src/CamAda.Core/Audio/AudioOutput.cs`, `src/CamAda/Audio/AudioService.cs`, `src/CamAda/Audio/PolicyConfig.cs`, `src/CamAda/Audio/SpeakerGlyph.cs`, `src/CamAda/Panels/SoundPanel.xaml(.cs)`
- Modify: `src/CamAda/CamAda.csproj` (NAudio.Wasapi 2.2.1), `src/CamAda.Core/Island/IslandLayout.cs`, `src/CamAda/Island/IslandBar.xaml(.cs)`, `src/CamAda/Island/IslandWindow.xaml.cs`, `src/CamAda/App.xaml.cs`, `src/CamAda/Theme/Panels.xaml` (`PanelSlider`), `src/CamAda/Theme/Glass.xaml` (`WarningBrush`)
- Test: `tests/CamAda.Core.Tests/Audio/SpeakerLevelTests.cs`, `tests/CamAda.Core.Tests/Apps/DockOverflowTests.cs` (`IslandLayoutTests`)

**Interfaces:**
- Produces (Core): `enum SpeakerLevel { Muted, Silent, Low, Medium, High }`; `static class SpeakerLevels` → `SpeakerLevel For(float volume, bool muted)`, `int Percent(float volume)`; `sealed record AudioOutput(string Id, string Name, bool IsDefault)`; `IslandLayout.SystemSectionWidth = 36`.
- Produces (app): `AudioService(Dispatcher dispatcher)`: `bool HasOutput`, `float Volume`, `bool Muted`, `void SetVolume(float)`, `void SetMuted(bool)`, `IReadOnlyList<AudioOutput> Outputs()`, `void SetDefaultOutput(string id)`, `event EventHandler<MasterVolumeChange> MasterChanged`, `event EventHandler DevicesChanged` (olaylar UI iş parçacığında); `sealed record MasterVolumeChange(float Volume, bool Muted, bool FromCamAda)`; `static class SpeakerGlyph { string For(SpeakerLevel) }`; `IslandBar.SoundClicked`, `IslandBar.ShowSound(SpeakerLevel level, bool micMuted)`; `SoundPanel(AudioService audio)`, `SoundPanel.PanelWidth = 360`. Görev 3 `AudioService`'e uygulama ve mikrofon üyelerini, `SoundPanel`'e iki bölüm ekler.

- [ ] **Step 1: Testleri yaz**
  - `SpeakerLevelTests.Level_follows_volume_and_mute` (`[Theory]`): (0.5, true)→`Muted`; (0, false)→`Silent`; (0.004, false)→`Silent`; (0.2, false)→`Low`; (0.34, false)→`Medium`; (0.5, false)→`Medium`; (0.67, false)→`High`; (1, false)→`High`.
  - `SpeakerLevelTests.Percent_rounds_half_up_and_clamps`: 0.125→13; 0.555→56; 1.2→100; -0.1→0; 0.996→100.
  - `IslandLayoutTests.Expanded_width_grows_with_app_buttons` verileri ses düğmesiyle güncellenir: (0, 956), (5, 956), (6, 994), (11, 1184), (30, 1250).
- [ ] **Step 2: Çalıştır** → Expected: derleme hatası (`SpeakerLevels` yok). Genişlik testinin kırmızısı ayrıca görülür: `SpeakerLevelTests` geçici olarak dışarıda bırakılıp çalıştırılınca `Expected: 956, Actual: 920`.
- [ ] **Step 3: Core'u yaz** — eşikler: `volume < 0.005` Silent, `< 0.34` Low, `< 0.67` Medium; yüzde `Math.Round(v*100, MidpointRounding.AwayFromZero)` 0–100'e kırpılır. `ExpandedWidthFor` = `min(1250, 770 + 150 + 36 + max(0, n−5)×38)`.
- [ ] **Step 4: Çalıştır** → Expected: PASS.
- [ ] **Step 5: `PolicyConfig`** — `[ComImport]` sınıf CLSID `870af99c-171d-4f9e-af0d-e63df40c2bc9`, arayüz IID `f8679f50-850a-41cf-9c72-430f290290c8`, yöntem sırası: GetMixFormat, GetDeviceFormat, ResetDeviceFormat, SetDeviceFormat, GetProcessingPeriod, SetProcessingPeriod, GetShareMode, SetShareMode, GetPropertyValue, SetPropertyValue, **SetDefaultEndpoint(string deviceId, ERole role)**, SetEndpointVisibility. Yalnız `SetDefaultEndpoint` kullanılır; diğerleri yer tutucu.
- [ ] **Step 6: `AudioService`** — `MMDeviceEnumerator` + `IMMNotificationClient` (varsayılan değişti / cihaz eklendi-kaldırıldı-durumu değişti → `DevicesChanged`, varsayılan çıkışın `AudioEndpointVolume` aboneliği eski cihazdan alınıp yenisine taşınır). Geri çağrılar içinde Core Audio çağrılmaz; her şey `Dispatcher.BeginInvoke` ile UI'da yapılır. CamAda'nın yaptığı değişiklikler sabit bir olay bağlamı Guid'iyle yazılır (NAudio `AudioEndpointVolume.NotificationGuid`), bildirimde `EventContext` bu Guid ise `FromCamAda = true` (NAudio sürümünde bu özellik yoksa: Ruling + son 300 ms içinde aynı değeri CamAda yazdıysa `FromCamAda`). Cihaz yoksa `HasOutput=false`, üyeler 0/false döner, ayarlayıcılar bir şey yapmaz. `Outputs()`: etkin çıkış cihazları, `FriendlyName`, varsayılan işaretli. `Dispose` tüm abonelikleri bırakır; `App.ExitGracefully`'de çağrılır.
- [ ] **Step 7: Ses düğmesi** — `IslandBar`'da `TrayButton`'dan sonra `SoundButton` (36×34, `AutomationProperties.Name="Ses ve mikrofon"`): ortada hoparlör glifi (15), sağ üstte 11'lik `EC54` `WarningBrush` rozet (mikrofon kapalıyken; Görev 3 bağlar). `ShowSound(level, micMuted)` glifi ve rozeti günceller. App, `MasterChanged`/`DevicesChanged`'de çağırır.
- [ ] **Step 8: `SoundPanel` ana bölüm** — üst satır: hoparlör düğmesi (adı "Sesi kapat"/"Sesi aç", `SetMuted`), `PanelSlider` (0–100, adı "Ses düzeyi"; sürüklerken `SetVolume`), sağda `%NN` (sessizken "Sessiz"). "Çıkış" başlığı (11,5 kalın, ikincil) + her cihaz için `PanelRowButton`: ad + varsayılanda sağda `E73E` vurgu rengi; tıklayınca `SetDefaultOutput`. `PanelSlider` stili: iz 4 DIP `#33FFFFFF`, dolu kısım `AccentBrush`, başparmak 14 DIP `#F4F6FB` daire, klavyeyle 5'er adım. Panel servis olaylarına `Loaded`'da abone olur, `Unloaded`'da bırakır; sürüklenen kaydırıcı dışarıdan gelen değerle güncellenmez.
- [ ] **Step 9: Derle, test et, kur, canlı dene** — build/test/install yukarıdaki gibi. Canlı: önce ses düzeyi, sessiz durumu ve varsayılan cihaz kaydedilir. UIA ile "Ses ve mikrofon" Invoke → panel; kaydırıcıyı `RangeValuePattern` ile 30'a al → Windows ses düzeyi (`AudioEndpointVolume` okuması) 0,30, düğme glifi `E993`; "Sesi kapat" → glif `E74F`; birden fazla çıkış cihazı varsa diğerini seç → Windows varsayılanı değişir, panel işareti taşınır, sonra eskisine dön (Review Focus 1). Son: kaydedilen düzey/sessiz/cihaz geri yüklenir.
- [ ] **Step 10: Commit** — `feat: ses düğmesi ve Ses paneli`

### Task 3: Uygulama bazında ses ve mikrofon

**Files:**
- Create: `src/CamAda.Core/Audio/AppVolumeGrouping.cs`, `src/CamAda.Core/Audio/MicState.cs`
- Modify: `src/CamAda/Audio/AudioService.cs`, `src/CamAda/Panels/SoundPanel.xaml(.cs)`, `src/CamAda/Island/IslandWindow.xaml(.cs)` (küçük adada işaret), `src/CamAda/Island/IslandBar.xaml.cs`, `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Audio/AppVolumeGroupingTests.cs`, `tests/CamAda.Core.Tests/Audio/MicStateTests.cs`

**Interfaces:**
- Consumes: Görev 2'nin `AudioService`, `SoundPanel`, `IslandBar.ShowSound`.
- Produces (Core): `sealed record SessionSample(int ProcessId, string ExeName, string DisplayName, float Volume, bool Muted, bool Active, bool IsSystemSounds)`; `sealed record AppVolumeRow(string Key, string Name, IReadOnlyList<int> ProcessIds, float Volume, bool Muted)`; `static class AppVolumeGrouping { IReadOnlyList<AppVolumeRow> Group(IEnumerable<SessionSample>) }`; `enum MicState { Missing, On, Off }`, `static class MicStates { MicState From(bool hasDevice, bool consoleMuted) }`.
- Produces (app): `AudioService.Apps()`, `SetAppVolume(AppVolumeRow row, float volume)`, `MicState Mic`, `string? MicName`, `void ToggleMic()`, `event EventHandler MicChanged`.

- [ ] **Step 1: Testleri yaz**
  - `AppVolumeGroupingTests.Inactive_and_system_sounds_are_hidden`: etkin olmayan ve `IsSystemSounds` örnekler satır üretmez.
  - `…Sessions_of_one_app_are_one_row`: `chrome.exe` 3 süreç (ses 0.2/0.8/0.5) → tek satır, `ProcessIds.Count == 3`, `Volume == 0.8`, `Key == "chrome.exe"` (büyük/küçük harf duyarsız: `Chrome.EXE` aynı satır).
  - `…Name_falls_back_to_exe_name`: `DisplayName` boş, `ExeName` `"discord.exe"` → `Name == "discord"`; dolu `DisplayName` öncelikli.
  - `…Muted_only_when_every_session_is_muted` ve `…Rows_are_sorted_by_name` (`tr-TR` karşılaştırma: "çalar" "zil"den önce).
  - `MicStateTests`: (false, *)→`Missing`; (true, true)→`Off`; (true, false)→`On`.
- [ ] **Step 2: Çalıştır** → Expected: derleme hatası.
- [ ] **Step 3: Core'u yaz.** **Step 4: Çalıştır** → Expected: PASS.
- [ ] **Step 5: Servis** — `Apps()`: varsayılan çıkışın oturumları (`AudioSessionManager.RefreshSessions()`), `ExeName` süreç yolundan (erişim reddinde `Process.ProcessName + ".exe"`), boş `DisplayName` yerine exe'nin `FileDescription`'ı, sonra `AppVolumeGrouping.Group`. `SetAppVolume` satırdaki tüm süreçlerin oturumlarına uygular. Mikrofon: konsol ve iletişim varsayılan kayıt cihazlarının `AudioEndpointVolume` aboneliği (olay → `MicChanged`); `ToggleMic` Global Constraints'teki kurala göre; cihaz yoksa bir şey yapmaz.
- [ ] **Step 6: Panel** — "Uygulamalar" bölümü: satır = 20 DIP uygulama simgesi (`IconImageConverter.GetImageFromAssociatedIcon(exe, IconSize.Small)`, yoksa `E767` glifi), ad (kırpılır), `PanelSlider`; satır yoksa "Şu an ses çıkaran uygulama yok". Panel açıkken saniyede bir `Apps()` ile satırlar **anahtara göre yerinde** güncellenir (yeni satır eklenir, kalkan silinir, sürüklenen ya da klavye odağındaki kaydırıcıya dokunulmaz). "Mikrofon" bölümü: `E720` + `MicName` + düğme "Mikrofonu kapat" / kapalıyken vurgu zeminli "Mikrofonu aç"; `Missing`'de yalnız "Mikrofon bulunamadı".
- [ ] **Step 7: İşaretler** — küçük adada `CompactEqualizer`'ın soluna `CompactMicOff` (`EC54`, 13, `WarningBrush`, sağ boşluk 10, adı "Mikrofon kapalı"), yalnız `Mic == Off` iken görünür; `IslandBar.ShowSound(level, micMuted: Mic == Off)`. App `MicChanged`'de ikisini günceller.
- [ ] **Step 8: Derle, test et, kur, canlı dene** — Canlı: scratchpad'de sessiz WAV döngüsü çalan deneme exe'si (`camaudioN.exe`, iki kopya iki süreç) çalıştırılır → panelde "Uygulamalar"da satırları görünür; bir satırın kaydırıcısı `RangeValuePattern` ile 40 → o sürecin oturum düzeyi 0,40; kaydırıcıya klavye odağı verilip bekletilirken liste yenilenir, değer değişmez (Review Focus 5); deneme exe'si kapatılınca satır ≤1 sn'de kalkar. Mikrofon: durum kaydedilir, "Mikrofonu kapat" → Windows'ta varsayılan kayıt cihazı sessiz, küçük adada işaret ve düğmede rozet görünür; "Mikrofonu aç" → kalkar; durum geri yüklenir.
- [ ] **Step 9: Commit** — `feat: uygulama bazında ses ve mikrofon`

### Task 4: Ses değişince ada içinde gösterge

**Files:**
- Create: `src/CamAda/Island/FlashContent.cs`
- Modify: `src/CamAda.Core/Island/IslandPresence.cs`, `src/CamAda/Island/IslandWindow.xaml(.cs)`, `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Island/SmartHideTests.cs` (mevcut `IslandPresenceRule.Decide` testi burada)

**Interfaces:**
- Consumes: `AudioService.MasterChanged`, `SpeakerLevels`, `SpeakerGlyph`.
- Produces: `IslandPresenceRule.Decide(bool fullScreen, bool occluded, bool expanded, bool flashing)`; `sealed record FlashContent(string Glyph, double? Level, string Text)`; `IslandWindow.Flash(FlashContent content)`. Görev 5 `Flash`'ı kullanır.

- [ ] **Step 1: Testleri yaz** — `Flash_shows_a_tucked_island`: `Decide(false, true, false, true)` == `Shown`; `Flash_never_shows_over_full_screen`: `Decide(true, false, false, true)` == `Hidden`; mevcut testler yeni parametreyle `flashing: false` olarak güncellenir.
- [ ] **Step 2: Çalıştır** → Expected: derleme hatası (4 parametreli `Decide` yok).
- [ ] **Step 3: Kuralı güncelle** — `fullScreen → Hidden; occluded && !expanded && !flashing → Tucked; değilse Shown`. **Step 4: Çalıştır** → PASS.
- [ ] **Step 5: `Flash`** — küçük hapın içinde `CompactContent`'in yerine geçen satır: solda glif (15), ortada 200×4 DIP iz (`#33FFFFFF`) ve `Level` oranında `AccentBrush` dolgu (`Level` null ise iz yok), sağda metin (12,5 kalın). Son `Flash` çağrısından **1,5 sn** sonra kaybolur (her çağrı süreyi yeniler); belirip kaybolma 0,15 sn (animasyonları azalt açıksa anında). Ada genişlemişken ya da panel açıkken `Flash` hiçbir şey yapmaz. Gösterim sürerken `ApplyPresence` `flashing: true` ile karar verir: çizgiye inmiş ada gösterge boyunca küçük hâliyle görünür, sonra yeniden iner; tam ekranda görünmez.
- [ ] **Step 6: Bağla** — App: `MasterChanged`'de `FromCamAda == false` ise `Flash(new(SpeakerGlyph.For(level), volume, muted ? "Sessiz" : $"%{SpeakerLevels.Percent(volume)}"))`.
- [ ] **Step 7: Ölç — Windows'un kendi ses penceresi** — görev çubuğu gizliyken ses tuşuyla (deneme: `keybd_event(VK_VOLUME_UP)` ve hemen `VK_VOLUME_DOWN`, düzey eski hâline döner) Windows'un ses penceresi çıkıyor mu, ekran görüntüsüyle bakılır. Çıkıyorsa ve Explorer'a enjeksiyonsuz, yalnız pencereyi bulup gizleyerek kapatılabiliyorsa (`FindWindow`/`ShowWindow`; kalıcı bir sistem ayarı değiştirilmez) uygulanır; olmuyorsa ikisi birlikte görünür. Her iki sonuç da deftere Ruling olarak yazılır ve kullanıcıya bildirilir.
- [ ] **Step 8: Derle, test et, kur, canlı dene** — Canlı: ses tuşu (yukarıdaki gibi) → küçük adada gösterge, `%NN` Windows düzeyiyle aynı; 1,5 sn sonra saat/müzik geri gelir. Panel kaydırıcısıyla değiştirince gösterge çıkmaz (Review Focus 3). Adanın yeri bir deneme penceresiyle örtülüyken ses tuşu → ada görünür, sonra çizgiye iner. Düzey geri yüklenir.
- [ ] **Step 9: Commit** — `feat: ses değişince adada gösterge`

### Task 5: Ctrl+Shift+M kısayolu

**Files:**
- Create: `src/CamAda.Core/Input/Hotkey.cs`, `src/CamAda.Core/Settings/HotkeySettings.cs`, `src/CamAda/Input/GlobalHotkeys.cs`
- Modify: `src/CamAda.Core/Settings/AppSettings.cs`, `src/CamAda.Core/Settings/SettingsStore.cs` (`Sanitize`), `src/CamAda/Interop/Native.Input.cs` (`RegisterHotKey`/`UnregisterHotKey`), `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Input/HotkeyTests.cs`, `tests/CamAda.Core.Tests/Settings/SettingsStoreTests.cs`

**Interfaces:**
- Consumes: `AudioService.ToggleMic`, `AudioService.Mic`, `IslandWindow.Flash`.
- Produces: `[Flags] enum HotkeyModifiers { None = 0, Alt = 1, Control = 2, Shift = 4, Win = 8 }` (değerler `RegisterHotKey`'in MOD_* değerleriyle aynı); `sealed record Hotkey(HotkeyModifiers Modifiers, int VirtualKey)` + `static Hotkey? TryParse(string? text)` + `ToString()`; `sealed record HotkeySettings { string MicToggle { get; init; } = "Ctrl+Shift+M"; }`; `AppSettings.Hotkeys`; `GlobalHotkeys : IDisposable` → `bool Register(Hotkey hotkey, Action action)`. Plan 4.2 aynı sınıfla Ctrl+Shift+V'yi ekler.

- [ ] **Step 1: Testleri yaz**
  - `HotkeyTests.Parses_modifiers_and_key` (`[Theory]`): `"Ctrl+Shift+M"` → (Control|Shift, 0x4D); `" ctrl + shift + m "` aynı; `"Alt+F13"` → (Alt, 0x7C); `"Win+Shift+7"` → (Win|Shift, 0x37).
  - `HotkeyTests.Rejects_invalid` (`[Theory]`): `null`, `""`, `"Shift"`, `"M"` (değiştiricisiz), `"Ctrl+Shift+M+K"`, `"Ctrl+Foo"` → `null`.
  - `HotkeyTests.ToString_is_canonical`: `TryParse("shift+ctrl+m")!.ToString()` == `"Ctrl+Shift+M"`.
  - `SettingsStoreTests.Hotkeys_default_and_invalid_falls_back`: dosyada `Hotkeys` yok → `"Ctrl+Shift+M"`; `{"Hotkeys":{"MicToggle":"Foo"}}` → `"Ctrl+Shift+M"`; `"Alt+F13"` korunur.
- [ ] **Step 2: Çalıştır** → Expected: derleme hatası.
- [ ] **Step 3: Core'u yaz** — tuşlar: A–Z, 0–9, F1–F24; ayraç `+`, boşluklar yok sayılır, büyük/küçük harf duyarsız; en az bir değiştirici şart. **Step 4: Çalıştır** → PASS.
- [ ] **Step 5: `GlobalHotkeys`** — `HwndSource` ile mesaj penceresi (`HWND_MESSAGE`), `RegisterHotKey(hwnd, id, mods | MOD_NOREPEAT, vk)`, `WM_HOTKEY` (0x0312) kimliğe göre eylemi çağırır; `Dispose` hepsini bırakır (`ExitGracefully`'de).
- [ ] **Step 6: Bağla** — App açılışta `Hotkeys.MicToggle`'ı kaydeder. Basınca: `ToggleMic()`, sonra `Flash` — `Off`: (`EC54`, null, "Mikrofon kapalı"); `On`: (`E720`, null, "Mikrofon açık"); `Missing`: (`EC54`, null, "Mikrofon bulunamadı"). Kayıt başarısızsa açılışta bir kez `Flash(E720, null, "<kısayol> kullanılamıyor")`. Not: Discord'un kendi Ctrl+Shift+M'si yalnız Discord öndeyken çalışır; genel kısayol onu da kapsar (sistem mikrofonu kapanır).
- [ ] **Step 7: Derle, test et, kur, canlı dene** — Canlı: mikrofon durumu kaydedilir; Ctrl+Shift+M `keybd_event` ile (odak deneme penceresindeyken, terminalde değil) → varsayılan kayıt cihazı sessiz + adada "Mikrofon kapalı" + küçük adada işaret; tekrar → açık; durum geri yüklenir. Review Focus 4: Ctrl+Shift+M'yi önce bir deneme exe'sine `RegisterHotKey` ile kaydettirip CamAda'yı yeniden başlat → "Ctrl+Shift+M kullanılamıyor" bir kez görünür, CamAda çalışır; deneme exe'si kapatılır, CamAda yeniden başlatılır.
- [ ] **Step 8: Commit** — `feat: Ctrl+Shift+M ile mikrofonu kapat/aç`
