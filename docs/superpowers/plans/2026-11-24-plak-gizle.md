# Plağı Gizle — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adanın sağ tık menüsüne kalıcı "Plağı gizle" seçeneği eklemek; plak anında kapanır/açılır.

**Architecture:** Menü öğesi `IslandCommands` üzerinden App'e bağlanır (mevcut "Görev çubuğunu gizle" deseni). App, `Record.Enabled`'ı kaydeder; gizlerken `RecordWindow`'u kapatır, gösterirken açılıştaki yolla yeniden kurar ve Spotify eklerini bağlar. Açılıştaki plak kurulumu da aynı `ShowRecord()` yolundan geçer.

**Tech Stack:** C# / .NET 10, WPF, xUnit (CamAda.Core.Tests).

**Spec:** `docs/superpowers/specs/2026-11-24-plak-gizle-design.md` (üst tasarım: `docs/superpowers/specs/2026-11-23-cam-ada-design.md`)

## Global Constraints

- Menü yazısı tam olarak: `Plağı gizle` — işaretli öğe (`IsCheckable="True"`), işaret = plak gizli; varsayılan işaretsiz.
- Kalıcılık alanı: `AppSettings.Record.Enabled` (yeni ayar alanı yok; `SettingsVersion` değişmez).
- Gizliyken plak penceresi yok (kapalı): medya olayı, örtülme yoklaması, kare zamanlayıcısı çalışmaz.
- Adadaki mini plak ve müzik bölümü etkilenmez.
- Derleme uyarısız; `dotnet test CamAda.slnx --nologo` tamamı geçer; `tools/install.ps1` ile kurulur.

## Review Focus

1. Gizle → göster → gizle art arda: hiçbir anda iki plak penceresi olmaz; kapanan pencere olay aboneliklerini bırakmıştır.
2. Plak gizliyken uygulama yeniden açılır: plak gelmez, menü açılınca "Plağı gizle" işaretli görünür.
3. Gösterilen plakta (Spotify bağlıyken) karttaki kalp ve "Sıradaki" yine görünür — ekler yeni pencereye bağlanmıştır.
4. Müzik çalarken gösterilen plak bir sonraki medya olayını beklemeden döner.
5. Plak gizliyken "Çıkış" ve kurulum betiğinin kapatması hatasız (kapanmış pencere ikinci kez kapatılmaz).

Not: 1–5 WPF pencere davranışı; Core'da test edilecek saf mantık yok. Task 1 Step 6'daki canlı denemede her biri ayrı ayrı denetlenir. Kalıcılığın Core tarafı Step 1'deki testle sabitlenir.

---

### Task 1: Menüden plağı gizle / göster

**Files:**
- Modify: `src/CamAda/Island/IslandCommands.cs` (iki parametre)
- Modify: `src/CamAda/Island/IslandWindow.xaml:44-54` (menü öğesi), `src/CamAda/Island/IslandWindow.xaml.cs:108-116, 448-452` (işaret ve tıklama)
- Modify: `src/CamAda/App.xaml.cs:114-146` (komut bağlama, plak kurulum sırası), yeni `SetRecordHidden`, `ShowRecord`, `CloseRecord`
- Test: `tests/CamAda.Core.Tests/Settings/SettingsStoreTests.cs`

**Interfaces:**
- Consumes: `RecordWindow(NowPlayingService media, Func<RecordSettings> settings)`, `RecordWindow.Extras : ISpotifyExtras?`, `SettingsStore.Save/Load`.
- Produces: `IslandCommands` sonuna `Func<bool> IsRecordHidden, Action<bool> SetRecordHidden`; App'te `private void ShowRecord()`, `private void CloseRecord()`, `private void SetRecordHidden(bool hidden)`, alan `private ISpotifyExtras? _spotifyExtras`.

- [ ] **Step 1: Kalıcılık testini yaz** — `SettingsStoreTests` içine:

```csharp
[Fact]
public void Hidden_record_survives_save_and_load()
{
    var store = new SettingsStore(_dir);
    var s = store.Load();
    store.Save(s with { Record = s.Record with { Enabled = false, Diameter = 400 } });

    var r = new SettingsStore(_dir).Load().Record;
    Assert.False(r.Enabled);
    Assert.Equal(400, r.Diameter); // gizlemek diğer plak ayarlarını sıfırlamaz
}
```

- [ ] **Step 2: Testi çalıştır**

Run: `dotnet test CamAda.slnx --nologo --filter Hidden_record_survives_save_and_load`
Expected: PASS — `SanitizeRecord` `Enabled`'ı zaten koruyor; bu test yeni özelliğin dayandığı mevcut davranışı sabitleyen bir korumadır (önce kırmızı olması beklenmez; deftere yazılır). FAIL çıkarsa `SanitizeRecord` düzeltilir.

- [ ] **Step 3: Komutu ekle** — `IslandCommands` kaydının sonuna `Func<bool> IsRecordHidden, Action<bool> SetRecordHidden` (XML belgesiyle: "Masaüstü plağı şu an gizli mi." / "Ayarı kaydeder ve plağı kapatır ya da yeniden kurar.").

- [ ] **Step 4: Menü öğesi** — `IslandWindow.xaml`'da "Windows açılınca başlat" öğesinin altına `<MenuItem x:Name="HideRecordItem" Header="Plağı gizle" IsCheckable="True" Click="HideRecordItem_Click" />`. `ContextMenuOpening` içinde `HideRecordItem.IsChecked = _commands.IsRecordHidden();`; tıklama işleyicisi `_commands.SetRecordHidden(HideRecordItem.IsChecked)`.

- [ ] **Step 5: App tarafı**
  - Spotify ekleri (`extras`) `_spotifyExtras` alanında tutulur; plak kurulumu eklerin oluşturulmasından **sonraya** taşınır: `if (_settings.Record.Enabled) ShowRecord();` ve eski `_record.Extras = extras` bloğu kalkar.
  - `ShowRecord()`: `_record` doluysa hiçbir şey yapmaz; değilse `new RecordWindow(_media!, () => _settings.Record)`, `Show()`, sonra `Extras = _spotifyExtras` (açılıştaki sırayla aynı: önce göster, sonra ekler).
  - `CloseRecord()`: `_record?.Close(); _record = null;`
  - `SetRecordHidden(bool hidden)`: `_settings = _settings with { Record = _settings.Record with { Enabled = !hidden } }`, `Save`, sonra `hidden ? CloseRecord() : ShowRecord()`.
  - Komut bağlama: `IsRecordHidden: () => !_settings.Record.Enabled`, `SetRecordHidden: SetRecordHidden`.
  - `ExitGracefully` içindeki `_record?.Close()` → `CloseRecord()`.

- [ ] **Step 6: Derle, test et, kur, canlı dene**

Run: `dotnet build CamAda.slnx --nologo` → Expected: `0 Uyarı`, `0 Hata`.
Run: `dotnet test CamAda.slnx --nologo` → Expected: `Başarısız: 0`, toplam 249.
Run: `powershell -File tools/install.ps1` → Expected: "CamAda kuruldu ve başlatıldı".

Canlı (UIA ile menü öğesi Invoke; plak penceresi başlığı `CamAda plak`, CamAda işlemine ait görünür üst düzey pencere sayılır):
- "Plağı gizle" → Expected: `CamAda plak` penceresi 0; `settings.json` → `"Enabled": false`; Review Focus 1 için hemen ardından göster/gizle/göster → her adımda pencere sayısı 0 ya da 1.
- Göster (işareti kaldır) → Expected: pencere 1, `"Enabled": true`; Spotify çalarken plak dönüyor (Focus 4) ve kartta kalp/Sıradaki var (Focus 3).
- Gizliyken Çıkış + yeniden aç → Expected: pencere 0, menüde "Plağı gizle" işaretli, çıkışta crash.log'a yeni satır yok (Focus 2, 5).
- Son durumda plağı yeniden göster (kullanıcının varsayılanı).

- [ ] **Step 7: Commit**

```bash
git add src/CamAda tests/CamAda.Core.Tests docs/superpowers
git commit -m "feat: adanın menüsünden plağı gizle"
```
