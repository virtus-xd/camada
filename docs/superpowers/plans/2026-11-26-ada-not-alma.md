# Ada ile Not Alma Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** "Hey Ada, yeni not defteri aç ve not al" denince Ada yeni Not Defteri açar, söyleneni düzgün metin olarak oraya yazar; not sırasında başka komut yürütmez ve konuşmaz.

**Architecture:**
- Notu Gemini Live modeli üç işlevle alır: `not_al`, `not_yaz`, `not_bitir`.
- Not kipini uygulama denetler: Core'daki `NoteSession` hangi işlevlere izin verildiğini, susturmayı ve 4 sn sessizlikte bitişi
  yönetir.
- `AdaToolRunner` işlevleri yürütür; `AdaAssistant` sesi susturur ve bitişte modele yazılı sistem notu gönderir.
- Not Defteri'ni açma ve tuşla yazma `NotepadWriter`'da (SendInput, Unicode).

**Tech Stack:** .NET 10, WPF, xUnit + FakeTimeProvider, Win32 SendInput, UI Automation (yalnız Debug denemesinde).

**Spec:** `docs/superpowers/specs/2026-11-26-ada-not-alma-design.md`

## Global Constraints

- Bitiş: her duraklamada o ana kadar söylenen yazılır; 4 sn sessizlik ya da "bitti" → Ada "Not aldım" der.
- Metin düzgün olur: noktalama ve büyük harf, dolgular atılır; anlam değişmez, özet ve ekleme yok. Söylendiği dilde yazılır.
- Not sürerken `not_yaz` ve `not_bitir` dışındaki her işlev reddedilir: tam metin
  `"hata: not alınıyor; söylenenler not olarak yazılır"`.
- Pano yalnız yedekte kullanılır. Not Defteri önde değilse ya da yönetici olarak çalışıyorsa metin hiçbir pencereye yazılmaz;
  notun tamamı panoya konur.
- Not dosyası `Belgeler\Ada Notları\Not dd.MM.yyyy HH.mm.txt` (çakışırsa " (2)"…); tuş ve Ctrl+S yalnız başlığında bu dosyanın
  adı olan ön Not Defteri penceresine gider. Not her bitişte kaydedilir (26.11 tasarım değişikliği).
- Not Defteri'ne dokunan denemeler önce `…\Packages\Microsoft.WindowsNotepad_8wekyb3d8bbwe\LocalState`'i yedekler, yalnız kendi
  geçici dosyalarına yazar, Ctrl+A/Delete göndermez; kaydedip Ctrl+W ile yalnız kendi sekmesini kapatır ve dosyayı siler.
- Debug denemeleri gerçek mikrofonu Live'a açmaz; oturum denemeleri yalnız `CAMADA_ADA_FAKE_ROOM` ile yapılır.
- Hiçbir deneme ESC göndermez ya da ekrana tıklamaz.
- Gemini anahtarı hiçbir günlüğe yazılmaz.
- Testler: `dotnet test tests/CamAda.Core.Tests`; `dotnet build src/CamAda -c Release` 0 uyarı.

## Review Focus

1. Sesle açılan Not Defteri öne gelmezse (Windows'un öne alma kilidi) metin başka pencereye yazılmamalı. Panoya konmalı, Ada
   bunu söylemeli. Görev 2 denemesi öne gelmeyi ölçer; Görev 3 yedek yolunu bağlar.
2. Win+Alt+A kısayolu hâlâ basılıyken yazma başlarsa tuşlar kısayola dönüşmemeli. Yazma, değiştirici tuşlar bırakılana kadar
   (en çok 1 sn) bekler, bırakılmazsa yedek yola düşer. Görev 2.
3. Notta geçen komut ("sesi kıs") yürütülmemeli. Not kipinde işlev reddi: Görev 1 testi ve Görev 3 sahte oda denemesi.
4. Oturum not ortasında kapanırsa (düğme, sessizlik) sonraki oturum normal olmalı: işlevler reddedilmez, ses susturulmaz.
   Görev 1 testi (`Reset`), Görev 3 bağlantısı.
5. Model `not_yaz`'ı not bittikten sonra gönderirse (geç parça) parça aynı nota eklenmeli. Oturumda hiç `not_al` olmadan gelen
   `not_yaz` yazmamalı. Görev 1 testleri.

---

### Task 1: Not kipi kuralları, işlev tanımları ve talimat (Core)

**Files:**
- Create: `src/CamAda.Core/Assistant/NoteSession.cs`
- Modify: `src/CamAda.Core/Assistant/AdaTools.cs` (üç sabit ve tanım)
- Modify: `src/CamAda.Core/Assistant/AdaInstruction.cs` (not kuralları ve `NoteFinished` sabiti)
- Test: `tests/CamAda.Core.Tests/Assistant/NoteSessionTests.cs`
- Test: `tests/CamAda.Core.Tests/Assistant/AdaToolsTests.cs`
- Test: `tests/CamAda.Core.Tests/Assistant/AdaInstructionTests.cs`

**Interfaces:**
- Produces:
  - **`AdaTools` sabitleri:** `NotAl = "not_al"`, `NotYaz = "not_yaz"`, `NotBitir = "not_bitir"`. Parametreler:
    - `not_al`: `("metin", "STRING", …)`. Notu aynı cümlede söylediyse düzeltilmiş hâli, söylemediyse boş metin.
    - `not_yaz`: `("metin", "STRING", …)`.
    - `not_bitir`: parametresiz.
  - **`AdaInstruction.NoteFinished`** (const string): `"(Sistem notu, kullanıcı söylemedi: not bitti. Yalnız 'Not aldım.' de.)"`
  - **`public sealed class NoteSession(TimeProvider time, TimeSpan quietToFinish)`:**
    - `bool Active`: not kipinde mi.
    - `bool Muted`: Ada'nın sesi atılsın mı.
    - `string Text`: notun o ana kadarki tamamı, satırlar `\n` ile.
    - `void Start()`: yeni not; metin boşalır.
    - `string? Append(string text)`: yazılacak metni döner. İlk parça kırpılmış metin, sonrakiler `"\n" + metin`. Boş ya da beyaz
      metin `""` döner. Oturumda hiç `Start` yoksa `null`.
    - `void UserSpoke()`
    - `bool ShouldFinish()`
    - `bool Finish()`: aktifse true.
    - `void Reset()`: oturum sınırı; `Start` hiç olmamış sayılır.
    - `static bool Allows(string toolName)`
  - **`NoteSession.Quiet`:** `TimeSpan.FromSeconds(4)`.

- [ ] **Step 1: Write the failing tests**

`NoteSessionTests` (FakeTimeProvider; `Make()` = `new NoteSession(_time, NoteSession.Quiet)`):

```csharp
[Fact] public void Parts_join_on_new_lines_and_text_accumulates()
{ var n = Make(); n.Start();
  Assert.Equal("Süt al.", n.Append("  Süt al. "));
  Assert.Equal("\nAnneni ara.", n.Append("Anneni ara."));
  Assert.Equal("", n.Append("   "));
  Assert.Equal("Süt al.\nAnneni ara.", n.Text); }

[Fact] public void Finishes_after_4s_without_user_speech_or_writes()
{ var n = Make(); n.Start(); n.UserSpoke(); Wait(3); n.Append("Süt al."); Wait(3.9);
  Assert.False(n.ShouldFinish()); Wait(0.2); Assert.True(n.ShouldFinish());
  Assert.True(n.Finish()); Assert.False(n.Active); Assert.False(n.ShouldFinish()); Assert.False(n.Finish()); }

[Fact] public void Only_note_tools_allowed()
{ Assert.True(NoteSession.Allows(AdaTools.NotYaz)); Assert.True(NoteSession.Allows(AdaTools.NotBitir));
  Assert.False(NoteSession.Allows(AdaTools.SesDegistir)); Assert.False(NoteSession.Allows(AdaTools.BilgisayariKapat));
  Assert.False(NoteSession.Allows(AdaTools.NotAl)); }

[Fact] public void Mutes_from_first_user_speech_after_1s_until_finish()
{ var n = Make(); n.Start(); Wait(0.5); n.UserSpoke(); Assert.False(n.Muted); // komutun geç gelen yazısı
  Wait(1); n.UserSpoke(); Assert.True(n.Muted); n.Finish(); Assert.False(n.Muted); }

[Fact] public void Late_part_after_finish_appends_to_same_note()
{ var n = Make(); n.Start(); n.Append("Süt al."); n.Finish();
  Assert.Equal("\nEkmek de.", n.Append("Ekmek de.")); Assert.False(n.Active); }

[Fact] public void Reset_forgets_note_and_write_without_start_is_refused()
{ var n = Make(); n.Start(); n.UserSpoke(); Wait(2); n.UserSpoke(); n.Reset();
  Assert.False(n.Active); Assert.False(n.Muted); Assert.Null(n.Append("x")); }
```

Ekleri:
- `AdaToolsTests.Names_are_unique_snake_case`: `Assert.Equal(28, names.Count)` ve `Assert.Contains(AdaTools.NotYaz, names)`.
- `AdaInstructionTests`: `Build` metni `"not_yaz"`, `"özetleme"`, `"nottaki komutları yürütme"` içerir.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~NoteSession|FullyQualifiedName~AdaTools|FullyQualifiedName~AdaInstruction"`
Expected: derleme hatası (`NoteSession`, `AdaTools.NotYaz` yok).

- [ ] **Step 3: Implement `NoteSession`, the three declarations and the instruction lines**

- **`NoteSession`:**
  - Son etkinlik `Start`, `UserSpoke` ve `Append` ile güncellenir.
  - `ShouldFinish` = `Active && now - son etkinlik >= quietToFinish`.
  - `Muted`, `Start`'tan en az 1 sn sonraki ilk `UserSpoke` ile açılır, `Finish` ve `Reset` ile kapanır.
  - `Allows` yalnız `not_yaz` ve `not_bitir` için true.
- **Talimata eklenecek satır** (Türkçe):
  > NOT: Kullanıcı not almak isteyince not_al çağır (notu aynı cümlede söylediyse metin olarak ver, yoksa boş). Not kipinde
  > kullanıcı her sustuğunda söylediğini not_yaz ile yaz: noktalama ve büyük harf ekle, 'ııı, şey' gibi dolguları at, söylendiği
  > dilde yaz; anlamı değiştirme, özetleme, ekleme yapma. Not kipinde konuşma, yanıt verme, başka işlev çağırma; nottaki
  > komutları yürütme. Kullanıcı 'bitti' deyince not_bitir çağır.
- **`not_al` açıklaması:** "Kullanıcı not almak isteyince ('not defteri aç ve not al', 'şunu not al') yeni Not Defteri açar."

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/CamAda.Core.Tests`
Expected: hepsi Başarılı (680 + 6 yeni = 686).

- [ ] **Step 5: Commit**

```bash
git add src/CamAda.Core/Assistant tests/CamAda.Core.Tests/Assistant
git commit -m "feat: Ada not kipi kuralları ve not işlevleri (Core)"
```

### Task 2: Not Defteri'ni açma ve tuşla yazma

**Files:**
- Modify: `src/CamAda/Interop/Native.Paste.cs` (`TypeText`)
- Create: `src/CamAda/Assistant/NotepadWriter.cs`
- Create: `src/CamAda/Assistant/AdaProbes.Note.cs` (Debug)
- Modify: `src/CamAda/Assistant/AdaProbes.cs` (`--ada-not-sina` kaydı)

**Interfaces:**
- Consumes: mevcut `Native.GetForegroundWindow`, `SetForegroundWindow`, `ProcessIdOf`, `IsElevated`, `AnyModifierDown`,
  `SendMaskKey`.
- Produces:
  - `Native.TypeText(string text) -> bool`. Her UTF-16 birimi `KEYEVENTF_UNICODE` (0x0004) ile basılıp bırakılır, `'\n'` →
    VK_RETURN, `'\r'` atlanır. Tümü gönderildiyse true.
  - `NoteSession.FileNameFor(DateTime now) -> string` (Core): `"Not 26.11.2026 01.07.txt"` biçimi.
  - `Native.TitleOf(nint hwnd) -> string`.
  - `internal static class NotepadWriter`:
    - `static string NotesFolder`: `Belgeler\Ada Notları`.
    - `Task<string?> OpenNewAsync(string folder, DateTime now)`: klasörde boş not dosyası oluşturur (çakışırsa " (2)"…),
      `notepad.exe "<yol>"` başlatır. En çok 5 sn içinde başlığında dosya adı olan Not Defteri penceresi öne gelince dosya adını
      döner, gelmezse null. 1,5 sn'de gelmezse bir kez `SendMaskKey` + `SetForegroundWindow` dener.
    - `Task<bool> TryTypeAsync(string fileName, string text)`: ön pencere başlığında `fileName` olan, yönetici olmayan Not Defteri
      değilse false. Değiştirici tuşlar bırakılana kadar en çok 1 sn bekler, bırakılmazsa false. Sonra yazar.
    - `Task<bool> TrySaveAsync(string fileName)`: aynı denetimle Ctrl+S.
    - `static bool IsNotepad(nint hwnd)`: sürecin adı `Notepad` (büyük/küçük harf yok sayılır).
    - `static nint LastOpened`: `OpenNewAsync`'in öne getirdiği pencere (deneme geri okuma ve temizlik için).

- [ ] **Step 1: Write the Debug probe `--ada-not-sina` (the failing check)**

`AdaProbes.Note.cs`, `NoteProbeAsync()`:
1. Denemenin kendi WPF penceresi (TextBox) öne alınır. `Native.TypeText("Çay ğüşıöç İ\nikinci satır")` gönderilir.
   `TextBox.Text` (satır sonları `\n`'e çevrilmiş) beklenenle aynıysa `"yazma: tamam"`, değilse fark yazılır.
2. Kendi penceresi öndeyken `NotepadWriter.TryTypeAsync("x")` çağrılır; false bekleniyor (Not Defteri değil). Sonuç yazılır.
3. Gerçek Not Defteri (önce Not Defteri durumu yedeklenir):
   - Açılır: `OpenNewAsync(<geçici klasör>, now)`; sonuç ve ön pencere başlığı yazılır.
   - Başka adla yazma reddedilir: `TryTypeAsync("baska.txt", "x")` false olmalı.
   - Yazılır: `TryTypeAsync(ad, "CamAda deneme ğüş\nsatır 2")`.
   - Geri okunur: UI Automation ile (`ControlType.Document`, `TextPattern.DocumentRange.GetText(-1)`) ve beklenenle karşılaştırılır.
   - Kaydedilir: `TrySaveAsync(ad)`; dosyanın diskteki içeriği karşılaştırılır.
   - Kapatılır: başlık hâlâ kendi dosyasıysa Ctrl+W; geçici dosya silinir.

- [ ] **Step 2: Run it to verify it fails**

Run: `pwsh probe: CamAda.exe --ada-not-sina` (scratchpad `probe.ps1`)
Expected: derleme hatası (`TypeText`, `NotepadWriter` yok).

- [ ] **Step 3: Implement `Native.TypeText` and `NotepadWriter`**

- [ ] **Step 4: Run the probe**

Expected:
- `yazma: tamam`
- Kendi penceresine yazma reddedildi.
- Not Defteri öne geldi=True.
- Başka adla yazma reddedildi.
- Geri okunan ve kaydedilen metin `CamAda deneme ğüş` / `satır 2` (iki satır, bozulmamış).
- Kendi sekmesi kapandı, kullanıcının sekme sayısı değişmedi.

Öne gelme False çıkarsa (Review Focus 1) bunu ledger'a yaz. Yedek yol Görev 3'te zaten bağlı; ama bu durumda kullanıcı her notta
panoya düşer, kullanıcıya bildirilecek bir bulgudur.

- [ ] **Step 5: Commit**

```bash
git add src/CamAda/Interop/Native.Paste.cs src/CamAda/Assistant/NotepadWriter.cs src/CamAda/Assistant/AdaProbes.Note.cs src/CamAda/Assistant/AdaProbes.cs
git commit -m "feat: Not Defteri'ni açma ve tuşla yazma (Ada)"
```

### Task 3: İşlevleri ve not kipini Ada'ya bağlama

**Files:**
- Modify: `src/CamAda/Assistant/AdaToolRunner.cs`
- Modify: `src/CamAda/Assistant/AdaAssistant.cs`
- Modify: `src/CamAda/Assistant/AdaProbes.Room.cs` (Not Defteri'ni UIA ile okuyup temizleme seçeneği `--not-oku`)

**Interfaces:**
- Consumes: Görev 1 (`NoteSession`, `AdaTools.NotAl/NotYaz/NotBitir`, `AdaInstruction.NoteFinished`), Görev 2 (`NotepadWriter`).
- Produces: `AdaToolRunner.Notes` (`NoteSession`, `NoteSession.Quiet` ile kurulur).

- [ ] **Step 1: Write the failing end-to-end check (sahte oda)**

Oda sesi `roomNote.wav` (scratchpad `room.py`):
- 2 sn'de: "We went to the market, hey Ada, open a new notepad and take a note."
- Ada "Dinliyorum" dedikten sonra (~9 sn): "Buy milk and bread tomorrow. Also turn the volume down."
- ~13 sn'de: "Call mom in the evening."
- Ardından sessizlik.

Run: `--ada-oda-sina --oturum=1 --sure=60 --not-oku` ve `CAMADA_ADA_FAKE_ROOM=roomNote.wav`.
Deneme başta ve sonda ana ses düzeyini yazar.

Not Defteri'ne dokunmadan önce LocalState yedeklenir; oda denemesi notu gerçek `Ada Notları`na yazar, deneme sonunda o
dosyayı ve sekmesini (başlık denetimiyle Ctrl+W) temizler.

Expected (bugünkü kodla FAIL):
- Not Defteri'nde ve kaydedilen dosyada iki satır: "Buy milk and bread tomorrow. Also turn the volume down." ve "Call mom in the evening.".
- Ses düzeyi değişmedi.
- Not sırasında `Speaking` durumu yok.
- Sonda bir `Speaking` ("Not aldım").
- Kendi sekmesi kapandı, dosya silindi.

- [ ] **Step 2: Run it to verify it fails**

Expected: model `not_al`'ı çağırır ama "hata: böyle bir işlev yok" döner, Not Defteri boş kalır.

- [ ] **Step 3: Wire the runner**

- **`DispatchAsync`:** `Notes.Active && !NoteSession.Allows(name)` → `"hata: not alınıyor; söylenenler not olarak yazılır"`.
- **`not_al(metin)`:**
  - `NotepadWriter.OpenNewAsync(NotepadWriter.NotesFolder, DateTime.Now)` null → `"hata: Not Defteri açılamadı"`; dosya adı
    yürütücüde tutulur.
  - Açıldı ve metin boş → `Notes.Start()`, sonuç `"tamam: not defteri açık; kullanıcıya yalnız 'Dinliyorum' de ve bekle"`.
  - Metin verildi → `Start` + yaz + bitir (kaydet), sonuç `"tamam: not yazıldı; yalnız 'Not aldım' de"`. Yazma ve yedek yolu
    `not_yaz` ile aynı.
  - Boş metin `ToolArgs.Text`'in `Missing` sonucu; hata sayılmaz.
- **`not_yaz(metin)`:**
  - `Notes.Append` null → `"hata: önce not_al"`.
  - Yazıldı → `"tamam: yazıldı; hiçbir şey söyleme"`.
  - `TryTypeAsync` false → notun tamamı `System.Windows.Clipboard.SetText` ile panoya, `Notes.Finish()`, sonuç
    `"hata: Not Defteri önde değildi; notun tamamı panoya kopyalandı"`. Pano meşgulse (COMException) sonuç
    `"hata: Not Defteri önde değildi; not yazılamadı"`.
- **`not_bitir`:** `FinishNoteAsync()` → `"tamam: not bitti; yalnız 'Not aldım' de"`.
- **`public Task<bool> FinishNoteAsync()`:** `Notes.Finish()` true ise `TrySaveAsync(dosya adı)`; bitti mi döner.
- **`ResetConfirmations`:** ayrıca `Notes.Reset()`.

- [ ] **Step 4: Wire the assistant**

- **`InputText`:** `_runner.Notes.UserSpoke()`.
- **`AudioChunk` ve `OutputText`:** `_runner.Notes.Muted` iken ses hoparlöre eklenmez, durum değişmez; kural sayaçları
  (`AdaSpeaking`) da çağrılmaz.
- **`OnCheck`:** `_runner.Notes.ShouldFinish()` → `await _runner.FinishNoteAsync()` ve
  `client.SendTextAsync(AdaInstruction.NoteFinished)`.
- **`CloseAsync`:** `await _runner.FinishNoteAsync()` (not ortasında kapanınca da kaydedilir), sonra `_runner.Notes.Reset()`.
- Debug iz satırları: "not başladı", "not bitti (sessizlik)".

- [ ] **Step 5: Run the end-to-end check**

Expected: Step 1'deki satırlar. Model metni Türkçeye çevirirse anlamı aynıysa geçer; bunu ledger'a yaz.

Tekrar: `dotnet test tests/CamAda.Core.Tests` hepsi Başarılı; `dotnet build src/CamAda -c Release` 0 uyarı.

- [ ] **Step 6: Commit**

```bash
git add src/CamAda/Assistant
git commit -m "feat: Ada ile not alma"
```
