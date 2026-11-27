# Plan 12: Ada arayüz dilinde açılır — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ada, Camada'nın arayüz dilinde (Türkçe/İngilizce) konuşarak açılsın; kelimesi kelimesine söylettiğimiz cümleler ve hatırlatma duyurusu da o dilde olsun; "Ada speaks Turkish for now" notu kalksın.

**Architecture:** Modele giden yönerge Türkçe kalır. Yalnız dil satırı, tarih ve tırnak içindeki hazır cümleler `CultureInfo ui` ile seçilir. Cümleler `CoreStrings`'te (İngilizce temel + `tr`) durur, `CoreText.Get/Format` ile okunur. Çağıran yer Plan 11 düzeninde `CultureInfo.CurrentUICulture` verir.

**Tech Stack:** C#/.NET 10, CamAda.Core (net10.0), xUnit, .resx (StronglyTyped `CoreStrings`).

**Spec:** `docs/superpowers/specs/2026-11-27-ada-arayuz-dili-design.md`

## Global Constraints

- Türkçe arayüzde Ada'nın söylediği her cümle bugünküyle birebir aynı kalır. Mevcut Türkçe test beklentileri değişmez; değişen tek şey imzalar (`ui` parametresi) ve iki işlev sonucunda tırnak içindeki "Not aldım" cümlesine eklenen nokta.
- Sistem notu sarmalayıcıları ve işlev sonuçlarının geri kalanı Türkçe kalır. Yalnız tırnak içindeki, Ada'nın söyleyeceği cümle yerelleşir.
- Yeni anahtarlar iki resx'e birden eklenir (`ResourceParityTests`). Dosyalar resx yardımcısıyla sıralı yazılır: `.superpowers/sdd/2026-11-27-plan-11-ingilizce-arayuz/migrate.py` `add_keys(CORE, [...])`.
- Çekirdek `CurrentUICulture` okumaz; kültür parametreyle gelir.
- Commit kimliği `virtus-xd <190945580+virtus-xd@users.noreply.github.com>`. Mesaj `feat:`/`fix:`/`docs:` önekli ve `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` ile biter.

## Review Focus

1. **Kullanıcının hatırlatma metninde `{` `}` olabilir** ("a {0} b"). Duyuru çökmez, metin olduğu gibi söylenir. Metin her zaman `string.Format` bağımsız değişkeni olur, biçim dizgesine eklenmez. Test: Görev 2.
2. **en-GB gibi başka bir İngilizce kültür.** Kaynaklar nötr İngilizceye düşer, cümleler İngilizce kalır. Saat o kültürün AM/PM işaretini kullanır ("pm"). Test: Görev 1 ve 2.
3. **Türkçe Windows'ta "Otomatik".** Çağıran yer ayarı değil çözülmüş `CurrentUICulture`'ı verir. Böylece Türkçe çıkar ve İngilizce Windows'ta İngilizce. Çekirdekte test edilemez; inceleme bütün çağrı yerlerinin `CultureInfo.CurrentUICulture` verdiğine bakar.
4. **İşlev sonuçları arka plan iş parçacığında üretilebilir.** `AdaToolRunner` await sonrası iş havuzunda çalışabilir; `DefaultThreadCurrentUICulture` App kurucusunda ayarlı olduğu için doğru dil gelir. İnceleme `AdaToolRunner`'ın kültürü işlevi çağırdığı anda okuduğuna bakar.
5. **İşlev sonucu için `ReminderText.When`/`Relative`.** Modele giden bu metinler Türkçe kalmalı. Duyuru için eklenen `ui` aşırı yüklemesi eski çağrıları değiştirmemeli. Test: mevcut `When_and_relative_texts` değişmeden geçer.

---

### Task 1: Ada'nın yönergesi ve hazır cümleleri arayüz dilinde

**Files:**
- Modify: `src/CamAda.Core/Assistant/AdaInstruction.cs`
- Modify: `src/CamAda.Core/Text/CoreStrings.resx`, `src/CamAda.Core/Text/CoreStrings.tr.resx` (yardımcıyla)
- Modify: `src/CamAda/Assistant/AdaToolRunner.cs` (`GuestMode`, not sonuçları: satır ~672-673, ~729, ~734, ~794)
- Modify: `src/CamAda/Assistant/AdaAssistant.cs` (~862 `Build`, ~1231 `NoteFinished`, ~1349 `MaxDurationGoodbye`)
- Modify: `src/CamAda/Assistant/AdaProbes.cs:241`, `src/CamAda/Assistant/AdaProbes.Live.cs:64` (`Build`)
- Test: `tests/CamAda.Core.Tests/Assistant/AdaInstructionTests.cs`, `tests/CamAda.Core.Tests/Assistant/LiveSetupTests.cs:66-68`

**Interfaces:**
- Produces (hepsi `AdaInstruction`, `CultureInfo ui` son parametre):
  - `string Build(DateTime now, IReadOnlyList<MemoryItem> memory, CultureInfo ui)`
  - `string MaxDurationGoodbye(CultureInfo ui)`. Bugünkü `const` yerini alır.
  - `string NoteFinished(CultureInfo ui)`. Bugünkü `const` yerini alır.
  - `string GuestModeResult(bool on, CultureInfo ui)`, `string NoteWrittenResult(CultureInfo ui)`, `string NoteListeningResult(CultureInfo ui)`, `string NoteEndedResult(CultureInfo ui)`
  - `const string Announcer` kalır, metni dilden bağımsız olur.
- CoreStrings anahtarları (İngilizce / Türkçe):
  - `AdaSay_MaxDuration`: "That took a while; you can call me again." / "Uzun sürdü, yeniden çağırabilirsin."
  - `AdaSay_NoteTaken`: "Note taken." / "Not aldım."
  - `AdaSay_Listening`: "I'm listening" / "Dinliyorum"
  - `AdaSay_GuestOn`: "Guest mode is on; say my name first." / "Misafir kipi açık, bana adımla seslen."
  - `AdaSay_GuestOff`: "Guest mode is off." / "Misafir kipi kapandı."

- [ ] **Step 1: Write the failing tests** (`AdaInstructionTests`; mevcut `Build(Now, [])` çağrılarına `Cultures.Tr` eklenir, Türkçe beklentiler aynen kalır)

```csharp
[Fact]
public void Turkish_instruction_speaks_turkish_and_switches_on_request()
{
    var text = AdaInstruction.Build(Now, [], Cultures.Tr);
    Assert.Contains("Türkçe konuş; kullanıcı başka bir dilde konuşmanı isterse bu oturumda o dilde konuş.", text);
    Assert.DoesNotContain("Her zaman Türkçe", text);
    Assert.Contains("25 Kasım 2026 Çarşamba", text);
}

[Theory]
[InlineData("en-US")]
[InlineData("en-GB")]
public void English_instruction_speaks_english(string culture)
{
    var text = AdaInstruction.Build(Now, [], CultureInfo.GetCultureInfo(culture));
    Assert.Contains("İngilizce (English) konuş; kullanıcı başka bir dilde konuşmanı isterse bu oturumda o dilde konuş.", text);
    Assert.Contains("25 November 2026 Wednesday", text);
    Assert.Contains("Saat şu an 18:30.", text);
}

[Fact]
public void Spoken_sentences_follow_the_ui_language()
{
    Assert.Equal("(Sistem notu, kullanıcı söylemedi: oturum süresi doldu. Yalnız 'Uzun sürdü, yeniden çağırabilirsin.' de; başka bir şey söyleme ve işlev çağırma.)",
        AdaInstruction.MaxDurationGoodbye(Cultures.Tr));
    Assert.Contains("'That took a while; you can call me again.'", AdaInstruction.MaxDurationGoodbye(Cultures.En));
    Assert.Equal("(Sistem notu, kullanıcı söylemedi: not bitti. Yalnız 'Not aldım.' de.)", AdaInstruction.NoteFinished(Cultures.Tr));
    Assert.Contains("'Note taken.'", AdaInstruction.NoteFinished(Cultures.En));
    Assert.Equal("tamam: misafir kipi açık; kullanıcıya yalnız 'Misafir kipi açık, bana adımla seslen.' de", AdaInstruction.GuestModeResult(true, Cultures.Tr));
    Assert.Equal("tamam: misafir kipi kapalı; kullanıcıya yalnız 'Misafir kipi kapandı.' de", AdaInstruction.GuestModeResult(false, Cultures.Tr));
    Assert.Contains("'Guest mode is on; say my name first.'", AdaInstruction.GuestModeResult(true, Cultures.En));
    Assert.Contains("'Guest mode is off.'", AdaInstruction.GuestModeResult(false, Cultures.EnGb));
    Assert.Equal("tamam: not yazıldı; yalnız 'Not aldım.' de", AdaInstruction.NoteWrittenResult(Cultures.Tr));
    Assert.Equal("tamam: not defteri açık; kullanıcıya yalnız 'Dinliyorum' de ve bekle", AdaInstruction.NoteListeningResult(Cultures.Tr));
    Assert.Contains("'I'm listening'", AdaInstruction.NoteListeningResult(Cultures.En));
    Assert.Equal("tamam: not bitti; yalnız 'Not aldım.' de", AdaInstruction.NoteEndedResult(Cultures.Tr));
    Assert.Contains("'Note taken.'", AdaInstruction.NoteEndedResult(Cultures.En));
}
```

Mevcut `Reminder_note_and_announcer_texts` testinde `Announcer` beklentisi şuna döner: `"Sen CamAda'nın sesli asistanı Ada'sın. Sana verilen cümleyi kendi dilinde, doğal ve hafif ciddi bir tonla aynen söyle; başka hiçbir şey ekleme."`. `LiveSetupTests:66-68` `MaxDurationGoodbye(Cultures.Tr)` kullanır.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~AdaInstructionTests|FullyQualifiedName~LiveSetupTests"`
Expected: derleme hatası (`Build` üç parametre almıyor, `MaxDurationGoodbye` yöntem değil).

- [ ] **Step 3: Implement**

`AdaInstruction`:
- Dil adı `UiLanguage.IsTurkish(ui) ? "Türkçe" : "İngilizce (English)"` olur. Yönerge Türkçe kaldığı için dil adı da Türkçe yazılır; üçüncü bir dil gelirse buraya bakılır.
- İlk satır şöyle olur: `"Sen CamAda'nın sesli asistanı Ada'sın. {dil} konuş; kullanıcı başka bir dilde konuşmanı isterse bu oturumda o dilde konuş. Kısa, doğal ve hafif ciddi bir tonla."`
- Tarih `now.ToString("dd MMMM yyyy dddd", ui)` ile yazılır. Saat `InvariantCulture` olarak kalır.
- Sonuç yöntemleri bugünkü Türkçe sarmalayıcıyı tırnak içine `CoreText.Get(nameof(CoreStrings.AdaSay_…), ui)` koyarak kurar.

Çağıran yerler `System.Globalization.CultureInfo.CurrentUICulture` verir. `AdaToolRunner`'daki dört dizge sonuç yöntemleriyle değişir. Dosya yolu söyleyen not sonuçları ("Belgeler\…") olduğu gibi kalır.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet build CamAda.slnx -c Release` ardından `dotnet test tests/CamAda.Core.Tests -c Release --no-build`
Expected: 0 uyarı, 0 hata; tüm testler geçer.

- [ ] **Step 5: Commit**

`feat: Ada arayüz dilinde konuşarak açılır`

---

### Task 2: Hatırlatma duyurusu arayüz dilinde

**Files:**
- Modify: `src/CamAda.Core/Assistant/Reminders.cs` (`ReminderText.When`, `Announcement`, `Join`)
- Modify: `src/CamAda.Core/Text/CoreStrings.resx`, `CoreStrings.tr.resx` (yardımcıyla)
- Modify: `src/CamAda/App.xaml.cs:~437` (`ReminderText.Announcement(due, DateTimeOffset.Now)`)
- Test: `tests/CamAda.Core.Tests/Assistant/RemindersTests.cs`

**Interfaces:**
- Consumes: yok (Görev 1'den bağımsız).
- Produces:
  - `string ReminderText.When(DateTimeOffset at, DateTimeOffset now, CultureInfo ui)`. Mevcut `When(at, now)` bunu Türkçe ile çağırır, `Relative` değişmez.
  - `string ReminderText.Announcement(IReadOnlyList<Reminder> due, DateTimeOffset now, CultureInfo ui)`. Bugünkü iki parametreli imzanın yerini alır.
- CoreStrings anahtarları (İngilizce / Türkçe):
  - `Reminder_TimeUp`: "Time's up: {0}." / "Süre doldu: {0}."
  - `Reminder_MissedOne`: "Missed reminder: {0}, {1}." / "Kaçırılan hatırlatma: {0}, {1}."
  - `Reminder_MissedMany`: "Missed reminders: {0}." / "Kaçırılan hatırlatmalar: {0}."
  - `Reminder_Tomorrow`: "tomorrow {0}" / "yarın {0}"
  - `Reminder_OnDay`: "{0}, {1}" / "{0} {1}"
  - `Format_ReminderClock`: "h:mm tt" / "HH.mm"
  - `Format_ReminderDay`: "dddd, MMMM d" / "d MMMM dddd"
  - `List_And`: "and" / "ve"

- [ ] **Step 1: Write the failing tests** (mevcut `Announcement_joins_names` çağrılarına `Cultures.Tr` eklenir, beklentiler aynen kalır; `When_and_relative_texts` hiç değişmez)

```csharp
[Fact]
public void English_announcement()
{
    Reminder R(string text, bool missed = false, int day = 26, int hour = 14, int minute = 32) => new(Guid.NewGuid(), text, At(day, hour, minute), missed);

    Assert.Equal("Time's up: pasta.", ReminderText.Announcement([R("pasta")], Now, Cultures.En));
    Assert.Equal("Time's up: a, b and c.", ReminderText.Announcement([R("a"), R("b"), R("c")], Now, Cultures.En));
    Assert.Equal("Missed reminder: meeting, 9:00 AM.", ReminderText.Announcement([R("meeting", true, 26, 9, 0)], Now, Cultures.En));
    Assert.Equal("Time's up: pasta. Missed reminders: meeting (9:00 AM) and pills (10:00 AM).",
        ReminderText.Announcement([R("pasta"), R("meeting", true, 26, 9, 0), R("pills", true, 26, 10, 0)], Now, Cultures.En));
}

[Fact]
public void English_when_texts()
{
    Assert.Equal("2:41 PM", ReminderText.When(At(26, 14, 41), Now, Cultures.En));
    Assert.Equal("tomorrow 9:00 AM", ReminderText.When(At(27, 9, 0), Now, Cultures.En));
    Assert.Equal("Saturday, November 28, 9:00 AM", ReminderText.When(At(28, 9, 0), Now, Cultures.En));
    Assert.Equal("2:41 pm", ReminderText.When(At(26, 14, 41), Now, Cultures.EnGb));
    Assert.Equal("28 Kasım Cumartesi 09.00", ReminderText.When(At(28, 9, 0), Now, Cultures.Tr));
}

[Fact]
public void Announcement_keeps_braces_in_reminder_text()
{
    Reminder R(string text) => new(Guid.NewGuid(), text, At(26, 14, 32), false);
    Assert.Equal("Time's up: a {0} b.", ReminderText.Announcement([R("a {0} b")], Now, Cultures.En));
    Assert.Equal("Süre doldu: {x}.", ReminderText.Announcement([R("{x}")], Now, Cultures.Tr));
}
```

Not: `Now` ve `At` testlerde Türkiye saatinde (UTC+3), 26 Kasım 2026. en-GB'nin öğleden sonra işareti makinede başka çıkarsa ("PM"), o tek beklenti gerçek çıktıya göre düzeltilir ve defterde not edilir. Kural şu: işaret kültürden gelir, sabit yazılmaz.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~RemindersTests"`
Expected: derleme hatası (`Announcement` ve `When` üç parametre almıyor).

- [ ] **Step 3: Implement**

`When(at, now, ui)`:
- saat `local.ToString(Get(Format_ReminderClock), ui)` ile yazılır;
- yarın için `Format(Reminder_Tomorrow, saat)`;
- başka gün için `Format(Reminder_OnDay, local.ToString(Get(Format_ReminderDay), ui), saat)`.

`Announcement` cümleleri `CoreText.Format` ile kurar; hatırlatma metni her zaman bağımsız değişken olarak geçer. `Join` bağlacı `List_And`'den alır. Kaçırılan öğe biçimi `"{metin} ({zaman})"` iki dilde aynıdır. App çağrısına `System.Globalization.CultureInfo.CurrentUICulture` eklenir.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet build CamAda.slnx -c Release` ardından `dotnet test tests/CamAda.Core.Tests -c Release --no-build`
Expected: 0 uyarı, tüm testler geçer.

- [ ] **Step 5: Commit**

`feat: hatırlatma duyurusu arayüz dilinde`

---

### Task 3: "Ada speaks Turkish for now" notunu kaldır, belgeleri güncelle, paketle

**Files:**
- Modify: `src/CamAda/Panels/AdaPanel.xaml:21` (`TurkishOnlyNote` TextBlock silinir), `src/CamAda/Panels/AdaPanel.xaml.cs:59` (görünürlük satırı silinir; kullanılmayan `using` varsa o da)
- Modify: `UiStrings.resx`, `UiStrings.tr.resx`: `AdaPanel_TurkishOnlyNote` iki dosyadan silinir (yardımcının `load/save`'i ile)
- Modify: `README.md:25`, `README.tr.md:25`, `packaging/store-listing.md:64,76,90`
- Modify: `docs/superpowers/specs/2026-11-27-ingilizce-arayuz-design.md` (satır 79, 93, 111'deki not kararı), kısa bir "Plan 12 ile kaldırıldı" notu

**Interfaces:**
- Consumes: Görev 1 ve 2 (Ada artık arayüz dilinde konuşuyor).

- [ ] **Step 1: Kaldır ve yaz**

Belge metinleri:
- `README.md`: "Ada speaks Turkish for now; English is next." yerine "Ada speaks the interface language and switches to another language when you ask."
- `README.tr.md`: "Ada şimdilik Türkçe konuşuyor; İngilizcesi sırada." yerine "Ada arayüz dilinde konuşur, istersen başka bir dile geçer."
- `store-listing.md`:
  - 64: "The optional voice assistant Ada speaks the interface language."
  - 76: "The UI follows the Windows display language; Ada speaks the same language."
  - 90: "speaks Turkish for now." yerine "speaks your interface language."

- [ ] **Step 2: Doğrula**

Run: `git grep -n -i "turkish for now\|şimdilik Türkçe\|TurkishOnlyNote"`
Expected: yalnız `docs/superpowers/specs/2026-11-27-ingilizce-arayuz-design.md`'deki tarihçe satırları.

Run: `dotnet build CamAda.slnx -c Release` ve `dotnet test tests/CamAda.Core.Tests -c Release --no-build`
Expected: 0 uyarı, tüm testler geçer (parity ve sabit metin testleri dahil).

- [ ] **Step 3: Paketle ve kur**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/package.ps1`
Expected: son satır `...\out\msix\Camada_1.1.0_x64.msix`, hata yok.

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/install.ps1`
Expected: çıkış 0, CamAda çalışıyor.

- [ ] **Step 4: Commit**

`docs: Ada arayüz dilinde konuşuyor, "Turkish for now" notu kalktı`

---

## Bitiş

- Bütün dalı bir incelemeci (en yetenekli model) okur: tasarıma uygunluk ve Review Focus'taki beş madde.
- Kullanıcı tasarımdaki canlı deneme adımlarını uygular.
- Sonra dal `main`'e alınır, gizli depoya yüklenir ve CI izlenir.
