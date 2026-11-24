# Plan 4.2: Pano ve Ekran Görüntüleri — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Genişlemiş adaya pano düğmesi ve Pano panelini (son kopyalananlar, sabitleme, ekran görüntüsü şeridi), her yerden açan ve seçileni önceki pencereye yapıştıran Win+Alt+V'yi ve yeni ekran görüntüsünde adada tıklanabilir önizlemeyi eklemek.

**Architecture:** Pano geçmişi saf bir `ClipboardHistory` modelinde (`CamAda.Core`, birim testli); `ClipboardService` Windows pano dinleyicisiyle (`AddClipboardFormatListener`) değişiklikleri UI iş parçacığında okuyup modele verir, sabitlenenleri `PinnedClipStore` ile diske yazar. Yapıştırma `PasteBack`'te: kısayol anındaki ön pencere hatırlanır, seçimden sonra öne alınıp tuşlar bırakılınca `SendInput` ile Ctrl+V gönderilir; ne yapılacağına saf `PasteRule` karar verir. Ekran görüntüleri `ScreenshotLibrary` (FOLDERID_Screenshots + `FileSystemWatcher`) ve pano görseliyle eşleştirmeyi yapan saf `ScreenshotTracker`'dan gelir.

**Tech Stack:** .NET 10, C#, WPF, ManagedShell 0.0.374, NAudio.Wasapi 2.2.1, xUnit + Microsoft.Extensions.TimeProvider.Testing. Yeni paket yok.

**Spec:** `docs/superpowers/specs/2026-11-24-pano-ve-ekran-goruntuleri-design.md` (bu parçanın kararları; çelişkide bu geçerli) ve `docs/superpowers/specs/2026-11-23-cam-ada-design.md` §7.2, §7.4 "Pano", §7.5, §7.9, §8, §10.

## Yol haritası (Plan 4, 2/4)

Plan 4.1 main'de (78159e6). Bu plan `plan-4-2-pano` dalında; tasarım commit'i 2aa0b1c. Sonra: 4.3 hızlı ayarlar (+ parlaklık), 4.4 bildirimler.

## Global Constraints

- Plan 1–4.1'in kısıtları geçerli: Windows 11 build 26300; `CamAda.Core` net10.0 ve WPF'siz; Explorer'a enjeksiyon yok; arayüz Türkçe; yazı Manrope, simgeler `Segoe Fluent Icons, Segoe MDL2 Assets`; derleme uyarısız; commit sonu `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Simgeler: pano `E77F`, metin `E8A5`, bağlantı `E71B`, kod `E943`, sabitle `E718`, sabitli `E840`, sil `E711`, kopyala `E8C8`, düzenle `E70F`, klasör `E838`.
- Kısayol: varsayılan **Win+Alt+V**, ayar `Hotkeys.ClipboardOpen`; geçersiz → varsayılan. Win+V'ye dokunulmaz.
- Sabitlenmemiş en fazla **30** öğe, tek öğe en fazla **16 MB** (saklanan biçimde); sabitlenmemişler **asla diske yazılmaz**. Sabitlenenler `%APPDATA%\CamAda\clipboard\` altında: `pinned.json` + `<id>.png`.
- Geçmişe girmeyenler: `ExcludeClipboardContentFromMonitorProcessing` ya da `Clipboard Viewer Ignore` biçimi olan, `CanIncludeInClipboardHistory` değeri 0 olan kopyalar (içerik okunmadan, `IsClipboardFormatAvailable` ile elenir); boş/yalnız boşluk metin; dosya listeleri.
- Pano hiçbir zaman uzun açık tutulmaz: açılamazsa 50 ms arayla en çok 5 deneme, `DispatcherTimer` ile (UI iş parçacığı uyutulmaz). PNG kodlama ve piksel özeti (SHA-256, BGRA32) arka planda.
- CamAda'nın kendi pano yazımları dinleyicide yeni kopya sayılmaz: yazımdan hemen sonra `GetClipboardSequenceNumber()` saklanır, aynı sıra numaralı `WM_CLIPBOARDUPDATE` yok sayılır; geçmiş öğesinin yeniden kopyalanması modele `Touch` ile bildirilir.
- Ctrl+V yalnız hedef pencere öndeyken ve Win/Alt/Ctrl/Shift bırakılmışken gönderilir; olmazsa yalnız kopya + "Kopyalandı — Ctrl+V ile yapıştır" (tasarım "Olmaması gerekenler").
- Ekran görüntüsü önizlemesi ~5 sn, örtülüyken de tıklanabilir; tam ekranda, ada genişlemişken ya da panel açıkken çıkmaz. "Animasyonları azalt" açıksa geçişler anında.
- Canlı denemeler: kullanıcının pano metni deneme başında kaydedilip sonunda geri konur; yapıştırma ve kısayol yalnız scratchpad'de derlenen deneme penceresi (`camclipN.exe`) öndeyken; ekran görüntüsü denemesi gerçek Ekran Alıntısı Aracı yerine deneme exe'sinin Screenshots klasörüne yazdığı `camada-test-*.png` ile, deneme sonunda silinir. Otomatik yükseltilen uygulama açılmaz; ESC SendKeys ile gönderilmez.

## Review Focus

1. **Çok büyük ya da kilitli kopya** (20 MB metin, panoyu tutan uygulama) → CamAda donmaz, öğe listeye girmez, diğer uygulamanın kopyala/yapıştırı bozulmaz. (Görev 2 Step 8)
2. **Seçerken Win/Alt hâlâ basılı** → Ctrl+V tuşlar bırakılana kadar bekler; 2 sn içinde bırakılmazsa yalnız kopya + ipucu; hiçbir zaman Win+Ctrl+V / Alt+Ctrl+V gitmez. (Görev 3 Step 9)
3. **Panel açıkken hedef pencere kapanır** → seçim yalnız kopyalar, ipucu görünür, Ctrl+V başka pencereye gitmez. (Görev 3 Step 9)
4. **Şifre yöneticisinin işaretli kopyası** → listede, bellekte ve diskte yok; `CanIncludeInClipboardHistory = 1` olan normal kopya ise listede. (Görev 2 Step 8)
5. **Ekran görüntüsü klasörü yok ya da boş** → şeritte "Henüz ekran görüntüsü yok", izleyici kurulamazsa çökme yok; klasör sonradan oluşunca yeni görüntü yakalanır. (Görev 4 Step 8)

---

### Task 1: Pano geçmişi modeli ve sabitlenenlerin kaydı

**Files:**
- Create: `src/CamAda.Core/Clipboard/ClipItem.cs`, `src/CamAda.Core/Clipboard/ClipboardHistory.cs`, `src/CamAda.Core/Clipboard/ClipClassifier.cs`, `src/CamAda.Core/Clipboard/ClipAge.cs`, `src/CamAda.Core/Clipboard/PinnedClipStore.cs`
- Test: `tests/CamAda.Core.Tests/Clipboard/ClipboardHistoryTests.cs`, `ClipClassifierTests.cs`, `ClipAgeTests.cs`, `PinnedClipStoreTests.cs`

**Interfaces:**
- Produces: `enum ClipKind { Text, Link, Code, Image }`; `sealed record ClipPayload(string? Text, string? Html, string? Rtf, byte[]? Png, int Width, int Height, string? PixelHash)` + `static ClipPayload FromText(string text, string? html = null, string? rtf = null)`, `static ClipPayload FromImage(byte[] png, int width, int height, string pixelHash)`, `string Key` (`"t:" + Text` ya da `"i:" + PixelHash`), `long Size` (metinlerde karakter × 2 toplamı + PNG uzunluğu); `sealed record ClipItem(string Id, ClipKind Kind, ClipPayload Payload, DateTimeOffset CopiedAt, bool Pinned)` (Id = `Guid.NewGuid().ToString("N")`).
- `sealed class ClipboardHistory(IEnumerable<ClipItem> pinned)`: `const int MaxRecent = 30`, `const long MaxItemBytes = 16L * 1024 * 1024`, `IReadOnlyList<ClipItem> Pinned` (en son sabitlenen üstte), `IReadOnlyList<ClipItem> Recent` (en yeni üstte), `int PinnedVersion` (Pinned her değişince artar), `ClipItem? Add(ClipPayload payload, DateTimeOffset now)`, `ClipItem? Find(string id)`, `void Touch(string id, DateTimeOffset now)`, `void Pin(string id)`, `void Unpin(string id)`, `void Remove(string id)`, `void ClearUnpinned()`.
- `static class ClipClassifier { ClipKind Classify(string text) }`; `static class ClipAge { string Format(DateTimeOffset copiedAt, DateTimeOffset now) }`.
- `sealed class PinnedClipStore(string directory)`: `IReadOnlyList<ClipItem> Load()`, `void Save(IReadOnlyList<ClipItem> pinned)` (G/Ç hatasını yutmaz, çağıran yakalar).

- [ ] **Step 1: Testleri yaz**
  - `ClipboardHistoryTests`: `Adds_newest_first`; `Same_text_moves_to_top_without_new_row` (Id aynı, sayı aynı, `CopiedAt` ve `Html` en sonuncununki); `Same_image_pixels_are_one_row` (aynı `PixelHash`, farklı PNG baytları); `Keeps_30_recent_and_drops_oldest` (31. eklenince ilk eklenen yok; 2 sabitli + 30 son birlikte durur); `Rejects_blank_and_oversized` (`" \n\t"` → null; `Size == MaxItemBytes` kabul, +1 → null); `Pin_moves_to_pinned_and_unpin_returns_to_top_of_recent` (her ikisinde `PinnedVersion` artar); `Copying_a_pinned_text_again_keeps_it_pinned` (`Recent` değişmez, sabitlinin `CopiedAt`'ı güncellenir, `PinnedVersion` artar); `ClearUnpinned_keeps_pinned`; `Remove_deletes_from_either_list`; `Touch_moves_recent_item_to_top`.
  - `ClipClassifierTests` (`[Theory]`): `"https://github.com/virtus-xd"` → Link; `"  http://a.b/c?d=1  "` → Link; `"bkz. https://x.com"` → Text; `"https://a.com\nhttps://b.com"` → Text; `"public void Foo()\n{\n    Bar();\n}"` → Code; `"dotnet test CamAda.slnx --nologo"` → Code; `"git status"` → Code; `"x => x * 2;"` → Code; `"toplantı yarın 14:00'te"` → Text; `"Merhaba,\nyarın görüşürüz.\nSevgiler"` → Text.
  - `ClipAgeTests` (`now` = 2026-11-24 15:00 +03:00): −30 sn → `"şimdi"`; −2 dk → `"2 dk"`; −59 dk → `"59 dk"`; −3 sa → `"3 sa"`; aynı gün 01:00 → `"14 sa"`; 2026-11-23 23:00 → `"dün"`; 2026-11-12 10:00 → `"12 Kas"`.
  - `PinnedClipStoreTests` (geçici klasör): metin+HTML+RTF ve görsel (PNG baytları birebir) gidiş-dönüş; daha az öğeyle `Save` artık sabitli olmayan `<id>.png`'yi siler; bozuk `pinned.json` → boş liste; klasör yok → boş liste.
- [ ] **Step 2: Çalıştır** — `dotnet test CamAda.slnx --nologo --filter "FullyQualifiedName~Clipboard"` → Expected: derleme hatası (`ClipboardHistory` yok).
- [ ] **Step 3: Core'u yaz** — `Classify`: tek satır ve `^https?://\S+$` → Link; çok satırda boş olmayan satırların ≥⅓'ü girintili (sekme ya da 2+ boşluk) ya da `;`, `{`, `}` ile bitiyorsa → Code; tek satırda `=>`, `();`, `{…}` ya da komut başı (`git|dotnet|npm|npx|pip|python|cd|ls|cargo|winget|powershell|pwsh` + boşluk) → Code; değilse Text. `ClipAge`: `tr-TR`, `now`'ın ofsetinde gün karşılaştırması. `PinnedClipStore`: `System.Text.Json`, önce geçici dosyaya yazıp `File.Move(overwrite: true)`.
- [ ] **Step 4: Çalıştır** → Expected: hepsi PASS; `dotnet test CamAda.slnx --nologo` → `Başarısız: 0`.
- [ ] **Step 5: Commit** — `feat: pano geçmişi modeli`

### Task 2: Pano dinleyicisi, pano düğmesi ve Pano paneli

**Files:**
- Create: `src/CamAda/Interop/Native.Clipboard.cs`, `src/CamAda/Clipboard/ClipboardService.cs`, `src/CamAda/Panels/ClipboardPanel.xaml(.cs)`
- Modify: `src/CamAda.Core/Island/IslandLayout.cs`, `src/CamAda/Island/IslandBar.xaml(.cs)`, `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Apps/DockOverflowTests.cs` (`IslandLayoutTests`)

**Interfaces:**
- Consumes: Görev 1'in tümü.
- Produces: `abstract record ClipChoice`; `sealed record HistoryChoice(string ItemId) : ClipChoice`; `sealed record ScreenshotChoice(string Path) : ClipChoice` (Görev 4 kullanır). `ClipboardService(Dispatcher dispatcher, string pinnedDirectory) : IDisposable`: `ClipboardHistory History`, `event EventHandler Changed` (UI'da), `bool Put(ClipChoice choice)` (panoya yazar, başarıyı döner; `HistoryChoice`'ta `Touch`), `void Pin(string id)`, `void Unpin(string id)`, `void Remove(string id)`, `void ClearUnpinned()` (bu görevde her görsel doğrudan geçmişe eklenir; Görev 4 araya eşleştirme koyar). `IslandBar.ClipboardClicked`. `ClipboardPanel(ClipboardService clips, bool pasteOnChoose)`, `ClipboardPanel.PanelWidth = 380`, `event EventHandler<ClipChoice> Chosen`. `IslandLayout.SystemSectionWidth = 72`.

- [ ] **Step 1: Genişlik testini güncelle** — `Expanded_width_grows_with_app_buttons`: (0, 992), (5, 992), (6, 1030), (11, 1220), (30, 1250).
- [ ] **Step 2: Çalıştır** → Expected: FAIL `Expected: 992, Actual: 956`.
- [ ] **Step 3: `SystemSectionWidth = 72`** (ses + pano), yorumu güncelle. **Step 4: Çalıştır** → PASS.
- [ ] **Step 5: `ClipboardService`** — `HwndSource` mesaj penceresi + `AddClipboardFormatListener`; `WM_CLIPBOARDUPDATE` (0x031D): kendi sıra numarasıysa çık → dışlama biçimleri (Global Constraints) → `Clipboard.GetDataObject()` yeniden denemeli okuma → metin (`UnicodeText` + varsa `Html`, `Rtf`) ya da görsel (`"PNG"` akışı varsa o, yoksa `Clipboard.GetImage()`; dondurulup arka planda PNG + piksel özeti) → `History.Add` → `Changed`. `FileDrop` içeren kopya atlanır. Sabitliler açılışta `PinnedClipStore.Load`, `PinnedVersion` değişince `Save` (hata `LogCrash`'e, çökme yok). `Put`: `DataObject` (metin + Html + Rtf ya da `SetImage` + `"PNG"`), `Clipboard.SetDataObject(obj, copy: true)` yeniden denemeli, sonra sıra numarası saklanır. `Dispose`: `RemoveClipboardFormatListener`; `App.ExitGracefully`'de.
- [ ] **Step 6: Pano düğmesi** — `IslandBar.xaml`'da `TrayButton` ile `SoundButton` arasında `ClipboardButton` (36×34, glif `E77F` 15, `AutomationProperties.Name="Pano"`), `ClipboardClicked`. App: `_island.Panels.Toggle("clipboard", () => CreateClipboardPanel(pasteOnChoose: false), ClipboardPanel.PanelWidth)`.
- [ ] **Step 7: `ClipboardPanel`** — tasarımdaki çizim: başlık "Pano" (14 kalın) + sağda "Sabitlenmeyenleri sil" (`PanelButton`, liste boşsa gizli); sabitli bölüm + ayraç + son bölüm, en çok 420 DIP yükseklikte kayar. Satır (`PanelRowButton`): solda tür glifi ya da görselde 40×28 küçük resim, ortada önizleme (metin 2 satır üç noktalı, kod eşit aralıklı `Cascadia Mono, Consolas`, görsel "Görsel 1280×720"), sağda `ClipAge` (11,5 ikincil) ve üzerine gelince sabitle (`E718`/sabitliyse `E840` vurgu) ve sil (`E711`) küçük düğmeleri (adları "Sabitle"/"Sabitlemeyi kaldır"/"Sil"). Boşsa "Kopyaladıkların burada görünür." Klavye: açılınca ilk satır odakta; ↑/↓, Enter seçer, Delete siler. Seçim → `Chosen`; `pasteOnChoose == false` ise panel kendisi `clips.Put` çağırır ve satırda 1 sn "Kopyalandı" (`E73E` vurgu) gösterir, açık kalır. `Changed`'e `Loaded`'da abone, `Unloaded`'da bırakır.
- [ ] **Step 8: Derle, test et, kur, canlı dene** — `dotnet build CamAda.slnx --nologo` → `0 Uyarı`; `dotnet test CamAda.slnx --nologo` → `Başarısız: 0`; `powershell -File tools/install.ps1`. Önce kullanıcının pano metni kaydedilir. Deneme exe'si (`camclip1.exe`) ile panoya sırayla: düz metin, bağlantı, kod, görsel (100×60 düz renk), `ExcludeClipboardContentFromMonitorProcessing` biçimli metin, `CanIncludeInClipboardHistory`=0 metin, =1 metin, 20 MB metin (Review Focus 1, 4). UIA ile "Pano" Invoke (ya da gerçek tıklama) → panel: beklenen 5 satır (dışlananlar ve 20 MB yok), türler doğru, ekran görüntüsü alınır. Bir satırı sabitle → `%APPDATA%\CamAda\clipboard\pinned.json`'da var; CamAda'yı menüdeki "Çıkış"la kapat-aç → yalnız sabitli kalır. Satıra tıkla → panoda o metin, "Kopyalandı", satır en üstte, yeni satır yok. "Sabitlenmeyenleri sil" → yalnız sabitli kalır. 20 MB kopya sırasında ada tepki vermeye devam eder. Deneme sonu: sabitli silinir, kullanıcının pano metni geri konur.
- [ ] **Step 9: Commit** — `feat: pano düğmesi ve Pano paneli`

### Task 3: Win+Alt+V ve önceki pencereye yapıştırma

**Files:**
- Create: `src/CamAda.Core/Clipboard/PasteRule.cs`, `src/CamAda/Clipboard/PasteBack.cs`
- Modify: `src/CamAda.Core/Input/Hotkey.cs` (yazım sırası), `src/CamAda.Core/Settings/HotkeySettings.cs`, `src/CamAda.Core/Settings/SettingsStore.cs` (`Sanitize`), `src/CamAda.Core/Island/HoverIntent.cs`, `src/CamAda/Interop/Native.Input.cs`, `src/CamAda/Panels/PanelHost.cs`, `src/CamAda/Island/IslandWindow.xaml.cs`, `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Clipboard/PasteRuleTests.cs`, `tests/CamAda.Core.Tests/Input/HotkeyTests.cs`, `tests/CamAda.Core.Tests/Settings/SettingsStoreTests.cs`, `tests/CamAda.Core.Tests/Island/HoverIntentTests.cs`

**Interfaces:**
- Consumes: `ClipboardService.Put`, `ClipboardPanel(clips, pasteOnChoose)`, `ClipboardPanel.Chosen`, `GlobalHotkeys.Register`, `IslandWindow.Flash`.
- Produces: `sealed record PasteTarget(bool Exists, bool IsShellOrSelf, bool Elevated)`; `enum PasteOutcome { CopyOnly, Paste, CopyWithHint }`; `static class PasteRule { PasteOutcome Decide(bool openedByHotkey, PasteTarget? target) }`; `HotkeySettings.ClipboardOpen` (`DefaultClipboardOpen = "Win+Alt+V"`); `HoverIntent.ExpandNow()`; `IslandWindow.ExpandForKeyboard()`; `PanelHost.IsOpenWith(object key)`; `PasteBack`: `static nint CaptureForeground()`, `static PasteTarget Describe(nint hwnd, int ownProcessId)`, `Task<bool> PasteIntoAsync(nint hwnd)` (Ctrl+V gönderildiyse true).

- [ ] **Step 1: Testleri yaz**
  - `PasteRuleTests`: düğmeyle (`false`, herhangi) → CopyOnly; kısayolla `null` hedef → CopyOnly; `IsShellOrSelf` → CopyOnly; `Exists=false` → CopyWithHint; `Elevated` → CopyWithHint; (true, true, false, false) → Paste.
  - `HotkeyTests.Win_comes_first`: `TryParse("alt+win+v")!.ToString()` == `"Win+Alt+V"`; mevcut `"Ctrl+Shift+M"` testi aynen geçer.
  - `SettingsStoreTests.Clipboard_hotkey_default_and_invalid_falls_back`: yok → `"Win+Alt+V"`; `"Foo"` → `"Win+Alt+V"`; `"Ctrl+Shift+V"` korunur.
  - `HoverIntentTests.ExpandNow_expands_immediately_and_closing_panel_outside_collapses`: `ExpandNow()` → anında `IsExpanded` + olay; `SetPanelOpen(true)`, `SetPanelOpen(false)` (imleç dışarıda) → küçülür.
- [ ] **Step 2: Çalıştır** → Expected: derleme hatası (`PasteRule`, `ClipboardOpen`, `ExpandNow` yok).
- [ ] **Step 3: Core'u yaz** — `Hotkey.ToString` sırası Win, Ctrl, Alt, Shift. `ExpandNow`: bekleyeni iptal, genişlemişse bir şey yapma. **Step 4: Çalıştır** → PASS.
- [ ] **Step 5: `PasteBack`** — `Native.Input.cs`'e `GetForegroundWindow`, `SetForegroundWindow`, `GetAsyncKeyState`, `SendInput` (+ `INPUT`/`KEYBDINPUT`), `OpenProcess`/`OpenProcessToken`/`GetTokenInformation(TokenElevation)`. `Describe`: pencere yoksa `Exists=false`; sınıfı `Progman`/`WorkerW` ya da süreci CamAda ise `IsShellOrSelf`; belirteç açılamazsa `Elevated=true`. `PasteIntoAsync`: `SetForegroundWindow` → 500 ms'ye kadar 20 ms arayla `GetForegroundWindow() == hwnd` beklenir → 2 sn'ye kadar VK_LWIN, VK_RWIN, VK_MENU, VK_CONTROL, VK_SHIFT bırakılması beklenir → ön pencere yeniden doğrulanır → Ctrl↓ V↓ V↑ Ctrl↑. Herhangi bir koşul tutmazsa false (hiçbir tuş gönderilmez).
- [ ] **Step 6: Kısayol** — App: `_hotkeys = new GlobalHotkeys()` açılışta koşulsuz kurulur (`RegisterMicHotkey` artık var olanı kullanır). `Hotkeys.ClipboardOpen` kaydedilir; başarısızsa açılışta bir kez `Flash(E77F, null, "<kısayol> kullanılamıyor")`. Basınca: `Panels.IsOpenWith("clipboard")` ise `Panels.Close()`; değilse (başka bir panel açık olsa bile) `target = Describe(CaptureForeground())`, `pasteOnChoose = PasteRule.Decide(true, target) != CopyOnly`, `Panels.Show("clipboard", …)` + `_island.ExpandForKeyboard()` (`HoverIntent.ExpandNow`; tam ekranda ada gizli kalır, panel yine açılır).
- [ ] **Step 7: Seçim** — `Chosen` (pasteOnChoose): `clips.Put(choice)` → `Panels.Close()` → `PasteRule.Decide(true, Describe(hwnd))` (seçim anında yeniden) → Paste ise `PasteIntoAsync`; false dönerse ya da CopyWithHint ise `Flash(E8C8, null, "Kopyalandı — Ctrl+V ile yapıştır")`. Kısayolla açılmış panel seçimsiz kapanınca (Esc ya da kısayol) ön pencere yoksa ya da CamAda'nınsa hatırlanan pencere öne alınır; kullanıcı dışarıda başka bir pencereye tıkladıysa odağa dokunulmaz.
- [ ] **Step 8: Derle, test et, kur** — build/test/install yukarıdaki gibi.
- [ ] **Step 9: Canlı dene** — Pano metni kaydedilir. Deneme penceresi `camclip2.exe` (tek `TextBox`, odakta, `--seconds 600`) öne alınır; panoya "deneme-1" konur; `keybd_event` ile Win↓ Alt↓ V↓ V↑ Alt↑ Win↑ → panel açık, ada genişlemiş (ekran görüntüsü); Enter `PostMessage` ile panele → TextBox'ta "deneme-1". Review Focus 2: Alt↓ tutulurken Enter → 2 sn içinde Alt↑ → yapıştırılır; Alt 3 sn tutulursa → yapıştırılmaz, ipucu görünür, TextBox değişmez. Review Focus 3: `camclip3.exe --seconds 8` öndeyken kısayol, panel açıkken pencere kapanır, Enter → ipucu, hiçbir pencereye Ctrl+V gitmez. Masaüstü öndeyken (deneme yok; `Describe` birim testine güvenilir). Düğmeyle açılan panelde Enter → yalnız "Kopyalandı". Pano metni geri konur.
- [ ] **Step 10: Commit** — `feat: Win+Alt+V ile pano ve önceki pencereye yapıştırma`

### Task 4: Ekran görüntüsü şeridi ve pano görseliyle eşleştirme

**Files:**
- Create: `src/CamAda.Core/Clipboard/ScreenshotTracker.cs`, `src/CamAda/Clipboard/ScreenshotLibrary.cs`
- Modify: `src/CamAda/Clipboard/ClipboardService.cs`, `src/CamAda/Interop/Native.Clipboard.cs` (`GetClipboardOwner`, `SHGetKnownFolderPath`), `src/CamAda/Panels/ClipboardPanel.xaml(.cs)`, `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Clipboard/ScreenshotTrackerTests.cs`

**Interfaces:**
- Consumes: `ClipboardService`, `ScreenshotChoice`, `ClipboardPanel`.
- Produces: `ClipboardService`'e `event EventHandler<ClipImageArrival>? ImageArrived` (dinleyici varsa görsel doğrudan eklenmez, karar dinleyicinin) ve `ClipItem? AddImage(ClipPayload payload)`; `sealed record ClipImageArrival(ClipPayload Payload, bool FromScreenshotTool)`; `enum ClipImageDecision { AddToHistory, Drop, Hold }`; `sealed class ScreenshotTracker(TimeProvider time)`: `static readonly TimeSpan Window = 3 sn`, `ClipImageDecision OnClipImage(string pendingId, int width, int height, bool fromScreenshotTool)`, `string? OnFile(int width, int height)` (eşleşen bekleyenin kimliği), `IReadOnlyList<string> TakeExpired()`. `ScreenshotLibrary(Dispatcher dispatcher, ClipboardService clips, string? folder) : IDisposable` (`ScreenshotTracker`'ın sahibi, `clips.ImageArrived`'ı dinler): `string? Folder`, `IReadOnlyList<ScreenshotFile> Recent(int count = 10)`, `event EventHandler<ScreenshotFile> Arrived`, `event EventHandler<ClipItem> ClipboardOnlyShot` (Görev 5 bağlar), `static string? KnownFolder()`, `static void OpenForEdit(string path)`, `static void ShowInFolder(string path)`; `sealed record ScreenshotFile(string Path, DateTime Created, int Width, int Height)`. `ClipboardPanel(ClipboardService clips, ScreenshotLibrary shots, bool pasteOnChoose)` (yeni parametre; Görev 3'teki çağrılar güncellenir).

- [ ] **Step 1: Testleri yaz** (`FakeTimeProvider`) — `File_then_matching_clip_within_3s_drops_clip`; `Tool_clip_waits_and_matching_file_claims_it` (Hold → `OnFile` aynı boyut → bekleyen kimlik döner); `Tool_clip_without_file_expires_after_3s` (`TakeExpired` 2,9 sn'de boş, 3,1 sn'de kimliği verir); `Other_app_image_is_added_directly`; `Different_size_is_not_a_match`; `Old_file_does_not_match` (dosyadan 4 sn sonra gelen pano görseli → AddToHistory).
- [ ] **Step 2: Çalıştır** → Expected: derleme hatası. **Step 3: `ScreenshotTracker`'ı yaz.** **Step 4: Çalıştır** → PASS.
- [ ] **Step 5: `ScreenshotLibrary`** — `KnownFolder()` = `SHGetKnownFolderPath(FOLDERID_Screenshots {B7BEDE81-DF94-4682-A7D8-57A52620B86F})`, App bunu verir (yalnız Debug derlemesinde `CAMADA_SCREENSHOTS` ortam değişkeni önceliklidir); yoksa ya da izleyici kurulamazsa `Folder` yine yolu verir, `Recent` boş, klasörün üst dizini izlenip klasör oluşunca izlemeye geçilir. `FileSystemWatcher` (`*.png`, `*.jpg`; `Created` + `Renamed`), dosya yazımı bitene kadar 100 ms arayla en çok 20 kez paylaşımsız açmayı dener; boyutlar `BitmapDecoder` ile (yalnız üst bilgi). Küçük resimler 96 DIP genişliğe `DecodePixelWidth` ile, dondurulmuş. `OpenForEdit`: `ms-screensketch:edit?source=CamAda&isTemporary=false&filePath=<URL kodlu yol>`; başarısızsa `mspaint.exe "<yol>"`. `ShowInFolder`: `explorer.exe /select,"<yol>"`.
- [ ] **Step 6: Eşleştirme** — `ClipboardService`: görsel geldiğinde `GetClipboardOwner` → süreç adı `SnippingTool` ya da `ScreenClippingHost` ise `FromScreenshotTool`; dinleyici varsa yalnız `ImageArrived` yayınlanır. `ScreenshotLibrary` `ScreenshotTracker` kararına göre `clips.AddImage` çağırır / atar / bekletir; yeni dosyada `OnFile` bir bekleyeni sahiplenirse o atılır; `TakeExpired` 500 ms'lik bir zamanlayıcıyla yalnız bekleyen varken sorulur, süresi dolan `AddImage` ile eklenir ve eklenen öğeyle `ClipboardOnlyShot` yayınlanır. `ScreenshotChoice` için `Put` görseli dosyadan yazar (kendi yazımı olduğundan geçmişe girmez).
- [ ] **Step 7: Şerit** — panelin altında "Ekran görüntüleri" başlığı + sağda "Klasörü aç" (`E838`); yatay kayan 96×54 küçük resimler (8 DIP köşe), en yenisi solda, en çok 10; boşsa "Henüz ekran görüntüsü yok". Tıklama / Enter → `Chosen(ScreenshotChoice)` (Görev 3'teki iki durumlu davranış); sağ tık menüsü "Düzenle", "Klasörde göster". `Arrived`'da canlı güncellenir. ←/→ ile şeritte gezinilir, ↓ listeden şeride geçer.
- [ ] **Step 8: Derle, test et, kur, canlı dene** — Deneme exe'si Screenshots klasörüne 1280×720 `camada-test-1.png` yazar → şeritte en solda; aynı boyutta görseli 1 sn içinde panoya koyup (sahibi deneme exe'si) → geçmişte yeni satır yok. 600×400 görsel panoya konur, dosya yok → geçmişte satır var. Şeritteki görüntüye tıkla → panoda 1280×720 görsel, geçmişte satır yok. Review Focus 5: kurulu CamAda menüdeki "Çıkış"la kapatılır, Debug derlemesi `CAMADA_SCREENSHOTS=%TEMP%\camada-shots-yok` ile çalıştırılır → şerit "Henüz ekran görüntüsü yok", çökme yok; klasör oluşturulup içine `camada-test-3.png` bırakılınca şeritte görünür; Debug örneği "Çıkış"la kapatılır, kurulu CamAda yeniden başlatılır. Deneme dosyaları silinir.
- [ ] **Step 9: Commit** — `feat: ekran görüntüsü şeridi`

### Task 5: Ekran görüntüsü önizlemesi

**Files:**
- Modify: `src/CamAda.Core/Island/IslandPresence.cs`, `src/CamAda.Core/Island/HoverIntent.cs`, `src/CamAda/Island/IslandWindow.xaml(.cs)`, `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Island/SmartHideTests.cs`, `tests/CamAda.Core.Tests/Island/HoverIntentTests.cs`

**Interfaces:**
- Consumes: `ScreenshotLibrary.Arrived`, `ScreenshotLibrary.ClipboardOnlyShot`, `ScreenshotLibrary.OpenForEdit`, `ClipboardService.Put`.
- Produces: `enum FlashKind { None, Passive, Interactive }`; `IslandPresenceRule.Decide(bool fullScreen, bool occluded, bool expanded, FlashKind flash)`; `HoverIntent.Suppress(bool on)`; `IslandWindow.ShowScreenshot(ImageSource thumbnail, Action copy, Action edit)`.

- [ ] **Step 1: Testleri yaz** — `SmartHideTests`: mevcut `flashing: false/true` çağrıları `FlashKind.None/Passive` olur (beklenenler aynı); `Interactive_flash_is_clickable_over_a_window`: `Decide(false, true, false, FlashKind.Interactive)` == `Shown`; `Interactive_flash_never_shows_over_full_screen` → `Hidden`. `HoverIntentTests.Suppressed_pointer_does_not_expand_until_released`: `Suppress(true)`, `PointerEntered`, 1 sn ilerlet → genişlemedi; `Suppress(false)` → giriş gecikmesi sonra genişler; `Suppress_release_with_pointer_outside_does_nothing`.
- [ ] **Step 2: Çalıştır** → Expected: derleme hatası. **Step 3: Core'u yaz.** **Step 4: Çalıştır** → PASS.
- [ ] **Step 5: Önizleme** — `IslandWindow.xaml`'da `FlashRow`'un yanında `ShotRow` (başta gizli): solda 96×54 küçük resim (8 köşe), "Ekran görüntüsü" (12,5 kalın), sağda iki küçük `PanelButton` "Kopyala" (`E8C8`) ve "Düzenle" (`E70F`). `ShowScreenshot`: ada genişlemişse, panel açıksa ya da tam ekransa hiçbir şey yapmaz; değilse hap 0,18 sn'de 420×76'ya büyür (köşe yarıçapı yükseklik/2 kalır, cam ve gölge izler), `CompactLayer` gizlenir, `FlashKind.Interactive` ile `ApplyPresence` (örtülüyken tıklama geçirmez), `HoverIntent.Suppress(true)`. 5 sn sonra kapanır; imleç `ShotRow` üzerindeyken süre durur, çıkınca 1 sn sonra kapanır. Kopyala → `copy()` + satırda "Kopyalandı"; Düzenle → `edit()` ve önizleme kapanır. Kapanınca hap 400×40'a döner, `Suppress(false)`. Yeni ekran görüntüsü gelirse içerik değişir, süre yenilenir. Pasif `Flash` önizleme sürerken gösterilmez.
- [ ] **Step 6: Bağla** — App: `Arrived` → `ShowScreenshot(küçük resim, () => clips.Put(new ScreenshotChoice(path)), () => ScreenshotLibrary.OpenForEdit(path))`; `ClipboardOnlyShot(item)` → aynısı; Kopyala `clips.Put(new HistoryChoice(item.Id))`, Düzenle önce `item.Payload.Png`'yi `%TEMP%\CamAda\shot-<yyyyMMdd-HHmmss>.png`'ye yazar.
- [ ] **Step 7: Derle, test et, kur, canlı dene** — Deneme exe'si Screenshots'a `camada-test-2.png` yazar → adada önizleme (ekran görüntüsü), 5 sn sonra kaybolur. Bir deneme penceresi adanın yerini örtüyorken yeni dosya → önizleme görünür; "Kopyala" gerçek tıklamayla → panoda görsel, ada genişlemez. "Düzenle" → Ekran Alıntısı Aracı (ya da Paint) dosyayla açılır, kapatılır. İmleç önizleme üzerinde 8 sn bekletilince kapanmaz. Deneme dosyaları silinir, pano metni geri konur.
- [ ] **Step 8: Commit** — `feat: ekran görüntüsü alınınca adada önizleme`

### Kullanıcının elle denemesi (son)

1. Chrome'da yazarken Win+Alt+V → bir öğe seç → Chrome'a yapıştırıldı mı; Ctrl+Shift+V hâlâ biçimsiz yapıştırıyor mu.
2. Win+Shift+S ile gerçek ekran görüntüsü → adada önizleme, Kopyala ve Düzenle (Ekran Alıntısı Aracı açılıyor mu); Pano panelinde şeritte tek kez, geçmişte ayrıca yok. (Bu deneme pano sahibinin süreç adını da doğrular: `SnippingTool` / `ScreenClippingHost`.)
3. Bir şey sabitle, bilgisayarı yeniden başlat → yalnız sabitli kalmış mı.
4. Şifre yöneticisi kullanıyorsan bir şifre kopyala → panelde görünmemeli.
