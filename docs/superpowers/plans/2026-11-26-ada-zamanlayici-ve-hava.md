# Ada Zamanlayıcı, Hatırlatıcı ve Hava Durumu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ada kısa süreli ve ileri tarihli hatırlatma kurar. Süre dolunca ton çalar, adada yazı çıkar ve Ada mikrofonsuz kısa bir
oturumda kendi sesiyle söyler. Hava sorularını Open-Meteo'dan ücretsiz yanıtlar.

**Architecture:** Kurallar Core'da ve birim testli: hatırlatma defteri, saat çözümü, metinler, hava ayrıştırma ve yanıt metni.
Uygulamada DPAPI dosyası, duvar saatine bakan zamanlayıcı, Ada işlevleri, Open-Meteo istemcisi, `AdaAssistant`'ta söyleme
oturumu ve adada geri sayım.

**Tech Stack:** .NET 10, WPF, Gemini Live (mevcut `GeminiLiveClient`), Open-Meteo, DPAPI, xUnit + `FakeTimeProvider`.

**Spec:** `docs/superpowers/specs/2026-11-26-ada-zamanlayici-ve-hava-design.md`

## Global Constraints

- Uyarıda mikrofon açılmaz. Söyleme oturumu Google'a yalnız yazı gönderir.
- İşlev adları: `hatirlatici_kur`, `hatirlaticilari_listele`, `hatirlatici_iptal`, `hatirlatici_ertele`, `hava_durumu`.
- Sınırlar: en çok 20 bekleyen hatırlatma, en çok 30 gün ileri, metin en çok 100 karakter, `dakika` 1–1440, erteleme yalnız son
  10 dakikada dolan hatırlatma için.
- Saklama: `%LOCALAPPDATA%\CamAda\ada-hatirlatmalar.bin`, DPAPI `CurrentUser`. Bozuksa `ada-hatirlatmalar.bozuk.bin` olarak
  yedeklenir, boş başlanır (hafıza gibi). Yazılamazsa işlev hata döner.
- Saat gösterimi Türkçe: "09.00", tarih "27 Kasım Cuma" (tr-TR).
- Uyarı cümleleri: `Süre doldu: makarna.`, birden çok için `Süre doldu: makarna ve çay.` (üç ve üstünde virgül + "ve"),
  kaçırılanlar için `Kaçırılan hatırlatma: toplantı, 09.00.`
- Adada yazı 8 sn. Geri sayım yalnız 60 dakikadan az kalan en yakın hatırlatma için. 10 dakikadan fazla kaldıysa `⏱ 42 dk`
  (yukarı yuvarlanmış dakika), azsa `⏱ 9:41`.
- Ada'nın sesi yoksa (Ada kapalı, anahtar yok, internet yok, Live hatası) yalnız ton ve yazı. Hatırlatma dolmuş sayılır,
  yinelenmez.
- Hava: Open-Meteo `current=temperature_2m,weather_code&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=7`.
  Hava kodu adı `WeatherText.Describe`, sıcaklık `WeatherText.Temperature`.
- Yeni kod yorumları ve metinler Türkçe. Testler: `dotnet test tests/CamAda.Core.Tests`. Derleme:
  `dotnet build src/CamAda -c Release` 0 uyarı. Debug denemeleri: `src\CamAda\bin\Debug\net10.0-windows10.0.22621.0\CamAda.exe --<deneme>`,
  sonuç `%LOCALAPPDATA%\CamAda\ada-deneme.log`.

## Review Focus

1. **Aynı anda dolan birden çok hatırlatma** (uykudan uyanış, aynı dakikaya iki kurulum): tek uyarıda hepsi söylenir, hiçbiri iki
   kez söylenmez. Görev 1 `Due_returns_all_due_once` ve `Announcement_joins_names`.
2. **Bozuk saat girdisi** ("25:00", "9", "21:5", "aa:bb") ve geçmiş tarih: hata döner, hatırlatma kurulmaz. Görev 1
   `Resolve_rejects_bad_clock` ve `Resolve_rejects_past_date`.
3. **Bozuk ya da okunamayan hatırlatma dosyası:** uygulama düşmez, boş defterle başlar, eski dosya yedeklenir. Görev 1
   `FromJson_garbage_is_empty`; Görev 4 denemesi bozuk dosyayla açılış.
4. **Söyleme oturumu başarısız** (internet yok, geçersiz anahtar): ton ve yazı yine çıkar, uygulama donmaz, hatırlatma
   yinelenmez. Görev 6 denemesi `--ada-duyuru-sina --gecersiz-anahtar`.
5. **Oturum sürerken ya da not alınırken dolan hatırlatma:** ikinci bir Live bağlantısı açılmaz. Oturumda aynı oturuma not
   gider, not kipinde not bitince. Görev 6, `AnnounceAsync` dallanması; oda denemesiyle.

---

### Task 1: Hatırlatma defteri, saat çözümü ve metinler (Core)

**Files:**
- Create: `src/CamAda.Core/Assistant/Reminders.cs` (`Reminder`, `ReminderBook`, `ReminderTime`, `ReminderText`)
- Test: `tests/CamAda.Core.Tests/Assistant/RemindersTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record Reminder(Guid Id, string Text, DateTimeOffset At, bool Missed = false)`
  - `public abstract record ReminderChange` with `Added(Reminder)`, `Rejected(string Reason)`.
  - `public sealed class ReminderBook(TimeProvider time)`:
    - `IReadOnlyList<Reminder> Pending` (yakından uzağa).
    - `ReminderChange Add(string text, DateTimeOffset at)`: Reasons tam metin:
      - `"en çok 20 bekleyen hatırlatma olabilir"`
      - `"en çok 30 gün sonrasına kurulabilir"`
      - `"hatırlatma metni boş"`
      - `"geçmiş bir zamana kurulamaz"`
      - Metin 100 karaktere kırpılır.
    - `NameMatchResult Cancel(string query)`: `NameMatch.Best` ile siler. `"hepsi"` hepsini siler ve `Found(-1)` döner.
    - `IReadOnlyList<Reminder> TakeDue()`: zamanı gelenleri çıkarıp döner ve "son dolanlar"a ekler. `At`'tan 2 dk'dan fazla
      geçmişse `Missed = true`.
    - `ReminderChange Snooze(int minutes)`: son 10 dk'da dolan en son hatırlatmayı yeniden kurar. Yoksa
      `Rejected("son 10 dakikada dolan bir hatırlatma yok")`.
    - `string ToJson()`, `void LoadJson(string json)` (bozuk JSON → boş), `event Action? Changed`.
  - `public static class ReminderTime`:
    `DateTimeOffset? Resolve(DateTimeOffset nowLocal, int minutes, string? clock, string? date, out string? error)`.
    - `minutes > 0` ise şimdi + dakika (1–1440).
    - Yoksa `clock` "HH:MM" (ya da "HH.MM"), isteğe bağlı `date` "YYYY-MM-DD". Tarihsiz saat geçtiyse yarın.
    - Hatalar: `"saat 'HH:MM' biçiminde olmalı"`, `"dakika 1 ile 1440 arasında olmalı"`, `"tarih 'YYYY-MM-DD' biçiminde olmalı"`,
      `"geçmiş bir zamana kurulamaz"`, `"süre ya da saat gerekli"`.
  - `public static class ReminderText`:
    - `string When(DateTimeOffset at, DateTimeOffset now)`: "14.41" (bugün), "yarın 09.00", "27 Kasım Cuma 09.00".
    - `string Relative(DateTimeOffset at, DateTimeOffset now)`: "9 dakika sonra (14.41)" (60 dk altı), yoksa `When`.
    - `string? Countdown(DateTimeOffset at, DateTimeOffset now)`: yukarıdaki biçimler; 60 dk ve üstünde null.
    - `string Announcement(IReadOnlyList<Reminder> due)`: Global Constraints'teki cümleler. Kaçırılanlar ayrı cümle, saatleriyle.

- [ ] **Step 1: Write the failing tests** (`FakeTimeProvider`; yerel saat `UtcNow` + `TimeZoneInfo.Local` yerine sabit offset
  için `new FakeTimeProvider(new DateTimeOffset(2026, 11, 26, 14, 32, 0, TimeSpan.FromHours(3)))` ve
  `SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "TR", "TR"))`)

```csharp
[Fact] public void Minutes_from_now()                 // Resolve(now, 10, null, null) == 14:42
[Fact] public void Clock_today_if_future()            // Resolve(now, 0, "17:00", null) == bugün 17:00
[Fact] public void Clock_tomorrow_if_passed()         // Resolve(now, 0, "09:00", null) == yarın 09:00
[Fact] public void Clock_with_date()                  // Resolve(now, 0, "09.00", "2026-11-28") == 28 Kasım 09:00
[Theory, InlineData("25:00"), InlineData("9"), InlineData("21:5"), InlineData("aa:bb")]
public void Resolve_rejects_bad_clock(string clock)   // null, error == "saat 'HH:MM' biçiminde olmalı"
[Fact] public void Resolve_rejects_past_date()        // ("10:00", "2026-11-25") → "geçmiş bir zamana kurulamaz"
[Fact] public void Resolve_rejects_minutes_out_of_range() // 1441 → "dakika 1 ile 1440 arasında olmalı"; 0 ve saat yok → "süre ya da saat gerekli"
[Fact] public void Add_limits()                       // 21. → Rejected("en çok 20 …"); 31 gün → "en çok 30 gün …"; "  " → "hatırlatma metni boş"; 150 karakter → 100'e kırpılır
[Fact] public void Pending_is_ordered_by_time()
[Fact] public void Due_returns_all_due_once()         // iki hatırlatma aynı dakikada: TakeDue iki döner, ikinci TakeDue boş
[Fact] public void Late_due_is_missed()               // 3 dk geç TakeDue → Missed true; 1 dk geç → false
[Fact] public void Cancel_by_name_and_all()           // "makarna" siler; "hepsi" boşaltır; benzersiz değilse Ambiguous
[Fact] public void Snooze_last_fired_within_ten_minutes() // TakeDue, 5 dk sonra Snooze(5) → Added (şimdi+5); 11 dk sonra Rejected
[Fact] public void Json_round_trips_and_FromJson_garbage_is_empty()
[Fact] public void Countdown_formats()                // 42 dk 10 sn → "⏱ 43 dk"; 9 dk 41 sn → "⏱ 9:41"; 61 dk → null
[Fact] public void When_and_relative_texts()          // "14.41", "yarın 09.00", "28 Kasım Cumartesi 09.00", "9 dakika sonra (14.41)"
[Fact] public void Announcement_joins_names()         // [makarna] "Süre doldu: makarna."; [makarna, çay] "… makarna ve çay."; [a,b,c] "… a, b ve c."; kaçırılan "Kaçırılan hatırlatma: toplantı, 09.00."
```

- [ ] **Step 2: Run** `dotnet test tests/CamAda.Core.Tests --filter RemindersTests` → FAIL (türler yok).
- [ ] **Step 3: Implement** `Reminders.cs`. JSON: `{"pending":[{"id","text","at"}],"fired":[…]}` (System.Text.Json; `at`
  ISO 8601 offsetli).
- [ ] **Step 4: Run** → PASS, ardından tüm testler.
- [ ] **Step 5: Commit** `feat: hatırlatma defteri ve metinleri (Core)`.

---

### Task 2: Hava tahmini ayrıştırma ve yanıt metni (Core)

**Files:**
- Modify: `src/CamAda.Core/Weather/OpenMeteo.cs` (`DailyForecastUrl`, `ParseReport`, `WeatherReport`, `WeatherDay`,
  `WeatherAnswer`)
- Test: `tests/CamAda.Core.Tests/Weather/WeatherTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record WeatherDay(DateOnly Date, int Code, double MaxC, double MinC, int? RainChance)`
  - `public sealed record WeatherReport(WeatherNow Now, IReadOnlyList<WeatherDay> Days)`
  - `OpenMeteo.DailyForecastUrl(double latitude, double longitude)`: Global Constraints'teki sorgu.
  - `OpenMeteo.ParseReport(string json)` → `WeatherReport?`.
  - `public static class WeatherAnswer` with
    `string For(string place, WeatherReport report, string? day, DateOnly today, CultureInfo turkish)`:
    - `day` boş ya da "bugün": `"tamam: Ankara, şimdi 8°, parçalı bulutlu; bugün en yüksek 12°, en düşük 3°, yağış olasılığı %20"`.
    - "yarın" ya da tarih: `"tamam: İzmir, yarın (27 Kasım Cuma): yağmurlu, en yüksek 19°, en düşük 11°, yağış olasılığı %70"`.
    - "hafta": `"tamam: Ankara, 7 gün:"` ve gün başına bir satır (`"26 Kasım Perşembe: …"`).
    - 7 gün dışı ya da bozuk `day`: `"hata: yalnız bugünden itibaren 7 günlük tahmin var"`.
    - Yağış olasılığı yoksa o kısım yazılmaz. Hava adı küçük harfle (`ToLower(tr)`).

- [ ] **Step 1: Write the failing tests**: Open-Meteo biçiminde küçük bir JSON örneğiyle (2 günlük `daily` dizileri):
  - `ParseReport` alanları;
  - eksik `daily` → null;
  - `For` çıktılarının dördü (bugün, yarın, tarih, hafta);
  - 8. gün hatası;
  - yağış olasılığı null → metinde yok.
- [ ] **Step 2: Run** `--filter WeatherTests` → FAIL.
- [ ] **Step 3: Implement.** Mevcut `ForecastUrl`/`ParseForecast` (adanın sıcaklığı) değişmez.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat: hava tahmini ve Ada yanıt metni (Core)`.

---

### Task 3: Yeni işlevler ve talimat (Core)

**Files:**
- Modify: `src/CamAda.Core/Assistant/AdaTools.cs`, `src/CamAda.Core/Assistant/AdaInstruction.cs`,
  `src/CamAda.Core/Assistant/LiveSetup.cs`
- Test: `tests/CamAda.Core.Tests/Assistant/AdaToolsTests.cs`, `AdaInstructionTests.cs`, `LiveSetupTests.cs`

**Interfaces:**
- Produces:
  - `AdaTools.HatirlaticiKur`, `HatirlaticilariListele`, `HatirlaticiIptal`, `HatirlaticiErtele`, `HavaDurumu` (adlar Global
    Constraints'te). Parametreler hepsi zorunlu (mevcut `Fn` düzeni), boş ya da 0 "yok" demek:
    - `hatirlatici_kur(metin STRING, dakika INTEGER "0: saat kullanılır", saat STRING "HH:MM ya da boş", tarih STRING "YYYY-MM-DD ya da boş")`
    - `hatirlatici_iptal(metin STRING "hatırlatmanın adı ya da 'hepsi'")`
    - `hatirlatici_ertele(dakika INTEGER)`
    - `hava_durumu(sehir STRING "boş: kayıtlı şehir", gun STRING "bugün | yarın | YYYY-MM-DD | hafta | boş")`
  - `guncel_bilgi` açıklamasından "hava durumu" çıkar.
  - `AdaInstruction.Build(DateTime now, …)`: tarih satırına `"Saat şu an HH:mm."` eklenir. Talimattaki zamanla değişen bilgiler
    listesinden "hava" çıkar. Eklenecek satırlar:
    - `"HATIRLATMA: Kullanıcı süre ya da saat söyleyip haber vermeni ya da hatırlatmanı isterse hatirlatici_kur çağır. Saati
      kendin hesaplama, işlevin döndürdüğü zamanı söyle."`
    - `"HAVA: Hava durumu sorularında hava_durumu çağır ve yalnız onun döndürdüğünü bir-iki cümleyle söyle."`
  - `AdaInstruction.ReminderNote(string sentence)` →
    `"(Sistem notu, kullanıcı söylemedi: hatırlatma zamanı geldi. Yalnız '{sentence}' de; başka bir şey söyleme, işlev çağırma.)"`.
  - `AdaInstruction.Announcer` (söyleme oturumu talimatı):
    `"Sen CamAda'nın sesli asistanı Ada'sın. Sana verilen cümleyi Türkçe, doğal ve hafif ciddi bir tonla aynen söyle; başka hiçbir şey ekleme."`
  - `LiveSetup.BuildSpeakOnly(string model, string voice, string instruction)`: `Build` gibi ama `tools`,
    `inputAudioTranscription` ve `realtimeInputConfig` yok.

- [ ] **Step 1: Write the failing tests**:
  - İşlev sayısı 34. Beş yeni ad listede.
  - `guncel_bilgi` açıklaması `"hava"` içermez.
  - `hava_durumu` açıklaması `"hava"` içerir.
  - Talimat `"Saat şu an 18:30."`, `"hatirlatici_kur"`, `"hava_durumu"` içerir, `"(döviz, altın, hava,"` içermez.
  - `ReminderNote("Süre doldu: makarna.")` tam metin.
  - `BuildSpeakOnly` çıktısında `setup.tools` yok, `voiceName` ve `model` var.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** → PASS (tüm testler).
- [ ] **Step 5: Commit** `feat: hatırlatma ve hava işlevleri, talimat (Core)`.

---

### Task 4: Hatırlatma dosyası, zamanlayıcı ve Ada işlevleri (App)

**Files:**
- Create: `src/CamAda/Assistant/ReminderFile.cs`, `src/CamAda/Assistant/ReminderScheduler.cs`,
  `src/CamAda/Assistant/AdaProbes.Reminders.cs` (`#if DEBUG`)
- Modify: `src/CamAda/Assistant/AdaToolContext.cs` (`ReminderFile? Reminders { get; init; }`),
  `src/CamAda/Assistant/AdaToolRunner.cs`, `src/CamAda/Assistant/AdaProbes.cs`

**Interfaces:**
- Consumes: Görev 1 ve Görev 3.
- Produces:
  - `internal sealed class ReminderFile(string directory, TimeProvider time)`:
    - `ReminderBook Book`.
    - `ReminderChange Add(string text, DateTimeOffset at)`, `NameMatchResult Cancel(string query)`,
      `ReminderChange Snooze(int minutes)`, `IReadOnlyList<Reminder> TakeDue()`.
    - Her değişiklikte DPAPI ile yazar. Yazılamazsa değişikliği geri alır ve `Rejected("hatırlatma dosyasına yazılamadı")`
      döner (`MemoryFile` düzeni).
  - `internal sealed class ReminderScheduler : IDisposable`:
    - `ReminderScheduler(ReminderFile file, Dispatcher dispatcher)`.
    - `event Action<IReadOnlyList<Reminder>>? Due` ve `event Action? Tick` (her denetimde, geri sayım için).
    - `void Start()` (2 sn'lik `DispatcherTimer`; açılışta hemen bir denetim), `void CheckNow()` (uyanınca).
  - `AdaToolRunner` işlevleri, sonuçlar tam metin:
    - kur: `"tamam: {ReminderText.When} '{metin}' hatırlatılacak"`; dakikalık için `"tamam: {Relative} '{metin}' hatırlatılacak"`.
    - listele: `"tamam: makarna: 9 dakika sonra (14.41); toplantı: yarın 09.00"` ya da `"tamam: bekleyen hatırlatma yok"`.
    - iptal: `"tamam: 'makarna' iptal edildi"`, `"tamam: bütün hatırlatmalar silindi"`, `"onay gerekiyor: hangisi? makarna, makarna 2"`
      ya da `"hata: '{metin}' adlı bir hatırlatma yok"`.
    - ertele: `"tamam: '{metin}' {Relative} yeniden hatırlatılacak"` ya da `"hata: {Reason}"`.
    - Bağlam yoksa `"hata: hatırlatmalar şu an kullanılamıyor"`.

- [ ] **Step 1: Write the Debug probe `--ada-hatirlatma-sina`** (önce başarısız olan denetim). Geçici bir klasörde
  `ReminderFile` kurar ve sahte `AdaToolContext` ile işlevleri çağırır:
  - "2 dakika sonra deneme" kur;
  - "saat 23:59" kur;
  - listele;
  - iptal "deneme";
  - "hepsi";
  - bozuk dosyayla açılış (`.bozuk.bin` yedeği oluşur).

  Her sonucu yazar. Ayrıca `ReminderScheduler`'ı 1 dakikalık bir hatırlatmayla 70 sn çalıştırır ve `Due` olayını yazar.
- [ ] **Step 2: Run** → derlenmez (FAIL).
- [ ] **Step 3: Implement** `ReminderFile`, `ReminderScheduler`, bağlam özelliği ve yürütücü durumları.
- [ ] **Step 4: Run the probe**. Beklenen:
  - sonuç metinleri yukarıdaki gibi;
  - `.bozuk.bin` var;
  - `Due` ~60 sn'de bir kez.
- [ ] **Step 5: Commit** `feat: hatırlatma dosyası, zamanlayıcı ve Ada işlevleri`.

---

### Task 5: Hava durumu işlevi (App)

**Files:**
- Create: `src/CamAda/Weather/WeatherLookup.cs`, `src/CamAda/Assistant/AdaProbes.Weather.cs` (`#if DEBUG`)
- Modify: `src/CamAda/Assistant/AdaToolContext.cs` (`WeatherLookup? Weather { get; init; }`), `AdaToolRunner.cs`, `AdaProbes.cs`

**Interfaces:**
- Consumes: Görev 2 (`WeatherReport`, `WeatherAnswer`), mevcut `OpenMeteo.GeocodeUrl`/`ParseGeocode`, `WeatherLocation`.
- Produces: `internal sealed class WeatherLookup(Func<WeatherLocation?> home)`.
  - `Task<string> AnswerAsync(string? city, string? day, CancellationToken ct)`: yer adı ve koordinatlar.
    - Şehir boşsa `home()` (koordinatı yoksa ad araması). Doluysa ad araması.
    - 10 dk'lık önbellek (yer başına).
    - 10 sn zaman aşımı. `WeatherAnswer.For` sonucunu döner.
  - Hatalar: `"hata: '{sehir}' adlı bir yer bulunamadı"`, `"hata: kayıtlı şehir yok; hangi şehir?"`,
    `"hata: hava durumuna ulaşılamadı"`.
  - `AdaToolRunner`: `AdaTools.HavaDurumu => WeatherAsync(...)`; bağlam yoksa `"hata: hava durumu şu an kullanılamıyor"`.

- [ ] **Step 1: Write the probe `--ada-hava-sina`**: kayıtlı şehir, "İzmir" yarın, "hafta", "Böyleşehiryok", gün "2027-01-01".
- [ ] **Step 2: Run** → FAIL (derlenmez).
- [ ] **Step 3: Implement.** HTTP `WeatherService`'teki gibi (`User-Agent: CamAda`, 10 sn).
- [ ] **Step 4: Run** → beş sonuç beklenen biçimde (ilk üçü `tamam:`, sonraki ikisi `hata:`), her biri <2 sn.
- [ ] **Step 5: Commit** `feat: Ada hava durumu işlevi (Open-Meteo)`.

---

### Task 6: Süre dolunca Ada'nın söylemesi (`AdaAssistant`)

**Files:**
- Modify: `src/CamAda/Assistant/AdaAssistant.cs`, `src/CamAda/Assistant/Audio/AdaSpeaker.cs` (`AdaCue.Reminder`)
- Create: `src/CamAda/Assistant/AdaProbes.Announce.cs` (`#if DEBUG`)
- Modify: `src/CamAda/Assistant/AdaProbes.cs`

**Interfaces:**
- Consumes: Görev 3 (`AdaInstruction.ReminderNote`, `Announcer`, `LiveSetup.BuildSpeakOnly`).
- Produces:
  - `AdaAssistant.Task<bool> AnnounceAsync(string sentence)`. Ada söylediyse true. Ses yoksa (Ada kapalı, anahtar yok, Live
    hatası, hoparlör yok) false; ton ve yazıyı çağıran yapar.
    - **Oturum varsa:** Not kipinde not bitince, değilse hemen `client.SendTextAsync(AdaInstruction.ReminderNote(sentence))`.
    - **Oturum yoksa söyleme oturumu:**
      1. Hoparlör (MTA'da, `PrepareAudio` gibi) ve `GeminiLiveClient.ConnectAsync(key, LiveSetup.BuildSpeakOnly(...))`.
      2. `SendTextAsync("(Sistem notu: şunu söyle: '" + sentence + "')")`.
      3. Gelen ses `AdaSpeaker`'da çalınır.
      4. `TurnComplete` gelip ses bitince ya da en geç 15 sn sonra kapanır.
      5. `Usage` olayları `CostTable.Live` ile kullanım kaydına yazılır (oturum sayısı artmaz).
    - Söyleme oturumu sürerken `Trigger`/uyanma: söyleme oturumu kapanır, normal oturum açılır.
    - Durdurulmuşken (kilit) de çalışır; `Enabled` kapalıysa false.
  - `AdaCue.Reminder`: 880 Hz 120 ms, 60 ms sessizlik, 880 Hz 120 ms, 60 ms sessizlik, 1175 Hz 160 ms.

- [ ] **Step 1: Write the probe `--ada-duyuru-sina`** (önce başarısız olan denetim). Gerçek anahtarla söyleme oturumu: "Süre
  doldu: deneme." Süre ve maliyet yazılır. `--gecersiz-anahtar` ile false dönmeli, 5 sn içinde. Mikrofon açılmaz.
- [ ] **Step 2: Run** → FAIL (derlenmez).
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the probe**. Beklenen:
  - normal: true, ses hoparlörden duyulur (kullanıcı yoksa günlükte gelen ses baytları > 0), ≤6 sn, maliyet ~0,002 $;
  - geçersiz anahtar: false, <5 sn.

  Oda denemesi (`--ada-oda-sina`) ile oturum sürerken `AnnounceAsync` çağrısı: aynı oturuma not gider, ikinci bağlantı açılmaz
  (günlükte tek bağlantı). Denemeye `--duyuru=sn` seçeneği eklenir.
- [ ] **Step 5: Commit** `feat: hatırlatmayı Ada'nın sesiyle söyleme`.

---

### Task 7: Bağlantılar, adada yazı ve geri sayım

**Files:**
- Modify: `src/CamAda/App.xaml.cs`, `src/CamAda/Island/IslandWindow.xaml` (`CompactTimer`), `src/CamAda/Island/IslandWindow.xaml.cs`

**Interfaces:**
- Consumes: Görev 4 (`ReminderFile`, `ReminderScheduler`), Görev 5 (`WeatherLookup`), Görev 6 (`AnnounceAsync`, `AdaCue.Reminder`).
- Produces:
  - `IslandWindow.Flash(FlashContent content, TimeSpan? hold = null)`: varsayılan 1,5 sn değişmez; hatırlatmada 8 sn.
  - `IslandWindow.ShowTimer(string? countdown, string? accessibleName)`: `CompactTimer` (`CompactTime`'ın solunda, 12,5 px,
    tabular rakam, sağ boşluk 10). Null ise gizli.
  - App:
    - `ReminderFile(LocalDir)` ve `WeatherLookup(() => _settings.Weather)` araç bağlamına `with { Reminders = …, Weather = … }`.
    - `ReminderScheduler.Due` olayında:
      1. `ReminderText.Announcement(due)`.
      2. `_island.Flash(new FlashContent("", null, cümle), 8 sn)` (Segoe Fluent "Stopwatch").
      3. Ton ve `AnnounceAsync`. Ada söylemediyse ton yine çalınır.

      Ton: oturum varsa onun hoparlöründen, yoksa söyleme oturumunun başında.
    - `Tick` olayında en yakın hatırlatma için `ReminderText.Countdown` → `ShowTimer`.
    - Uyanışta (`PowerModes.Resume`) `CheckNow`.

- [ ] **Step 1: Implement** yukarıdakileri. Ses yolu tek yerde olmalı: ton + söyleme `AdaAssistant`'ta; Ada kapalıyken yalnız ton
  için geçici bir `AdaSpeaker` (MTA'da) kurulur.
- [ ] **Step 2: Run** `dotnet build src/CamAda -c Release` → 0 uyarı. Ardından tüm testler.
- [ ] **Step 3: Run** Debug uygulamada değil, deneme üzerinden: `--ada-hatirlatma-sina --ucuca` (yeni seçenek). Gerçek
  `ReminderScheduler` + `AnnounceAsync` + sahte ada geri çağrısı; 1 dk'lık hatırlatma. Günlükte geri sayım metinleri
  ("⏱ 0:59" …) ve dolunca tek uyarı görünmeli.
- [ ] **Step 4: Commit** `feat: hatırlatma uyarısı ve adada geri sayım`.

---

### Task 8: Kurulum ve kullanıcının canlı denemesi

**Files:**
- Modify: `docs/superpowers/specs/2026-11-25-sesli-asistan-design.md` (işlevler listesine beş işlev, bir satırlık gönderme)

- [ ] **Step 1: Run** tüm testler ve Release derlemesi → PASS, 0 uyarı.
- [ ] **Step 2: Install** `.\tools\install.ps1`.
- [ ] **Step 3: USER STEP — canlı deneme listesi:**
  1. "2 dakika sonra çay için haber ver": Ada saati söyler, adada geri sayım; dolunca ton, adada yazı, Ada "Süre doldu: çay."
  2. "Yarın 9'da toplantıyı hatırlat": "yarın 09.00" der.
  3. "Hatırlatmalarım neler?", "çay hatırlatmasını iptal et".
  4. Dolduktan hemen sonra "Hey Ada, 5 dakika ertele".
  5. Ada'yla konuşurken dolan hatırlatma: aynı konuşmada söyler.
  6. CamAda'yı kapatıp hatırlatma saatinden sonra açma: "kaçırılan hatırlatma" söylenir.
  7. "Hava nasıl?", "yarın İzmir'de yağmur var mı?", "bu hafta hava nasıl?": hızlı yanıt.
- [ ] **Step 4: Commit** `docs: ana tasarıma hatırlatma ve hava işlevleri`.
