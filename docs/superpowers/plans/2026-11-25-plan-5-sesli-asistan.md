# Plan 5: Sesli Asistan "Ada" — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** CamAda'ya bir sesli asistan eklenir:
- "Hey Ada", adadaki mikrofon düğmesi ya da kısayol bir konuşma oturumu açar;
- Gemini Live dinler, işlev çağırır ve Türkçe konuşur;
- hoparlörde de söz kesilebilir (Windows'un yankı engelleyicisiyle);
- 25 işlev gerçekten yapılır;
- güncel bilgi Google aramasıyla gelir;
- Ada kullanıcıyı hatırlar.

**Architecture:**
- **Saf kurallar** `CamAda.Core/Assistant` altında, birim testli:
  - ayarlar ve Live iletilerinin kurulması/ayrıştırılması;
  - maliyet ve aylık sayaç;
  - oturum süre kuralları;
  - işlev tanımları, talimat, argüman ve ad eşleme;
  - WhatsApp bağlantısı, hafıza kuralları;
  - PCM dönüştürme.
- **WPF tarafı** `src/CamAda/Assistant/` altında:
  - ses (yankı engelleyicili mikrofon, düz mikrofon, hoparlör), uyandırma dinleyicisi;
  - Live istemcisi, güncel bilgi istemcisi;
  - işlev yürütücü, oturum yöneticisi (`AdaAssistant`).
- **Görünüm:** Ada'nın adadaki durumları ve `AdaPanel` (anahtar, mikrofon, harcama, hafıza).
- **Bağlantı:** App her şeyi `StartAssistant()` içinde bağlar.

**Tech Stack:**
- .NET 10, C#, WPF; `System.Text.Json`, `ClientWebSocket`.
- NAudio.Wasapi 2.2.1; Windows Voice Capture DSP (COM, kaynak üretimli `ComWrappers`).
- **Yeni paket:** `org.k2fsa.sherpa.onnx` 2.0.3 (anahtar sözcük algılayıcısı; kendi ONNX çalışma zamanıyla).
- Testler: xUnit + Microsoft.Extensions.TimeProvider.Testing.
- Google SDK'sı yok; Live ham WebSocket, arama REST.

**Spec:** `docs/superpowers/specs/2026-11-25-sesli-asistan-design.md` (onaylı, hafıza eki dahil). Çelişkide o geçerlidir.

## Yol haritası

Plan 1–4.3 ve düzeltme turu 2 main'de; Plan 4.4 (bildirimler) iptal. Bu plan `plan-5-sesli-asistan` dalında yürütülür; tasarım commit'i 184b920.

Denemelerden öğrenilenler tasarım belgesinin "Bulgular" bölümünde. Deneme kodu (C# `LiveDeneme`, Python `hey_ada.py`) geçici klasördeydi, commit edilmez. Çalışan yollar bu plana işlendi.

## Global Constraints

- **Önceki planlardan devralınanlar:**
  - Windows 11 build 26300;
  - `CamAda.Core` net10.0 ve WPF'siz;
  - arayüz Türkçe; yazı tipi Manrope, simgeler `Segoe Fluent Icons, Segoe MDL2 Assets`;
  - derleme uyarısız;
  - commit iki satırla biter:
    ```
    Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
    Claude-Session: https://claude.ai/code/session_014pqQdLGBR1cxiTZvhXJTfd
    ```
- **Proje açık olacak:** Kurulum sertifika eklemez, geliştirici modu ya da yönetici izni istemez. Her kullanıcı kendi Gemini anahtarını girer.
- **Anahtar hiçbir yere yazılmaz:** günlük, `LogCrash`, hata metni, ayar dosyası dahil. Anahtarlı URL günlüğe girmez; günlüğe giden her metinde anahtar `***` ile değiştirilir.
- **Mikrofon yalnız oturumda internete gider.** Boşta yalnız uyandırma algılayıcısı çalışır (bilgisayarda). Oturum kapanınca WebSocket kapanır ve yankı engelleyici durur.
- **Uç noktalar ve adlar (denemede doğrulandı):**
  - Live: `wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent`;
  - REST: `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`, başlık `x-goog-api-key`;
  - modeller: `models/gemini-3.8-live` ve `gemini-3.5-flash-lite`; ses `Kore`.
- **Live kurulumu:**
  - `responseModalities: ["AUDIO"]`;
  - `thinkingConfig` yalnız `{ "includeThoughts": false }`: 3.8 Live `thinkingBudget` ve `thinkingLevel`'ı 400 ile reddediyor;
  - `inputAudioTranscription` ve `outputAudioTranscription` açık;
  - `contextWindowCompression: { slidingWindow: {} }`;
  - `realtimeInputConfig.automaticActivityDetection` varsayılan hassasiyette;
  - Live'ın kendi `googleSearch` aracı **eklenmez** (model onu değil bizim işlevi seçiyordu).
- **Ses biçimleri:**
  - giriş 16 kHz mono 16 bit küçük-sonlu, `mimeType: "audio/pcm;rate=16000"`, ~100 ms parçalar;
  - çıkış 24 kHz mono 16 bit.
  - Sunucu iletileri ikili (binary) çerçevede JSON olarak gelir.
- **Yankı engelleyici:**
  - `CLSID_CWMAudioAEC {745057c7-f353-4f2d-a7ee-58434477730e}`;
  - özellik kümesi `{6f52c567-0360-4bd2-9617-ccbf1421c939}`: pid 2 = 0 (SINGLE_CHANNEL_AEC), pid 3 = VARIANT_TRUE (kaynak modu), pid 4 = `(render << 16) | capture` (etkin uç dizinleri);
  - çıkış türü PCM 16000/1/16;
  - `IMediaBuffer` kaynak üretimli COM ile (`[GeneratedComInterface]`, `[GeneratedComClass]`, `StrategyBasedComWrappers`); `DMO_OUTPUT_DATA_BUFFER` elle bellekte. Eski COM köprüsü "Typelib export" hatası verdi.
- **Uyandırma:**
  - sherpa-onnx gigaspeech 3.3M int8 (`encoder/decoder/joiner-epoch-12-avg-2-chunk-16-left-64.int8.onnx`, `tokens.txt`);
  - anahtar satırı `▁HE Y ▁A D A :1.2 #<eşik> @HEY_ADA`, `KeywordsBuf` ile bellekten;
  - `NumThreads = 1`;
  - eşik varsayılanı 0,20.
- **Süreler ve eşikler:**

  | Ne | Değer |
  |---|---|
  | Ada'nın yanıtından sonra sessizlik | 8 sn |
  | Uyandıktan sonra hiç konuşma yoksa | 6 sn |
  | En uzun oturum | 10 dk |
  | Kapatma onayı penceresi | 30 sn |
  | Yerel söz kesme | temiz mikrofon RMS > 0,04, art arda ~150 ms |
  | Yarı çift yönlü yedekte susma payı | 400 ms |
  | Ön kayıt arabelleği | 2 sn |
  | Güncel bilgi zaman aşımı | 15 sn |
  | Hafıza | 50 madde, madde başı 200 karakter |
  | Diğer sesleri kısma | %30'a |
- **Dosyalar (`%LOCALAPPDATA%\CamAda\`):**
  - `gemini-key.bin` (DPAPI);
  - `ada-hafiza.bin` (DPAPI), bozuksa `ada-hafiza.bozuk.bin` yedeği;
  - `ada-kullanim.json` (aylık sayaç; düz JSON, kişisel bilgi yok);
  - `ada-deneme.log` (yalnız Debug denemeleri).
  - Eski düz metin `gemini-anahtar.txt` bir kez içeri alınıp silinir.
- **Güvenlik:** Hiçbir işlev dosya silmez, uygulamayı zorla öldürmez (yalnız `Native.RequestClose`), mesaj göndermez. Kapatma ve yeniden başlatma `ConfirmGate` (30 sn) ile iki adımlıdır.
- **Yalnız Debug'da çalışan denemeler (komut satırı):**
  - `--ada-ses-sina`;
  - `--ada-uyandirma-sina`;
  - `--ada-baglanti-sina`;
  - `--ada-islev-sina`.

  Her biri sonucunu `ada-deneme.log`'a yazıp çıkar. Ortam değişkeni `CAMADA_FAKE_LAUNCHER` (mevcut) ve yeni `CAMADA_FAKE_POWER=<günlük yolu>` kilitleme, uyku ve kapatmayı günlüğe yazar.
- **Canlı deneme kuralları:**
  - Ses düzeyi, çıkış/giriş cihazı, parlaklık, telsiz ve medya durumu denemeden önce kaydedilir, sonra geri konur.
  - Kapatma, yeniden başlatma, kilitleme ve uyku yalnız sahte güç ve başlatıcıyla denenir.
  - Wi‑Fi'yi kapatan deneme aynı komutta geri açar ve interneti doğrular.
  - **Açık Live oturumu ses akıtır ve faturalanır:** her canlı deneme oturumu kapatarak biter; açık kalan deneme süreci öldürülür.
  - Ekranda rastgele noktaya tıklanmaz; ESC SendKeys ile gönderilmez.
  - Kullanıcının sesini isteyen denemelerde kullanıcıya ne söyleyeceği yazılır.
  - Boş (henüz bağlanmamış) bir işlev "deneme: yapılmadı" döndürür, asla "tamam" demez.

## Review Focus

1. **Oturum ortasında bağlantı kopar** (internet gider, sunucu `goAway` ya da 1011 ile kapatır). Beklenen: Ada'nın sesi durur, mikrofon akışı ve yankı engelleyici kapanır, adada "Bağlantı koptu" görünür, uyandırma algılayıcısı yeniden dinler, çökme olmaz. (Görev 8 Step 4)
2. **Oturum sırasında çıkış cihazı değişir** (kullanıcı "kulaklığa geç" der, ya da Buds2 bağlanır). Beklenen: Ada'nın sesi yeni varsayılan cihazdan sürer; yankı engelleyici yeni referansla yeniden kurulur, olmazsa yarı çift yönlüye düşer. (Görev 8 Step 4)
3. **Mikrofon yok, kullanımda ya da yankı engelleyici açılamıyor** (Sonar sanal mikrofonu gibi). Beklenen: bir kez bilgilendirilip düz mikrofonla yarı çift yönlü çalışılır; mikrofon hiç yoksa "Mikrofon bulunamadı" ve asistan duraklar. (Görev 3 Step 6, Görev 8 Step 4)
4. **Üst üste tetikleme:** oturumdayken kısayola ya da mikrofon düğmesine basmak, oturum açılırken "Hey Ada" yeniden söylemek, kısayola çift basmak. Beklenen: tek oturum; oturumdayken düğme ya da kısayol oturumu kapatır; oturumdayken uyandırma dinlenmez. (Görev 8 Step 4)
5. **Modelden bozuk argüman gelir:** sayı yerine metin ("40"), ondalık (40.0), eksik alan, bilinmeyen işlev adı. Beklenen: sayılar kabul edilir; eksikse işlev "hata: eksik bilgi: yuzde" döndürür; bilinmeyen işlev "hata: böyle bir işlev yok"; asla çökme yok. (Görev 2 Step 1)

---

### Task 1: Core — ayarlar, Live iletileri, maliyet, oturum kuralları

**Files:**
- Create:
  - `src/CamAda.Core/Settings/AssistantSettings.cs`
  - `src/CamAda.Core/Assistant/LiveSetup.cs`, `src/CamAda.Core/Assistant/LiveEvents.cs`, `src/CamAda.Core/Assistant/LiveErrors.cs`
  - `src/CamAda.Core/Assistant/CostTable.cs`, `src/CamAda.Core/Assistant/UsageLedger.cs`
  - `src/CamAda.Core/Assistant/SessionRules.cs`
- Modify: `src/CamAda.Core/Settings/AppSettings.cs` (`Assistant` özelliği), `src/CamAda.Core/Settings/SettingsStore.cs` (`Sanitize`).
- Test (`tests/CamAda.Core.Tests/` altında): `Assistant/LiveSetupTests.cs`, `Assistant/LiveEventsTests.cs`, `Assistant/LiveErrorsTests.cs`, `Assistant/CostTableTests.cs`, `Assistant/UsageLedgerTests.cs`, `Assistant/SessionRulesTests.cs`, `Settings/SettingsStoreTests.cs`.

**Interfaces:**
- **Ayarlar:**
  - `sealed record WhatsAppContact(string Name, string Phone)`
  - `sealed record AssistantSettings`. Varsayılanlar:

    | Alan | Varsayılan | Geçerli aralık |
    |---|---|---|
    | `Enabled` | `false` | |
    | `Model` | `"models/gemini-3.8-live"` | |
    | `LookupModel` | `"gemini-3.5-flash-lite"` | |
    | `Voice` | `"Kore"` | |
    | `Microphone` | `null` | |
    | `WakeThreshold` | `0.20` | 0,05–0,60 |
    | `SilenceSeconds` | `8` | 3–60 |
    | `NoSpeechSeconds` | `6` | 3–30 |
    | `MaxSessionMinutes` | `10` | 1–14 |
    | `Hotkey` | `"Win+Alt+A"` | geçersiz → varsayılan |
    | `ListenInFullScreen` | `false` | |
    | `DuckOthers` | `true` | |
    | `WhatsAppContacts` | `[]` | adı ya da telefonu boş kişi atılır |

    Boş model, ses ya da arama modeli varsayılana döner.
  - `AppSettings.Assistant { get; init; } = new()`
- **Live kurulumu:**
  - `sealed record LiveSetupOptions(string Model, string Voice, string SystemInstruction, JsonArray FunctionDeclarations)`
  - `static class LiveSetup`: `JsonObject Build(LiveSetupOptions o)`, Global Constraints'teki alanlarla.
- **Live iletileri:**
  - `static class LiveMessages`: `JsonObject Audio(ReadOnlySpan<byte> pcm16k)`, `JsonObject ToolResponse(IReadOnlyList<ToolResult> results)`
  - `sealed record ToolCall(string? Id, string Name, JsonObject Args)`
  - `sealed record ToolResult(string? Id, string Name, string Result)`
  - `sealed record UsageReport(IReadOnlyDictionary<string, long> Prompt, IReadOnlyDictionary<string, long> Response, long Thoughts, long ToolUsePrompt)`; anahtar kip adı: `"TEXT"`, `"AUDIO"` …
  - `abstract record LiveEvent` ve alt türleri: `SetupComplete`, `AudioChunk(byte[] Pcm24k)`, `InputText(string Text)`, `OutputText(string Text)`, `Interrupted`, `TurnComplete`, `ToolCalls(IReadOnlyList<ToolCall> Calls)`, `ToolCancelled(IReadOnlyList<string> Ids)`, `Usage(UsageReport Report)`, `GoAway(TimeSpan TimeLeft)`.
  - `static class LiveEvents`: `IReadOnlyList<LiveEvent> Parse(ReadOnlySpan<byte> json)`. Bozuk JSON'da boş liste döner, hata atmaz. `thought: true` parçalar atlanır. Tek iletideki olaylar şu sırayla çıkar: Interrupted, InputText, AudioChunk, OutputText, TurnComplete, sonra ToolCalls / Usage / GoAway.
- **Hatalar:**
  - `enum AssistantError { None, Network, InvalidKey, PaymentRequired, QuotaExceeded, ModelUnavailable, Microphone, Unknown }`
  - `static class LiveErrors`: `AssistantError FromClose(string? description)`, `AssistantError FromHttp(int status, string? body)`.
- **Maliyet:**
  - `static class CostTable`: `decimal Live(UsageReport u, DateOnly date)`, `decimal Lookup(long promptTokens, long outputTokens, long searchesBeforeThisMonth, int searches)`.
  - Live fiyatları ($/1M): giriş TEXT 0,75, AUDIO 3,00, diğer 1,00; çıkış AUDIO 12,00, TEXT 4,50. Düşünme ve araç tokenları da bu tablodan: düşünme = çıkış TEXT, araç = giriş TEXT.
  - Arama modeli fiyatları: giriş 0,30, çıkış 2,50. Aylık 5.000 arama ücretsiz, sonrası arama başına 0,014.
  - Tablo `DateOnly` başlangıçlı satırlar hâlinde tutulur; şimdilik tek satır, 2026-01-01'den başlar.
- **Aylık sayaç:**
  - `sealed record UsageMonth(string Month, decimal EstimatedUsd, long Sessions, long Searches)`; `Month` `"yyyy-MM"` biçiminde.
  - `static class UsageLedger`: `UsageMonth Add(UsageMonth? current, DateOnly today, decimal usd, int sessions, int searches)`.
- **Oturum kuralları:**
  - `enum SessionEnd { NoSpeech, Silence, MaxDuration }`
  - `sealed class SessionRules(TimeProvider time, TimeSpan silence, TimeSpan noSpeech, TimeSpan maxSession)`: `void Started()`, `void UserSpoke()`, `void AdaSpeaking()`, `void AdaFinished()`, `void ToolRunning(bool running)`, `SessionEnd? Check()`.

- [ ] **Step 1: Testleri yaz**
  - **`SettingsStoreTests`:**
    - `Assistant_has_spec_defaults`: tablodaki tüm değerler.
    - `Out_of_range_assistant_values_fall_back` (`[Theory]`): `WakeThreshold` 0,01 ve 0,9 → 0,20; `SilenceSeconds` 0 → 8; `MaxSessionMinutes` 30 → 10; `Hotkey` "Ctrl+" → "Win+Alt+A"; `Voice` "" → "Kore".
    - `Contacts_without_name_or_phone_are_dropped`.
  - **`LiveSetupTests`:**
    - `Setup_carries_model_voice_and_tools`: `setup.model == "models/gemini-3.8-live"`; `generationConfig.speechConfig.voiceConfig.prebuiltVoiceConfig.voiceName == "Kore"`; `tools[0].functionDeclarations` verilen dizi.
    - `Setup_has_transcription_and_compression`: `inputAudioTranscription`, `outputAudioTranscription`, `contextWindowCompression.slidingWindow` var; `realtimeInputConfig.automaticActivityDetection` var.
    - `Setup_never_sends_thinking_budget_or_level`: JSON'da `thinkingBudget` ve `thinkingLevel` yok; `thinkingConfig.includeThoughts == false`.
    - `Setup_has_no_builtin_google_search`.
    - `Audio_message_is_base64_pcm_16k`: `{0x01,0x02}` → `realtimeInput.audio.data == "AQI="`, `mimeType == "audio/pcm;rate=16000"`.
    - `Tool_response_keeps_ids`: `[("abc","ses_ayarla","tamam")]` → `toolResponse.functionResponses[0]` = `{id:"abc", name:"ses_ayarla", response:{sonuc:"tamam"}}`; `Id == null` ise `id` alanı yazılmaz.
  - **`LiveEventsTests`** (JSON örnekleri denemedeki ileti biçiminden):
    - `Setup_complete`: `{"setupComplete":{}}` → `[SetupComplete]`.
    - `Audio_chunk_decodes_base64`: `modelTurn.parts[0].inlineData.data == "AQI="` → `AudioChunk([1,2])`.
    - `Interrupted_comes_first`: `interrupted` + `inputTranscription` aynı iletide → `[Interrupted, InputText]`.
    - `Thought_parts_are_skipped`: `{"text":"**…**","thought":true}` → olay yok.
    - `Turn_complete_after_text`: `outputTranscription.text == "Tamam."` + `turnComplete` → `[OutputText("Tamam."), TurnComplete]`.
    - `Tool_calls_parse_id_name_args`: `functionCalls[0] = {id:"f1", name:"ses_ayarla", args:{yuzde:30}}` → `ToolCall("f1","ses_ayarla",{"yuzde":30})`.
    - `Usage_by_modality`: denemedeki ilk kullanım (`TEXT 1765`, `AUDIO 1` giriş; `AUDIO 48` çıkış; `thoughtsTokenCount 37`) → sözlükler ve `Thoughts == 37`.
    - `Go_away_parses_time_left`: `{"goAway":{"timeLeft":"5s"}}` → `GoAway(5 sn)`.
    - `Garbage_returns_empty`: `"{bozuk"` → boş.
  - **`LiveErrorsTests`:**
    - `FromClose`: "You exceeded your current quota…" → QuotaExceeded; "API key not valid…" → InvalidKey; "…billing…" ya da "Payment Required" → PaymentRequired; "Thinking budget is not supported for this model." → ModelUnavailable; "models/x is not found" → ModelUnavailable; null → Network; diğer → Unknown.
    - `FromHttp`: 400 ve gövdede "API key" → InvalidKey; 403 → InvalidKey; 402 → PaymentRequired; 429 → QuotaExceeded; 404 → ModelUnavailable; 503 → Network.
  - **`CostTableTests`:**
    - `Live_matches_spike_first_turn`: yukarıdaki kullanım → `0.00206925m`.
    - `Live_audio_output_dominates`: `AUDIO 1000` çıkış → `0.012m`.
    - `Lookup_free_searches_then_paid`: `(1000, 200, 4999, 2)` → `0.0003m + 0.0005m + 0.014m` = `0.0148m`; `(0, 0, 0, 1)` → `0m`.
  - **`UsageLedgerTests`:**
    - `Same_month_accumulates`: `null` + (2026-11-25, 0.5, 1, 2) → ("2026-11", 0.5, 1, 2); üstüne (0.25, 1, 0) → (0.75, 2, 2).
    - `New_month_restarts`: "2026-11" kaydı + 2026-12-01 → ("2026-12", yalnız yeni değerler).
  - **`SessionRulesTests`** (`FakeTimeProvider`; 8 sn / 6 sn / 10 dk):
    - `No_speech_ends_quietly_at_6s`: `Started()`; +5,9 sn → null; +0,2 sn → `NoSpeech`.
    - `Silence_counts_from_ada_finishing`: `UserSpoke()`, `AdaSpeaking()`, +20 sn → null; `AdaFinished()`; +7,9 sn → null; +0,2 sn → `Silence`.
    - `User_speaking_resets_silence`: `AdaFinished()`; +7 sn `UserSpoke()`; +7 sn → null.
    - `Tool_running_holds_session`: `ToolRunning(true)`; +30 sn → null; `ToolRunning(false)`, `AdaFinished()`, +8,1 sn → `Silence`.
    - `Max_duration_wins_while_talking`: `AdaSpeaking()`; +10 dk → `MaxDuration`.
- [ ] **Step 2: Testleri çalıştır, derleme hatasıyla düştüklerini gör**
  Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~Assistant|FullyQualifiedName~SettingsStore"`
  Expected: FAIL (türler yok).
- [ ] **Step 3: Türleri ve kuralları yaz**
  - Kurallar arayüz tablolarındaki gibi. JSON alan adları camelCase (Google'ın biçimi).
  - `Parse` için `JsonDocument` kullanılır; `Args` `JsonObject.Create(element)` ile kopyalanır (belge kapanınca bozulmasın).
  - `SettingsStore.Sanitize`'a `SanitizeAssistant` eklenir; kurallar mevcut `SanitizeRecord` düzeninde.
- [ ] **Step 4: Tüm testler geçmeli**
  Run: `dotnet test tests/CamAda.Core.Tests` → Expected: tamamı PASS (502 + yeniler).
- [ ] **Step 5: Commit** — `feat: sesli asistan için Core kuralları (ayarlar, Live iletileri, maliyet, oturum)`

---

### Task 2: Core — işlevler, talimat, ad eşleme, WhatsApp, hafıza

**Files:**
- Create:
  - `src/CamAda.Core/Assistant/AdaTools.cs`, `src/CamAda.Core/Assistant/AdaInstruction.cs`
  - `src/CamAda.Core/Assistant/ToolArgs.cs`, `src/CamAda.Core/Assistant/NameMatch.cs`, `src/CamAda.Core/Assistant/WhatsAppLink.cs`
  - `src/CamAda.Core/Assistant/MemoryBook.cs`, `src/CamAda.Core/Assistant/SensitiveText.cs`
- Test: `tests/CamAda.Core.Tests/Assistant/` altında `AdaToolsTests.cs`, `AdaInstructionTests.cs`, `ToolArgsTests.cs`, `NameMatchTests.cs`, `WhatsAppLinkTests.cs`, `MemoryBookTests.cs`, `SensitiveTextTests.cs`.

**Interfaces:**
- **İşlevler:** `static class AdaTools`
  - ad sabitleri: `SesAyarla = "ses_ayarla"`, `SesDegistir`, `SessizeAl`, `CikisCihaziSec`, `GirisCihaziSec`, `Mikrofon`, `ParlaklikAyarla`, `ParlaklikDegistir`, `Wifi`, `Bluetooth`, `MedyaKontrol`, `SpotifyCal`, `UygulamaAc`, `UygulamaKapat`, `WebSitesiAc`, `TarayicidaGoogleAc`, `GuncelBilgi`, `WhatsAppMesaj = "whatsapp_mesaj"`, `EkranGoruntusu`, `Kilitle`, `Uyku`, `BilgisayariKapat`, `KonusmayiBitir`, `Hatirla`, `Unut`;
  - `JsonArray Declarations()`. Parametre şemaları denemedeki gibi: `type` büyük harfle (`"OBJECT"`, `"INTEGER"` …) ve `required` dizisi.
  - Açıklamalar tasarımın "İşlevler" tablosundan, iki ayrıntıyla:
    - `guncel_bilgi`: "YALNIZ zamanla değişen bilgiler… Tarih, bilim, coğrafya, genel kültür … ÇAĞIRMA";
    - `tarayicida_google_ac`: "YALNIZ kullanıcı açıkça… Bilgi sorularında ASLA kullanma".
- **Talimat:**
  - `sealed record MemoryItem(string Topic, string Fact, DateTimeOffset UpdatedAt)`
  - `static class AdaInstruction`: `string Build(DateTime now, IReadOnlyList<MemoryItem> memory)`. Tarih tr-TR'de `dd MMMM yyyy dddd` biçiminde ("25 Kasım 2026 Çarşamba"). Hafıza bölümü "Kullanıcı hakkında bildiklerin:" başlığı ve `- konu: bilgi` satırlarıyla; boşsa "(henüz yok)".
- **Argümanlar:** `static class ToolArgs`
  - `ArgResult<int> Int(JsonObject args, string key, int min, int max)`;
  - `ArgResult<bool> Bool(JsonObject args, string key)`;
  - `ArgResult<string> Text(JsonObject args, string key)`;
  - `string? NormalizeUrl(string text)`;
  - `string GoogleSearchUrl(string query)`.
  - `readonly record struct ArgResult<T>(T? Value, string? Error)`. Hata metni `"eksik bilgi: <key>"` ya da `"geçersiz değer: <key>"`.
- **Ad eşleme:**
  - `abstract record NameMatchResult`, alt türleri `Found(int Index)`, `Ambiguous(IReadOnlyList<int> Indexes)`, `NotFound`.
  - `static class NameMatch`: `NameMatchResult Best(string spoken, IReadOnlyList<string> candidates)`.
  - Aşamalar sırayla: tam eşleşme > ad şununla başlıyor > bir sözcüğü şununla başlıyor > içeriyor. Karşılaştırma tr-TR'de büyük/küçük harfe duyarsız; sözcükler harf ya da rakam olmayan karakterlerden bölünür. İlk sonuç veren aşamada birden fazla aday varsa `Ambiguous`.
- **WhatsApp:** `static class WhatsAppLink`
  - `string? NormalizePhone(string phone)`;
  - `WhatsAppLinkResult Build(IReadOnlyList<WhatsAppContact> contacts, string person, string message)`.
  - `abstract record WhatsAppLinkResult`, alt türleri `Link(string Uri, string ContactName)`, `Ambiguous(IReadOnlyList<string> Names)`, `NoContact`.
- **Hafıza:**
  - `sealed class MemoryBook`: `const int MaxItems = 50`, `const int MaxFactLength = 200`, `IReadOnlyList<MemoryItem> Items`, `MemoryChange Remember(string topic, string fact, DateTimeOffset now)`, `bool Forget(string topic)`, `void Clear()`, `string ToJson()`, `static MemoryBook FromJson(string json)` (bozuksa `FormatException`).
  - `abstract record MemoryChange`, alt türleri `Saved(MemoryItem? Evicted)`, `Updated`, `Rejected(string Reason)`.
- **Hassas metin:** `static class SensitiveText`: `bool Looks(string text)`.

- [ ] **Step 1: Testleri yaz**
  - **`AdaToolsTests`:**
    - `Names_are_unique_snake_case`: 25 ad, hepsi `^[a-z_]+$` ve tekil.
    - `Every_declaration_has_description_and_required`: her işlevin açıklaması boş değil; parametresi olanın `required` dizisi parametre adlarını içeriyor.
    - `Lookup_and_browser_search_are_scoped`: `guncel_bilgi` açıklaması "YALNIZ zamanla değişen" içeriyor; `tarayicida_google_ac` açıklaması "ASLA" içeriyor.
  - **`AdaInstructionTests`:**
    - `Instruction_has_date_and_principles`: 2026-11-25 → "25 Kasım 2026 Çarşamba"; metinde "yapamıyorum", "emin misin", "Ekranı göremezsin" ve "hatirla" geçiyor.
    - `Memory_is_listed`: `[("kulaklık","Buds2")]` → "- kulaklık: Buds2"; boş → "(henüz yok)".
  - **`ToolArgsTests`:**
    - `Int_accepts_number_string_and_decimal` (`[Theory]`): `{"yuzde":40}`, `{"yuzde":"40"}`, `{"yuzde":40.0}` → 40; `{"yuzde":140}` → 100 (`min..max`'e sıkıştırılır).
    - `Missing_or_wrong_type_is_error`: `{}` → `Error == "eksik bilgi: yuzde"`; `{"yuzde":"çok"}` → `"geçersiz değer: yuzde"`.
    - `Bool_accepts_true_false_strings`: `true`, `"true"`, `"evet"` → true.
    - `Url_normalization` (`[Theory]`):
      - `"https://www.youtube.com"` → aynı;
      - `"youtube.com"` → `"https://youtube.com"`;
      - `"YouTube"` → `"https://youtube.com"`;
      - `"http://a.b/c?d=1"` → aynı;
      - `"file:///C:/x"`, `"javascript:alert(1)"`, `""`, `"a b"` → `null`.
    - `Google_search_url`: `"Ankara hava"` → `"https://www.google.com/search?q=Ankara%20hava"`.
  - **`NameMatchTests`:**
    - `Exact_beats_prefix`: `"Steam"`, `["Steam VR","Steam"]` → `Found(1)`.
    - `Word_start_finds_device`: `"Buds"`, `["Hoparlör (Realtek(R) Audio)","Kulaklıklar (Buds2)"]` → `Found(1)`.
    - `Prefix_finds_plural`: `"kulaklık"`, aynı liste → `Found(1)`.
    - `Turkish_case`: `"DİSCORD"` → `["Discord"]`'da `Found(0)`; `"ıtunes"` → `["iTunes"]`'da `NotFound` (tr-TR'de ı ≠ i).
    - `Ambiguous_when_many_match`: `"Sonar"`, dört "SteelSeries Sonar - …" → `Ambiguous` (4 dizin).
    - `Not_found`: `"Photoshop"` → `NotFound`.
  - **`WhatsAppLinkTests`:**
    - `Phone_normalization` (`[Theory]`): `"0555 111 22 33"` → `"905551112233"`; `"+90 555 111 2233"` → `"905551112233"`; `"905551112233"` → aynı; `"12"` → `null`.
    - `Builds_link_with_encoded_text`: kişi `("Ahmet","05551112233")`, mesaj `"geliyorum, 5 dk"` → `Link("whatsapp://send?phone=905551112233&text=geliyorum%2C%205%20dk","Ahmet")`.
    - `Unknown_contact`: `"Mehmet"` → `NoContact`; iki "Ahmet …" kişi → `Ambiguous`.
  - **`SensitiveTextTests`** (`[Theory]`):
    - true: `"4111 1111 1111 1111"` (Luhn), `"TR33 0006 1005 1978 6457 8413 26"`, `"10000000146"` (geçerli TC kimlik), `"şifrem 1234"`, `"parola: abc"`, `"cvv 123"`;
    - false: `"kulaklık deyince Buds2"`, `"Ankara'da oturuyor"`, `"0555 111 22 33"` (telefon hassas sayılmaz).
  - **`MemoryBookTests`** (`FakeTimeProvider`):
    - `Remember_saves_and_updates_same_topic`: ("şehir","Ankara") → `Saved(null)`; ("Şehir","İzmir") → `Updated`; tek madde "İzmir".
    - `Rejects_sensitive_and_too_long`: kart numarası → `Rejected("hassas bilgi")`; 201 karakter → `Rejected("çok uzun")`; boş konu → `Rejected("eksik bilgi")`.
    - `Fifty_first_evicts_oldest_updated`: 50 madde; 1. madde sonra güncellenir; 51. eklenir → `Saved(Evicted: 2. madde)`.
    - `Forget_and_clear`: `Forget("ŞEHİR")` → true; yine → false; `Clear()` → boş.
    - `Json_round_trip`: `FromJson(ToJson())` aynı maddeleri verir; `FromJson("{")` → `FormatException`.
- [ ] **Step 2: Testler düşmeli**
  Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~Assistant"` → Expected: FAIL.
- [ ] **Step 3: Yaz**
  - **Talimat metni:** denemede doğrulanan ilkeler (tasarım "Talimat") ve hafıza kuralı: "Önemli kişisel bilgi, tercih ya da düzeltmeyi `hatirla` ile kaydet ve kaydettiğini söyle; şifre, kart, IBAN, kimlik numarası kaydetme."
  - **`SensitiveText`:**
    - 13–19 haneli, Luhn'u tutan dizi (boşluk ve tire yok sayılır);
    - `TR` + 24 hane;
    - TC kimlik algoritmasını geçen 11 hane;
    - anahtar sözcükler "şifre", "parola", "password", "cvv", "pin kodu", "iban".
- [ ] **Step 4: Tüm testler geçmeli**
  Run: `dotnet test tests/CamAda.Core.Tests` → Expected: PASS.
- [ ] **Step 5: Commit** — `feat: Ada'nın işlevleri, talimatı ve hafıza kuralları (Core)`

---

### Task 3: Ses — yankı engelleyicili mikrofon, düz mikrofon, hoparlör

**Files:**
- Create:
  - `src/CamAda.Core/Audio/Downsampler16k.cs`, `src/CamAda.Core/Audio/PreRollBuffer.cs`
  - `src/CamAda/Assistant/Audio/IAdaMic.cs`, `src/CamAda/Assistant/Audio/EchoCancelledMic.cs`, `src/CamAda/Assistant/Audio/PlainMic.cs`
  - `src/CamAda/Assistant/Audio/AdaSpeaker.cs`, `src/CamAda/Assistant/Audio/MicDevices.cs`
  - `src/CamAda/Assistant/AdaProbes.cs` (yalnız Debug)
- Modify: `src/CamAda/App.xaml.cs` (Debug'da `--ada-ses-sina`).
- Test: `tests/CamAda.Core.Tests/Audio/Downsampler16kTests.cs`, `tests/CamAda.Core.Tests/Audio/PreRollBufferTests.cs`.

**Interfaces:**
- **Core:**
  - `sealed class Downsampler16k(int inputRate, int channels)`: `short[] Process(ReadOnlySpan<float> interleaved)`. Kanallar ortalanır; her çıkış örneği oran penceresindeki girişlerin ortalaması; durum parçalar arasında korunur.
  - `static class Pcm`: `byte[] ToBytes(short[] samples)`, `double Rms(ReadOnlySpan<short> samples)` (0–1).
  - `sealed class PreRollBuffer(int capacitySamples)`: `void Write(ReadOnlySpan<short>)`, `short[] Drain()`.
- **Mikrofon arayüzü:** `interface IAdaMic : IDisposable`: `event Action<short[]>? Samples` (16 kHz mono, ses iş parçacığında), `event Action<string>? Failed`, `bool EchoCancelled`, `void Start()`.
- **Gerçekleştirmeler:**
  - `sealed class EchoCancelledMic(int captureIndex, int renderIndex) : IAdaMic`: denemedeki `AecCapture`; ~100 ms'lik parçalar, `EchoCancelled = true`.
  - `sealed class PlainMic(MMDevice device) : IAdaMic`: `WasapiCapture` + `Downsampler16k`, `EchoCancelled = false`.
- **Hoparlör:** `sealed class AdaSpeaker : IDisposable`: `AdaSpeaker(MMDevice device)`, `void Add(byte[] pcm24k)`, `void Clear()`, `bool IsPlaying`, `void PlayCue(AdaCue cue)`, `MMDevice Device`.
  - `enum AdaCue { Wake, Error }`: Wake = 660 Hz → 880 Hz, 2×80 ms; Error = 440 Hz, 200 ms; ikisi de −18 dBFS, yumuşak giriş ve çıkışlı.
- **Cihazlar:** `static class MicDevices`
  - `MMDevice ResolveMic(string? nameHint)`: ipucu ad parçasıysa ilk eşleşen etkin kayıt cihazı, yoksa varsayılan iletişim mikrofonu; hiç yoksa `null`.
  - `MMDevice DefaultSpeaker()`;
  - `(int Capture, int Render) DspIndexes(MMDevice mic, MMDevice speaker)`: etkin uç listelerindeki sıra.

- [ ] **Step 1: Core testlerini yaz**
  - `Stereo_48k_to_mono_16k`: 4800 kare sabit (0,5 / 0,5) → 1600 örnek, her biri ~16383 (±1).
  - `Channels_are_averaged`: sol 1,0, sağ 0 → ~16383.
  - `Chunks_keep_count`: 48 kHz'te 1000 karelik 3 parça → toplam 1000 örnek (±1).
  - `Rate_44100`: 4410 kare → 1600 örnek (±1).
  - `Rms`: sabit 16384 → 0,5 (±0,001); boş → 0.
  - **`PreRollBuffer` (32000 örnek):**
    - 40000 örnek yazılır → `Drain()` son 32000'i sırayla verir;
    - ikinci `Drain()` boş döner.
- [ ] **Step 2: Testler düşmeli, sonra Core'u yaz, testler geçmeli**
  Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~Audio"`.
- [ ] **Step 3: WPF ses sınıflarını yaz**
  - `EchoCancelledMic`, denemedeki `AecCapture`'ın düzenlenmiş hâli (Global Constraints).
  - `AdaSpeaker`: `BufferedWaveProvider(24000,16,1)` → `WdlResamplingSampleProvider` → kanal çoğaltma → `WasapiOut(device, Shared, true, 60)`.
- [ ] **Step 4: Debug denemesi `--ada-ses-sina`**
  - Ayardaki mikrofonla `EchoCancelledMic` 3 sn çalışır.
  - Bu sırada `AdaSpeaker` `PlayCue(Wake)` çalar.
  - `ada-deneme.log`'a `ada-ses-sina: <bayt> bayt, tepe <x>, yankı engelli <true/false>, hata <yok|metin>` yazılır ve süreç çıkar.
- [ ] **Step 5: Canlı doğrula**
  - Run: `bin\Debug\...\CamAda.exe --ada-ses-sina` (çalışan CamAda'ya dokunmaz).
  - Expected: Realtek mikrofonuyla ~96.000 bayt (±%10), `yankı engelli true`, `hata yok`; bip duyulur.
- [ ] **Step 6: Yedeği doğrula (Review Focus 3)**
  - **Kural:** DSP başladıktan sonra 1 sn içinde hiç örnek gelmezse ya da `Failed` olursa `PlainMic`'e düşülür (`Failed("ses gelmiyor")`).
  - **Önce kodla zorla:** Debug'da `CAMADA_ADA_NO_AEC=1` ile DSP başarısız sayılır. Beklenen: deneme `PlainMic`'e düşer ve `yankı engelli false`, `hata yok` yazar.
  - **Sonra Sonar'la dene:** Ayardaki mikrofon `"SteelSeries Sonar - Microphone"` yapılır, sonuç (bayt, tepe) günlüğe yazılır ve ayar geri alınır.
    - Denemede DSP Sonar'dan veri verdiği hâlde kullanıcının sesi duyulmamıştı. Bu durum kodla ayırt edilemez; çaresi paneldeki mikrofon seçimi (Görev 9).
    - Sonar sonucu yalnız kaydedilir, başarı ölçütü değildir.
- [ ] **Step 7: Commit** — `feat: Ada için yankı engelleyicili mikrofon ve hoparlör`

---

### Task 4: Uyandırma — "Hey Ada"

**Files:**
- Create:
  - `src/CamAda.Core/Assistant/WakeWord.cs`
  - `src/CamAda/Assistant/WakeWordListener.cs`
  - `src/CamAda/Assets/WakeWord/` altında `encoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx`, `decoder-…int8.onnx`, `joiner-…int8.onnx`, `tokens.txt`, `LICENSE.txt`
- Modify:
  - `src/CamAda/CamAda.csproj`: `org.k2fsa.sherpa.onnx` 2.0.3 ve gerekiyorsa win-x64 çalışma zamanı paketi; `Assets\WakeWord\*` → `CopyToOutputDirectory=PreserveNewest`.
  - `src/CamAda/Assistant/AdaProbes.cs`: `--ada-uyandirma-sina`.
- Test: `tests/CamAda.Core.Tests/Assistant/WakeWordTests.cs`.

**Interfaces:**
- `static class WakeWord`: `const string Tokens = "▁HE Y ▁A D A"`, `const string Label = "HEY_ADA"`, `const double Boost = 1.2`, `string KeywordLine(double threshold)`. Sayılar `CultureInfo.InvariantCulture` ile yazılır.
- `sealed class WakeWordListener : IDisposable`
  - kurucu: `WakeWordListener(string modelDir, double threshold, Func<MMDevice?> mic)`;
  - `event Action? Detected` (algılama iş parçacığında);
  - `void Start()`, `void Stop()` (mikrofonu bırakır; `Stop` sonrası `Start` çalışır);
  - `short[] DrainPreRoll()`: son 2 sn;
  - `bool IsListening`.

- [ ] **Step 1: Test yaz**
  - `Keyword_line_uses_invariant_numbers`: `CurrentCulture = tr-TR` iken `KeywordLine(0.2)` → `"▁HE Y ▁A D A :1.2 #0.2 @HEY_ADA"`.
- [ ] **Step 2: Düşmeli, yaz, geçmeli**
  Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~WakeWord"`.
- [ ] **Step 3: Model dosyalarını ekle**
  - Kaynak: `https://github.com/k2-fsa/sherpa-onnx/releases/download/kws-models/sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01.tar.bz2`. Yalnız int8 üçlü ve `tokens.txt` alınır (~5,3 MB).
  - `LICENSE.txt`: "Apache License 2.0", kaynak adresi, model adı.
  - Arşiv geçici klasörde ayrı bir dizine açılır.
- [ ] **Step 4: `WakeWordListener`'ı yaz**
  - `KeywordSpotterConfig`: `FeatConfig.SampleRate = 16000`, `FeatureDim = 80`, `ModelConfig.Transducer.{Encoder,Decoder,Joiner}`, `ModelConfig.Tokens`, `NumThreads = 1`, `Provider = "cpu"`, `KeywordsBuf = WakeWord.KeywordLine(threshold)` (`KeywordsBufSize` bayt uzunluğu), `KeywordsThreshold = threshold`, `KeywordsScore = 1.2`.
  - Ses `PlainMic` ile alınır. `AcceptWaveform(16000, float[])`, `IsReady`/`Decode`/`GetResult`; anahtar boş değilse `Reset` ve `Detected`.
  - `PreRollBuffer(32000)` her parçayla beslenir.
- [ ] **Step 5: Debug denemesi `--ada-uyandirma-sina`**
  - 60 sn dinler; her algılamada `ALGILANDI <saat>` yazar.
  - 15 sn'de bir `İŞLEMCİ %<tek çekirdek>` yazar (`Process.TotalProcessorTime` farkı).
- [ ] **Step 6: Canlı doğrula (kullanıcıyla)**
  - Kullanıcıya söylenecekler: aralıklarla 5 kez "Hey Ada", araya iki "Ada" içeren normal cümle.
  - Expected: ≥ 4 `ALGILANDI`; normal cümlelerde 0 (bir yanlış tetiklenme kabul edilebilir, günlüğe not edilir); `İŞLEMCİ` ≤ %5.
- [ ] **Step 7: Commit** — `feat: "Hey Ada" uyandırma dinleyicisi (sherpa-onnx)`

---

### Task 5: Live istemcisi, güncel bilgi istemcisi, anahtar deposu

**Files:**
- Create: `src/CamAda/Assistant/GeminiKeyStore.cs`, `src/CamAda/Assistant/GeminiLiveClient.cs`, `src/CamAda/Assistant/CurrentInfoClient.cs`, `src/CamAda/Assistant/AssistantException.cs`
- Modify: `src/CamAda/Assistant/AdaProbes.cs` (`--ada-baglanti-sina`).

**Interfaces:**
- **Anahtar deposu:** `sealed class GeminiKeyStore(string directory)`
  - `string? Load()`, `void Save(string key)`, `void Clear()`;
  - `bool ImportLegacy(string legacyPath)`: BOM ve boşluk kırpılır, kaydedilir, düz dosya silinir; dosya yoksa false.
  - Yöntem `SpotifyTokenStore` ile aynı (DPAPI, CurrentUser).
- **Hata türü:** `sealed class AssistantException(AssistantError error, string message) : Exception`.
- **Live istemcisi:** `sealed class GeminiLiveClient : IAsyncDisposable`
  - `static Task<GeminiLiveClient> ConnectAsync(string apiKey, JsonObject setup, CancellationToken ct)`: `SetupComplete` gelene kadar bekler (en çok 10 sn). Başaramazsa `AssistantException(LiveErrors.FromClose(...))` atar.
  - `event Action<LiveEvent>? Received`, `event Action<AssistantError>? Closed`: alma iş parçacığında. Normal kapanışta `Closed(None)`.
  - `Task SendAudioAsync(short[] pcm16k)`, `Task SendToolResultsAsync(IReadOnlyList<ToolResult> results)`, `Task CloseAsync()`.
- **Güncel bilgi:**
  - `sealed record LookupResult(bool Ok, string Text, bool Searched, long PromptTokens, long OutputTokens, AssistantError Error)`
  - `sealed class CurrentInfoClient(Func<string?> apiKey, Func<string> model)`: `Task<LookupResult> AskAsync(string question, CancellationToken ct)`. İstem denemedeki gibi; zaman aşımı 15 sn; `groundingMetadata.webSearchQueries` varsa `Searched = true`.

- [ ] **Step 1: `GeminiKeyStore`'u yaz**
  Kayıtlı anahtar düz metin olarak diskte durmaz; denetimi Step 4'te.
- [ ] **Step 2: `GeminiLiveClient`'ı yaz**
  - Gönderimler `SemaphoreSlim` ile sıraya girer.
  - İletiler parçalanarak gelebilir; `EndOfMessage`'a kadar birleştirilir.
  - **Anahtar:** önce `Options.SetRequestHeader("x-goog-api-key", key)` ile denenir. Kurulum anahtar hatasıyla reddedilirse URL'deki `?key=` ile yeniden denenir. Hangisinin çalıştığı Step 4'te günlüğe yazılır ve kod yalnız çalışan yolu tutar.
- [ ] **Step 3: `CurrentInfoClient`'ı yaz**
  HTTP hatası `LiveErrors.FromHttp` ile sınıflandırılır.
- [ ] **Step 4: Debug denemesi `--ada-baglanti-sina` ve canlı doğrulama**
  - **Akış:**
    1. Eski düz metin anahtar varsa içeri alınır.
    2. Ses göndermeden `ConnectAsync` → `SetupComplete` → `CloseAsync` (talimat ve işlevler Task 2'den).
    3. `AskAsync("Bugün 1 ABD doları kaç Türk lirası?")`.
    4. `AskAsync` geçersiz anahtarla ("x") denenir.
  - **Expected (günlük):**
    - `kurulum tamam (yol: başlık|url)`;
    - `arama: Searched=true`, metinde "TL" ve bir sayı;
    - geçersiz anahtar → `InvalidKey`;
    - `gemini-anahtar.txt` artık yok, `gemini-key.bin` var.
  - **Anahtar sızıntı denetimi:** günlükte ve `crash.log`'da anahtarın ilk 8 karakteri geçmiyor.
- [ ] **Step 5: Commit** — `feat: Gemini Live ve güncel bilgi istemcileri, anahtar deposu`

---

### Task 6: İşlevlerin gerçek hâli ve hafıza dosyası

**Files:**
- Create:
  - `src/CamAda/Assistant/AdaToolRunner.cs`, `src/CamAda/Assistant/AdaToolContext.cs`, `src/CamAda/Assistant/MemoryFile.cs`
  - `src/CamAda/Assistant/IPowerActions.cs` (`SystemPowerActions`, Debug'da `RecordingPowerActions`)
  - `src/CamAda/Panels/ItemLauncher.cs` (`StartPanel.Open`'dan taşınır)
- Modify:
  - `src/CamAda/Audio/AudioService.cs`: kayıt cihazları;
  - `src/CamAda/Panels/StartPanel.xaml.cs`: `ItemLauncher`'ı kullanır;
  - `src/CamAda/Assistant/AdaProbes.cs`: `--ada-islev-sina`.

**Interfaces:**
- **`AudioService` eklemeleri:** `IReadOnlyList<AudioOutput> Inputs()` (etkin kayıt cihazları, varsayılan işaretli), `void SetDefaultInput(string deviceId)` (`PolicyConfig.SetDefaultEndpoint`; üç rol).
- **Güç:** `interface IPowerActions { void Shutdown(); void Reboot(); }`
- **Bağlam:** `sealed record AdaToolContext`
  - servisler: `AudioService? Audio`, `BrightnessService? Brightness`, `RadioService? Radios`, `NowPlayingService? Media`, `SpotifyClient? Spotify`, `AppIndex Apps`;
  - `IQuickLauncher Launcher`, `IPowerActions Power`;
  - `Func<IReadOnlyList<(nint Hwnd, string AppName)>> OpenWindows`: ManagedShell `Tasks.GroupedWindows`'tan;
  - `MemoryFile Memory`, `CurrentInfoClient Info`, `Func<AssistantSettings> Settings`;
  - `Action EndSessionAfterSpeech`, `Action<int> SearchesUsed`.
- **Yürütücü:** `sealed class AdaToolRunner(AdaToolContext ctx, TimeProvider time)`: `Task<string> RunAsync(ToolCall call)`.
  - Dönüş metni `"tamam: …"`, `"hata: …"` ya da `"onay gerekiyor: …"` ile başlar.
  - İçeride `ConfirmGate(time, 30 sn)`.
- **Hafıza dosyası:** `sealed class MemoryFile(string directory, TimeProvider time)`: `MemoryBook Book`, `MemoryChange Remember(string topic, string fact)`, `bool Forget(string topic)`, `void Clear()`. Her değişiklikte DPAPI ile yazılır; bozuk dosya yedeklenip boş kitapla başlanır.

- [ ] **Step 1: `ItemLauncher`'ı taşı**
  `StartPanel` davranışı değişmez; mevcut testler geçmeli.
- [ ] **Step 2: `AudioService` kayıt cihazlarını ekle**
  `Inputs()` çıkış listesiyle aynı biçimde.
- [ ] **Step 3: `AdaToolRunner`'ı yaz**
  - Her işlevin davranışı tasarımın "İşlevler" tablosundaki gibi. Argümanlar `ToolArgs`, adlar `NameMatch` ile.
  - **Metin kuralları (Türkçe, kısa):**

    | İşlev ya da durum | Dönüş |
    |---|---|
    | Servis yok | `"hata: <iş> bu bilgisayarda kullanılamıyor"` |
    | `cikis_cihazi_sec` / `giris_cihazi_sec`, birden çok aday | `"hata: birden fazla cihaz uyuyor: A, B; hangisi?"` |
    | `cikis_cihazi_sec` / `giris_cihazi_sec`, aday yok | `"hata: '<ad>' adlı cihaz yok; olanlar: …"` |
    | `ses_degistir` | şimdiki düzey + fark, sınırlı; `"tamam: ses %40"` |
    | `medya_kontrol` | "oynat" yalnız çalmıyorsa, "duraklat" yalnız çalıyorsa geçiş yapar |
    | `mikrofon` (`acik=false`) | `"tamam: mikrofon kapatıldı; konuşma bitiyor"` ve `EndSessionAfterSpeech()` |
    | `uygulama_ac` | `AppIndex`'in `SearchKind.App` öğeleri |
    | `uygulama_kapat` | eşleşen uygulamanın her penceresine `Native.RequestClose`; `"tamam: Chrome'un 2 penceresine kapatma isteği gönderildi"`; yoksa `"hata: açık '<ad>' penceresi yok"` |
    | `web_sitesi_ac` | `NormalizeUrl` null ise `"hata: geçersiz adres"` |
    | `guncel_bilgi` | başarılıysa metin; değilse `"hata: güncel bilgiye ulaşılamadı"`; arama sayısı `SearchesUsed(1)` ile |
    | `whatsapp_mesaj` | bağlantı açılır; `"tamam: <kişi> sohbeti mesaj hazır açıldı; göndermek kullanıcıda"`. `NoContact` → `"hata: '<kişi>' rehberde yok; kişiler ayar dosyasında (Assistant.WhatsAppContacts)"` |
    | `bilgisayari_kapat`, ilk çağrı | `"onay gerekiyor: kullanıcıya 'emin misin?' diye sor; onaylarsa 30 sn içinde yeniden çağır"` |
    | `bilgisayari_kapat`, onaylı ikinci çağrı | `Power.Shutdown()` ya da `Reboot()` |
    | `hatirla` | `Saved` → `"tamam: kaydedildi"` (+ çıkan madde varsa adı); `Rejected` → `"hata: kaydedilmedi: <neden>"` |
    | `konusmayi_bitir` | `EndSessionAfterSpeech()` ve `"tamam"` |
    | `spotify_cal` | Task 7'ye kadar `"hata: deneme: Spotify çalma henüz bağlanmadı"` |
    | Bilinmeyen ad | `"hata: böyle bir işlev yok"` |
    | Argüman hatası | `"hata: " + ArgResult.Error` |
  - Her işlem `try/catch` içinde; beklenmeyen hata `LogCrash` + `"hata: beklenmeyen bir sorun oldu"`.
- [ ] **Step 4: Debug denemesi `--ada-islev-sina`**
  - Gerçek servislerle bir dizi çağrı yapar ve sonuçları yazar.
  - **Ses:** önce kayıt; `ses_ayarla(35)`, `ses_degistir(-5)` → %30; sonra geri.
  - **Cihaz:** `cikis_cihazi_sec("hoparlör")` → Realtek; sonra geri.
  - **Parlaklık:** önce kayıt; `parlaklik_ayarla(60)`; sonra geri.
  - **Medya:** `medya_kontrol("duraklat")` yalnız çalan bir şey varsa; sonra eski durum.
  - **Uygulamalar:** `uygulama_ac("Not Defteri")` → açılır; `uygulama_kapat("Not Defteri")` → kapanma isteği. Kapanış yalnız deneme açtığı Not Defteri için yapılır; açık başka Not Defteri varsa bu adım atlanır.
  - **Tarayıcı ve güncel bilgi:** `web_sitesi_ac("youtube")`, `guncel_bilgi("gram altın kaç TL")`.
  - **WhatsApp:** `whatsapp_mesaj("Yok Kişi","x")` → rehberde yok.
  - **Güç:** `bilgisayari_kapat(false)` iki kez, `CAMADA_FAKE_POWER` ile → önce "onay gerekiyor", sonra sahte günlükte `shutdown`.
  - **Hafıza:** `hatirla("deneme","silinecek")` → `unut("deneme")`.
  - **Bozuk argümanlar:** `ses_ayarla({})` → eksik bilgi; `ses_ayarla({"yuzde":"40"})` → tamam; `yok_islev` → böyle bir işlev yok.
  - Expected: her satır beklenen önekle; durumlar geri yüklenmiş.
- [ ] **Step 5: Commit** — `feat: Ada işlevlerinin gerçek hâli ve şifreli hafıza`

---

### Task 7: Spotify'da çalma

**Files:**
- Modify:
  - `src/CamAda.Core/Spotify/SpotifyAuth.cs`: `Scopes` + `" user-modify-playback-state"`;
  - `src/CamAda.Core/Spotify/SpotifyAuth.cs`: `SpotifyToken`'a `string Scope`; yanıttaki `scope` saklanır;
  - `src/CamAda.Core/Spotify/SpotifyJson.cs`: arama ve cihaz ayrıştırma;
  - `src/CamAda/Spotify/SpotifyClient.cs`: `PlayAsync`;
  - `src/CamAda/Spotify/SpotifyConnectView.xaml(.cs)`: "İzni yenile";
  - `src/CamAda/Assistant/AdaToolRunner.cs`: `spotify_cal`.
- Test: `tests/CamAda.Core.Tests/Spotify/SpotifyTests.cs`.

**Interfaces:**
- **Core:**
  - `sealed record SpotifyPick(string? ContextUri, string? TrackUri, string Title)`
  - `static class SpotifyJson`: `SpotifyPick? PickFromSearch(string json, string query)`, `string? PickComputerDevice(string devicesJson)`.
  - Seçim kuralı:
    1. Adı sorguya tam eşit (tr-TR, büyük/küçük harf yok sayılır) bir sanatçı varsa sanatçı bağlamı (`spotify:artist:…`).
    2. Yoksa ilk şarkı.
    3. O da yoksa ilk çalma listesi.
  - `bool SpotifyToken.HasScope(string scope)`.
- **İstemci:**
  - `enum SpotifyPlayOutcome { Playing, NotConnected, NeedsReconnect, NoDevice, NotFound, PremiumRequired, Failed }`
  - `SpotifyClient.PlayAsync(string query, Func<Task> openSpotifyApp)` → `Task<(SpotifyPlayOutcome Outcome, string? Title)>`.
  - Akış:
    1. `GET /v1/search?q=…&type=artist,track,playlist&limit=1`.
    2. `PUT /v1/me/player/play`.
    3. 404 `NO_ACTIVE_DEVICE` → `openSpotifyApp` ve en çok 8 sn `GET /v1/me/player/devices` yoklaması (500 ms); bilgisayar cihazı bulununca `PUT …/play?device_id=`.
    4. 403 ve gövdede "scope" → `NeedsReconnect`; 403 "PREMIUM" → `PremiumRequired`.

- [ ] **Step 1: Core testlerini yaz**
  - `Artist_exact_match_plays_artist`: sorgu "Tarkan"; aramada sanatçı "Tarkan" ve şarkı "Şımarık" → `ContextUri = "spotify:artist:…"`, `Title = "Tarkan"`.
  - `Otherwise_first_track`: sorgu "Şımarık" → `TrackUri`, `Title = "Tarkan — Şımarık"`.
  - `Nothing_found_is_null`.
  - `Computer_device_is_picked`: cihazlardan `type == "Computer"` olan.
  - `Token_scope`: "a b user-modify-playback-state" → `HasScope("user-modify-playback-state") == true`; eski belirteç (scope yok) → false.
- [ ] **Step 2: Düşmeli, yaz, geçmeli**
  Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~Spotify"`.
- [ ] **Step 3: İstemci, "İzni yenile" ve `spotify_cal` bağlantısı**
  - Bağlı ama izni eksik belirteçte müzik panelinde "Spotify izni yenilenmeli — İzni yenile" görünür; mevcut bağlanma akışını yeniden çalıştırır.
  - `spotify_cal` sonuçları:
    - `"tamam: <Title> çalıyor"`;
    - `NeedsReconnect` → `"hata: Spotify izninin yenilenmesi gerekiyor; müzik panelinden 'İzni yenile'"`;
    - `NotConnected` → `"hata: Spotify bağlı değil"`;
    - `NoDevice` → `"hata: Spotify açılamadı"`;
    - `PremiumRequired` → `"hata: Spotify Premium gerekiyor"`.
- [ ] **Step 4: Canlı doğrula (kullanıcıyla)**
  - Kullanıcı müzik panelinden "İzni yenile" ile bir kez yeniden bağlanır.
  - `--ada-islev-sina spotify "Tarkan"` → Expected: Spotify'da Tarkan çalar, sonuç `tamam`.
  - Önceki çalma durumu geri konur.
- [ ] **Step 5: Commit** — `feat: Spotify'da şarkı çalma (Ada)`

---

### Task 8: Oturum yöneticisi `AdaAssistant` ve App bağlantısı

**Files:**
- Create: `src/CamAda/Assistant/AdaAssistant.cs`, `src/CamAda/Assistant/AdaState.cs`, `src/CamAda/Assistant/AudioDucker.cs`, `src/CamAda/Assistant/UsageFile.cs`
- Modify: `src/CamAda/App.xaml.cs` (`StartAssistant()`, kısayol, tam ekran, çıkış).

**Interfaces:**
- **Durum:**
  - `abstract record AdaState` ve alt türleri: `Idle`, `Connecting`, `Listening(string? UserText)`, `Speaking(string AdaText)`, `Failed(AssistantError Error, string Message)`.
  - `Failed` 4 sn gösterilip `Idle`'a döner.
- **Yönetici:** `sealed class AdaAssistant : IDisposable`
  - kurucu: `AdaAssistant(Dispatcher dispatcher, AdaDependencies deps)`;
  - `event EventHandler<AdaState>? StateChanged` (UI iş parçacığında);
  - `bool IsSessionActive`;
  - `void Start()`: etkinse ve anahtar varsa uyandırmayı açar;
  - `void Stop()`;
  - `void Trigger()`: oturum yoksa açar, varsa kapatır;
  - `void SetFullScreen(bool fullScreen)`.
- **Bağımlılıklar:** `sealed record AdaDependencies`
  - `Func<AssistantSettings> Settings`, `GeminiKeyStore Keys`, `AdaToolContext Tools`, `MemoryFile Memory`, `UsageFile Usage`;
  - `string ModelDir`, `Action<Exception> Log`, `AudioService? Audio` (kısma ve cihaz olayları için).
- **Sesleri kısma:** `sealed class AudioDucker(AudioService audio)`: `void Duck(double factor)`, `void Restore()`. Kendi süreci hariç uygulama oturumlarının sesi kaydedilip çarpılır; geri yüklenir.
- **Kullanım dosyası:** `sealed class UsageFile(string directory)`: `UsageMonth? Current`, `void Add(DateOnly today, decimal usd, int sessions, int searches)`.

- [ ] **Step 1: Oturum akışını yaz** (tasarım "Uyanma ve oturum")
  - **Tetiklenme:**
    1. Durum `Connecting`; `PlayCue(Wake)` çalınır.
    2. Uyandırma durdurulur, ön kayıt alınır.
    3. `EchoCancelledMic` başlatılır; başarısızsa `PlainMic` ve yarı çift yönlü.
    4. `ConnectAsync`; talimat `AdaInstruction.Build(now, Memory.Book.Items)`.
    5. Bağlantı hazır olana kadar gelen ses arabellekte tutulur, sonra ön kayıtla birlikte sırayla gönderilir.
    6. Durum `Listening`.
  - **Olaylar:**

    | Olay | Yapılan |
    |---|---|
    | `InputText` | `Listening(metin)`, `Rules.UserSpoke()` |
    | `AudioChunk` | hoparlöre eklenir, `Speaking`, `Rules.AdaSpeaking()` |
    | `OutputText` | `Speaking(metin)` |
    | `Interrupted` | `Speaker.Clear()` |
    | `TurnComplete` | hoparlör bitince `Rules.AdaFinished()` |
    | `ToolCalls` | `Rules.ToolRunning(true)` → her çağrı `RunAsync` → `SendToolResultsAsync` → `ToolRunning(false)` |
    | `Usage` | `CostTable.Live` ile toplanır |
    | `GoAway` | oturum kapanır |
    | `Closed(hata)` | `Failed` ve kapanış |
  - **Yerel söz kesme:** yankı engelliyken, hoparlör çalarken temiz mikrofon RMS > 0,04 art arda ~150 ms olursa `Speaker.Clear()` ve o yanıtın kalan sesi atılır (yeni `TurnComplete` ya da `Interrupted`'a kadar).
  - **Yarı çift yönlü yedek:** hoparlör çalarken ve sonraki 400 ms sıfır gönderilir.
  - **Kural denetimi:** oturum boyunca 250 ms'lik `DispatcherTimer` ile `Rules.Check()`; boştayken zamanlayıcı yok.
  - **Kapanış (her yoldan aynı):**
    1. Mikrofon durdurulur.
    2. `CloseAsync`.
    3. Hoparlör bittikten sonra kısma geri alınır.
    4. Kullanım dosyasına yazılır.
    5. Uyandırma yeniden başlatılır (tam ekranda ve `ListenInFullScreen=false` ise başlatılmaz).
    6. Durum `Idle`.
    - `konusmayi_bitir` ve mikrofon kapatma, Ada'nın son sesi bittikten sonra kapatır.
  - **Kısma:** `DuckOthers` açıksa oturum başında %30'a.
- [ ] **Step 2: Çıkış cihazı değişimini işle** (Review Focus 2)
  - `AudioService.DevicesChanged`'da varsayılan çıkış değiştiyse oturum sürerken `AdaSpeaker` yeni cihazla yeniden kurulur.
  - Yankı engelleyici yeni dizinlerle yeniden başlatılır; başarısızsa yarı çift yönlüye düşülür ve bir kez bilgilendirilir.
- [ ] **Step 3: App bağlantısı**
  - **`StartAssistant()`:** `StartQuickSettingsAsync`'ten sonra çağrılır.
    - Eski düz anahtar içeri alınır.
    - `AdaToolContext` kurulur. `OpenWindows` ManagedShell `Tasks.GroupedWindows`'tan alınır; uygulama adı olarak `ApplicationWindow` başlığı değil süreç ya da exe görünen adı kullanılır.
  - **Kısayol:** `Hotkey` → `Trigger()`; alınamazsa adada bir kez "Win+Alt+A kullanılamıyor".
  - **Tam ekran ve güç:**
    - `_strip.FullScreenChanged` → `SetFullScreen`.
    - Oturum kilitlenince ya da bilgisayar uykuya girince oturum kapanır, uyandırma durur.
    - Uykudan dönünce (`PowerModes.Resume`) uyandırma yeniden kurulur.
  - **Çıkış:** `ExitGracefully` içinde `Dispose`.
- [ ] **Step 4: Canlı doğrula (kullanıcıyla; her deneme oturumu kapatarak biter)**
  - **Temel akış:** "Hey Ada, sesi yüzde otuza getir" tek nefeste söylenir. Beklenen: ses %30 olur; Ada onaylar; 8 sn sonra oturum kapanır; `Get-NetTCPConnection -OwningProcess <CamAda>` ile Google'a bağlantı kalmadığı doğrulanır; ses geri konur.
  - **Söz kesme:** "Hey Ada, Napolyon'u anlat" ve araya girilir ("dur"). Beklenen: Ada hemen susar.
  - **Yanlış tetiklenme:** "Hey Ada" denip susulur. Beklenen: 6 sn sonra sessizce kapanır.
  - **Kısayol ve düğme** (Review Focus 4): Win+Alt+A → oturum; yeniden Win+Alt+A → kapanır; çift basış → tek oturum.
  - **Bağlantı kopması** (Review Focus 1): oturumdayken Debug'da `CAMADA_ADA_FAIL_AFTER=3` ortam değişkeniyle istemci 3 sn sonra kapatılır. Beklenen: "Bağlantı koptu", mikrofon kapalı, uyandırma yeniden dinliyor.
  - **Cihaz değişimi** (Review Focus 2): oturumdayken "kulaklığa geç". Beklenen: Ada'nın sesi Buds2'den sürer (Buds2 bağlı değilse adım atlanır ve not edilir). Önceki çıkış cihazı geri konur.
  - **Tam ekran:** YouTube tam ekranda "Hey Ada" → tetiklenmez; Win+Alt+A → oturum açılır.
- [ ] **Step 5: Commit** — `feat: Ada oturum yöneticisi ve uygulama bağlantısı`

---

### Task 9: Ada'nın adadaki görünümü ve ayarlar paneli

**Files:**
- Create: `src/CamAda/Panels/AdaPanel.xaml`, `src/CamAda/Panels/AdaPanel.xaml.cs`
- Modify:
  - `src/CamAda/Island/IslandBar.xaml(.cs)`: `AssistantButton`, `AssistantClicked`;
  - `src/CamAda/Island/IslandWindow.xaml(.cs)`: Ada satırı, gizlilik noktası, menü öğeleri;
  - `src/CamAda/Island/IslandCommands.cs`: üç yeni üye;
  - `src/CamAda.Core/Island/IslandLayout.cs`: `SystemSectionWidth` 144 → 180;
  - `tests/CamAda.Core.Tests/Apps/DockOverflowTests.cs`: genişlik verileri;
  - `src/CamAda/App.xaml.cs`.

**Interfaces:**
- **Ada düğmesi:** `IslandBar.AssistantButton` (36×34, glif `` mikrofon) `^` düğmesinin solunda; `event EventHandler? AssistantClicked`.
  - Asistan kurulu değilse `AdaPanel` açılır, kuruluysa `Trigger()`.
  - Oturumda glif vurgu renginde.
- **Ada satırı:** `IslandWindow.ShowAda(AdaState state)`
  - Ekran görüntüsü önizlemesindeki genişleme düzeniyle: hap büyür, solda glif, ortada tek satır metin.
  - `Listening`: kırmızımsı nokta + "Dinliyorum…" ya da `"<kullanıcının sözü>"`; nokta hafifçe nabız atar (`AnimationTicker`, 30 kare/sn).
  - `Speaking`: ✦ + Ada'nın metni (üç noktayla kısaltılmış).
  - `Failed`: ⚠ + mesaj.
  - `Idle`: gizli.
  - Tam ekranda gösterilmez. Ada satırı açıkken ada fareyle genişlemez.
  - Oturum boyunca küçük adada da kırmızımsı nokta (`CompactAdaDot`) görünür.
- **Menü:** "Sesli asistan" (işaretlenebilir) ve "Ada ayarları…".
- **Ada komutları:** `IslandCommands`'a `Func<bool> IsAssistantEnabled`, `Action<bool> SetAssistantEnabled`, `Action OpenAssistantPanel`.
- **Panel:** `AdaPanel(AdaPanelServices s)`, panel anahtarı `"ada"`, genişlik 360.
  - Anahtar yokken:
    - açıklama;
    - AI Studio bağlantısı (`https://aistudio.google.com/api-keys`);
    - gizli anahtar kutusu;
    - **Kaydet**: Live kurulum denetimi yapar ve sonucu yazar ("Anahtar doğrulandı" ya da hata metni).
  - Anahtar varken:
    - "Sesli asistan açık" anahtarı;
    - mikrofon seçici (etkin kayıt cihazları; seçim ayara yazılır);
    - "Bu ay tahmini: 0,42 $ · 37 oturum · 12 arama";
    - "Anahtarı sil";
    - **Hafıza** listesi: her madde "konu: bilgi" ve ×; "Hafızayı temizle" iki adımlı.

- [ ] **Step 1: Genişlik testini güncelle**
  - Yeni değerler: (0,1100), (5,1100), (6,1138), (8,1214), (9,1250), (30,1250).
  - Açıklama satırına "Plan 5: Ada düğmesi 36 DIP (1100)" eklenir.
  - Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~IslandLayout"` → önce FAIL.
- [ ] **Step 2: `SystemSectionWidth = 180`; test geçmeli**
- [ ] **Step 3: Düğme, Ada satırı, gizlilik noktası, menü ve paneli yaz; App'te bağla**
  - Mevcut `Panels.xaml` stilleri kullanılır.
  - Metinler tasarımın hata tablosundan.
- [ ] **Step 4: Canlı doğrula**
  - **Menü:** Debug derlemesinde menüden "Ada ayarları…" → panel açılır (UIA ile bulunur, gerçek tıkla).
  - **Anahtar:** Yanlış anahtar ("x") → "Gemini anahtarı geçersiz". Doğru anahtar (mevcut depodan) → "Anahtar doğrulandı".
  - **Mikrofon seçimi:** "Mikrofon Dizisi (Realtek)" seçilir → `settings.json`'da `Assistant.Microphone` yazılır.
  - **Ada düğmesi:** basınca oturum açılır, adada "Dinliyorum…" ve kırmızı nokta; yeniden basınca kapanır.
  - **Hafıza:** kullanıcı "Hey Ada, ben Ankara'da yaşıyorum" der → Ada "aklımda tutuyorum" der, panelde "şehir: Ankara" görünür. × ile silinir.
- [ ] **Step 5: Commit** — `feat: Ada'nın adadaki görünümü ve ayarlar paneli`

---

### Task 10: Uçtan uca deneme, ölçümler, belgeler

**Files:**
- Modify: `docs/superpowers/specs/2026-11-23-cam-ada-design.md` (tasarımın "Ana tasarımda değişen" maddeleri).

- [ ] **Step 1: Kur**
  - `tools\install.ps1` çalıştırılır.
  - Kurulum klasöründe `Assets\WakeWord\` dosyaları ve sherpa-onnx yerel kütüphaneleri var.
- [ ] **Step 2: Boşta ölçüm** (asistan açık, oturum yok, 2 dk)
  - CamAda işlemci kullanımı, asistan kapalıyken ölçülene göre en çok +%5 tek çekirdek.
  - Bellek en çok +60 MB.
  - Google'a açık TCP bağlantısı yok.
- [ ] **Step 3: Kullanıcıyla uçtan uca liste** (her madde oturumu kapatarak biter; durumlar geri konur)
  1. "Hey Ada, YouTube'u aç."
  2. "Hey Ada, Discord'u aç." / "Discord'u kapat."
  3. "Hey Ada, sesi yüzde kırka getir." / "Parlaklığı biraz azalt." / "Kulaklığa geç."
  4. "Hey Ada, dolar kaç TL?" / "Gram altın ne kadar?"
  5. "Hey Ada, Tarkan çal." / "Durdur."
  6. "Hey Ada, Ahmet'e WhatsApp'tan 'geliyorum' yaz." Ayara kullanıcının verdiği bir kişi önceden yazılır; gönderme kullanıcıda.
  7. "Hey Ada, Napolyon'u anlat." Araya girilir.
  8. "Hey Ada, bilgisayarı kapat." "Emin misin?" sorusuna "hayır" denir; kapanmamalı.
  9. "Hey Ada, benim hakkımda ne biliyorsun?" / "Şehrimi unut."
  10. "Hey Ada" deyip susmak → sessiz kapanış.
- [ ] **Step 4: Ana tasarım belgesini güncelle**
  §3, §7.1/§7.5 ve §8'e tasarımın "Ana tasarımda değişen" maddeleri işlenir.
- [ ] **Step 5: Tüm testler, uyarısız derleme, commit**
  - Run: `dotnet test tests/CamAda.Core.Tests` ve `dotnet build src/CamAda -c Release` → PASS, 0 uyarı.
  - Commit — `docs: ana tasarıma sesli asistan`

## Kullanıcının elle denemesi (son)

Görev 10 Step 3 listesi. Ayrıca bir hafta günlük kullanım: yanlış tetiklenme sıklığı, aylık tahmini harcama (panel) ve AI Studio'daki gerçek harcama karşılaştırması.
