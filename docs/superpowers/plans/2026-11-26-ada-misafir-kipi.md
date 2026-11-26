# Ada Misafir Kipi Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ada kullanıcının sesini tanır. Başkasının sesi Google'a gitmez. Başka bir ses duyulunca (ya da kullanıcı isteyince)
misafir kipine geçer ve yalnız "Ada" ile başlayan sözleri dinler.

**Architecture:** Mikrofonla Google arasına bir süzgeç girer. Kararları Core'daki `VoiceGate` verir (birim testli, sahte
çözümleyiciyle). Uygulamadaki `SherpaVoiceAnalyzer` konuşmayı algılar (Silero VAD), "Ada" sözcüğünü yakalar (uyandırma modeli)
ve sesi ses iziyle karşılaştırır (konuşmacı modeli). `VoiceFilter` bunları kendi iş parçacığında yürütür. `GuestMode` kipin
açık olup olmadığını tutar. Ses izi ayarlar panelinde tanıtılır ve DPAPI ile saklanır.

**Tech Stack:** .NET 10, WPF, sherpa-onnx 2.0.3 (`VoiceActivityDetector`, `SpeakerEmbeddingExtractor`, `KeywordSpotter`),
NAudio, xUnit + `FakeTimeProvider`.

**Spec:** `docs/superpowers/specs/2026-11-26-ada-misafir-kipi-design.md`

## Global Constraints

- Mikrofonu Google'a açan otomatik deneme yok. Gerçek mikrofonla deneme yalnız kullanıcı başlatır ve yerelde kalır. Oturum
  denemeleri yalnız sahte odayla (`CAMADA_ADA_FAKE_ROOM`, Windows TTS sesi).
- Tanıtmada ses kaydı saklanmaz; yalnız ses izi: `%LOCALAPPDATA%\CamAda\ada-ses-izi.bin`, DPAPI `CurrentUser`.
- Ölçüm kayıtları yalnız `%LOCALAPPDATA%\CamAda\deneme\` altında. Depoya girmez. Görev 4'ün sonunda silinir.
- Model dosyaları `src/CamAda/Assets/Speaker/` altında, lisansları yeniden dağıtıma izin verir, lisans metni yanında
  (`LICENSE-models.txt`). Csproj: `<None Include="Assets\Speaker\*" CopyToOutputDirectory="PreserveNewest" />`.
- Çözümleyici yalnız oturumda ya da tanıtırken ses işler. Boştayken yalnız bugünkü "Hey Ada" dinleyicisi çalışır.
- Çözümleyici yüklenemezse (model eksik) Ada bugünkü gibi çalışır, süzgeç yok. `misafir_kipi` hata döner.
- Kendiliğinden açılan kip: son başka sesten **2 dk** sonra biter (`GuestModeMinutes`, varsayılan 2, aralık 1–30).
- Kip açılış tonu (`AdaCue.Guest`): 784 Hz 90 ms, 40 ms sessizlik, 784 Hz 90 ms. Uyanma (660→880 Hz) ve hatadan (440 Hz) farklı.
  Yalnız kendiliğinden açılışta, açılış başına bir kez çalar.
- Adada kip açıkken dinleme satırı tam olarak: `“Ada” deyin, dinliyorum` (normalde `Dinliyorum…`).
- İşlev `misafir_kipi`, parametre `acik` (BOOLEAN). Sonuçlar Görev 5'te tam metinle.
- Yeni kod yorumları ve metinler Türkçe (projenin geleneği).
- Testler: `dotnet test tests/CamAda.Core.Tests`. Derleme: `dotnet build src/CamAda -c Release` 0 uyarı.
- Debug denemeleri: `dotnet build src/CamAda -c Debug`, sonra `src\CamAda\bin\Debug\net10.0-windows10.0.22621.0\CamAda.exe --<deneme>`.
  Sonuç `%LOCALAPPDATA%\CamAda\ada-deneme.log`'a yazılır.

### Ölçümle belirlenen değerler (Görev 1 doldurur)

| Değer | Başlangıç | Ölçüm sonucu |
|---|---|---|
| Konuşmacı modeli (`VoiceModels.SpeakerModel`) | CAM++ `3dspeaker_speech_campplus_sv_zh_en_16k-common_advanced.onnx` | |
| `SpeakerThreshold` (ayar, aralık 0.10–0.90) | 0.50 | |
| `VoiceGateOptions.DecideAfterSamples` | 12800 (0,8 sn) | |
| `VoiceGateOptions.MinSpeechRms` | 0.02 | |
| "Ada" eşiği (`NameThreshold`) ve güçlendirmesi | 0.25 / 1.2 | |

Sonraki görevler bu sütundaki değerleri kullanır. Ölçüm boş bıraktıysa başlangıç değeri geçerli.

## Review Focus

1. **Ada'nın kendi sesi misafir sanılmamalı.** Yankı engelleyicide kalan yankı (RMS ≤ 0,006) ve yarı çift yönlü yolda Ada'nın
   tam sesi. Beklenen: kip açılmaz. Görev 3 `Quiet_speech_is_ignored`. Görev 1 `--yanki` kaydı yankının düzeyini ve puanını
   ölçer. Görev 6: yarı çift yönlüde susturma süzgeçten önce. Görev 9 canlı denemesi 2. madde: yalnızken uzun yanıtta ton yok.
   Sahte odada yankı olmadığı için bu orada denenemez.
2. **Yalnızken yanlış ret kullanıcıyı kilitlemez.** Ses izine uymayan ama "Ada" ile başlayan söz geçer, kısa söz kip kapalıyken
   denetlenmeden geçer. Görev 3 `Name_from_unknown_voice_passes_and_reports_guest`,
   `Short_segment_passes_unverified_when_guest_mode_is_off`.
3. **Model dosyası eksik ya da bozuk.** sherpa-onnx eksik dosyayla süreci düşürebilir (Plan 5 dersi). Beklenen: dosyalar önceden
   denetlenir, Ada bugünkü gibi çalışır. Görev 4 `--ada-suzgec-sina --bos-klasor` null döndüğünü yazar.
4. **Ses izi başka modelle alınmış.** Model değişince eski iz kullanılmaz, panel "yeniden tanıt" der. Görev 1
   `Other_model_or_broken_json_is_null`.
5. **Oturum kapanırken süzgeçte bekleyen çıktı.** Kapanan oturumun geç gelen parçaları Google'a gitmez, kipi açmaz. Görev 6:
   `OnFiltered` oturum karşılaştırması. Oda denemesinde kapanıştan sonra `misafir sesi` izi olmamalı.

---

### Task 1: Ölçüm — modeller, ses izi puanı ve kayıt/ölçüm denemesi

**Files:**
- Create: `src/CamAda.Core/Assistant/Voiceprint.cs`
- Create: `src/CamAda.Core/Assistant/VoiceEnrollmentText.cs`
- Test: `tests/CamAda.Core.Tests/Assistant/VoiceprintTests.cs`
- Create: `src/CamAda/Assistant/Audio/VoiceModels.cs`
- Create: `src/CamAda/Assistant/Audio/SpeakerEmbedder.cs`
- Create: `src/CamAda/Assistant/AdaProbes.Speaker.cs` (`#if DEBUG`)
- Modify: `src/CamAda/Assistant/AdaProbes.cs` (`--ada-konusmaci-sina` kaydı)
- Modify: `src/CamAda/CamAda.csproj` (Assets\Speaker)
- Add: `src/CamAda/Assets/Speaker/<seçilen model>.onnx`, `silero_vad.onnx`, `LICENSE-models.txt`

**Interfaces:**
- Produces (Core):
  - `public sealed class Voiceprint(string model, IReadOnlyList<float[]> samples)` with `string Model`,
    `IReadOnlyList<float[]> Samples`, `double Score(ReadOnlySpan<float> embedding)` (normalize edilmiş örneklerin ortalamasıyla
    kosinüs, −1..1; sıfır vektör 0), `string ToJson()`, `static Voiceprint? FromJson(string json, string expectedModel)`
    (bozuk, boş, boyutları farklı ya da model farklıysa null).
  - `public static class VoiceEnrollmentText { public static IReadOnlyList<string> Sentences { get; } }`, tam olarak:
    1. "Bugün hava güzel, biraz yürüyüşe çıkabilirim."
    2. "Ada, sesi biraz kıs ve müziği duraklat."
    3. "Akşam yemeğinden sonra kitabımı okumak istiyorum."
    4. "Yarın sabah erkenden kalkıp işe gideceğim."
    5. "Bilgisayarın parlaklığını yüzde elliye ayarla."
- Produces (App):
  - `internal static class VoiceModels` with `const string SpeakerModel`, `const string Vad = "silero_vad.onnx"`,
    `static string Dir` (= `AppContext.BaseDirectory\Assets\Speaker`), `static bool Present(string dir)`.
  - `internal sealed class SpeakerEmbedder(string modelPath) : IDisposable` with `float[] Compute(ReadOnlySpan<short> audio16k)`
    (kendi kilidiyle, her çağrıda yeni akış, `InputFinished` sonra `Compute`), `int Dim`.

- [ ] **Step 1: Write the failing tests** (`VoiceprintTests`)

```csharp
[Fact] public void Same_direction_scores_one() =>
    Assert.Equal(1.0, new Voiceprint("m", [[1, 0, 0], [2, 0, 0]]).Score([3, 0, 0]), 6);
[Fact] public void Orthogonal_scores_zero() =>
    Assert.Equal(0.0, new Voiceprint("m", [[1, 0, 0]]).Score([0, 1, 0]), 6);
[Fact] public void Mean_of_normalized_samples_is_used() =>
    Assert.Equal(1.0, new Voiceprint("m", [[10, 0], [0, 1]]).Score([1, 1]), 6);
[Fact] public void Zero_embedding_scores_zero() =>
    Assert.Equal(0.0, new Voiceprint("m", [[1, 0]]).Score([0, 0]));
[Fact] public void Json_round_trips()
// FromJson(v.ToJson(), "m") → Model "m", Samples aynı (eleman eleman).
[Fact] public void Other_model_or_broken_json_is_null()
// FromJson(json, "baska") null; FromJson("{", "m") null; örneksiz iz null; boyutları farklı örnekler null.
```

`VoiceEnrollmentText.Sentences` için: 5 cümle, hepsi noktayla biter, ikincisi "Ada," ile başlar.

- [ ] **Step 2: Run** `dotnet test tests/CamAda.Core.Tests --filter VoiceprintTests` → FAIL (tür yok).
- [ ] **Step 3: Implement** `Voiceprint` ve `VoiceEnrollmentText`. JSON: `{"model":"…","samples":[[…],…]}` (System.Text.Json).
- [ ] **Step 4: Run** aynı komut → PASS.
- [ ] **Step 5: Download candidate models** into `src/CamAda/Assets/Speaker/` (`curl -L -o`), base
  `https://github.com/k2-fsa/sherpa-onnx/releases/download/`:
  - `speaker-recongition-models/3dspeaker_speech_campplus_sv_zh_en_16k-common_advanced.onnx` (28 281 165 bayt)
  - `speaker-recongition-models/wespeaker_en_voxceleb_resnet34_LM.onnx` (26 530 550 bayt)
  - `speaker-recongition-models/3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx` (39 593 762 bayt)
  - `asr-models/silero_vad.onnx` (643 854 bayt)

  Boyutlar tutmalı. Adaylar ölçüm bitene kadar commit edilmez.
- [ ] **Step 6: Implement** `VoiceModels`, `SpeakerEmbedder`, csproj satırı ve `--ada-konusmaci-sina` denemesi:
  - `--kaydet=<ad> [--sure=sn]` (varsayılan 40 sn): seçili mikrofonu oturumdaki gibi açar
    (`MicDevices.ResolveMic` → `DspIndexes` → `MicFactory.EchoCancelled`, olmazsa `Plain`; MTA'da), 16 kHz mono WAV olarak
    `%LOCALAPPDATA%\CamAda\deneme\<ad>.wav`'a yazar. Başta 3 sn geri sayım günlüğe ve konsola. `<ad>` = `tanitma` ise
    `VoiceEnrollmentText.Sentences`'ı konsola yazar. Google'a bağlanmaz.
  - `--yanki`: Windows TTS ile ~10 sn İngilizce konuşmayı varsayılan hoparlörden çalarken aynı anda yankı engelleyiciyle kaydeder
    (`deneme\yanki.wav`). Odada kimse konuşmaz.
  - `--olc`: `Assets\Speaker`'daki her konuşmacı modeli için:
    - `tanitma.wav`'ın VAD parçalarından (≥1 sn) ses izi kurar.
    - `kullanici.wav`, `misafir.wav`, `yanki.wav`'ın her VAD parçasını iki türlü puanlar: ilk 0,8 sn ve 1 sn'lik kayan pencereler.
    - 0.30–0.70 arası 0.05 adımlı her eşikte şunları yazar: kullanıcının geçme %'si, misafirin durma %'si, yankı parçalarının
      RMS'i ve puanı.
    - Gömme hesaplama süresini (ms, ortalama ve en çok) yazar.
    - "Ada" algılayıcıyı (`▁A D A`, eşik 0.20/0.25/0.30) `ada.wav` (10 cümle), `kullanici.wav` ve `misafir.wav` üstünde
      çalıştırır: yakalanan sayısı ve dakika başına yanlış tetiklenme.
- [ ] **Step 7: Build and dry-run** `dotnet build src/CamAda -c Debug`, sonra
  `CamAda.exe --ada-konusmaci-sina --olc`. Kayıt yokken "kayıt yok: tanitma.wav" yazıp çıkmalı.
- [ ] **Step 8: USER STEP — kayıtlar.** Dur ve kullanıcıdan şunları iste (odada bir kişi daha olmalı; her biri yerel kalır):
  `--kaydet=tanitma` (5 cümleyi okur), `--kaydet=kullanici` ("Ada" demeden doğal konuşma ve komutlar),
  `--kaydet=misafir` (öbür kişi konuşur), `--kaydet=ada` ("Ada, …" ile başlayan 10 cümle), `--yanki`.
  Kullanıcı her birini `! & "src\CamAda\bin\Debug\net10.0-windows10.0.22621.0\CamAda.exe" --ada-konusmaci-sina --kaydet=<ad>`
  ile çalıştırır.
- [ ] **Step 9: Measure** `--olc`. Sonuçları bu plandaki "Ölçümle belirlenen değerler" tablosuna yaz.
  **Sürdürme koşulu:** bir modelde, bir eşikte 0,8 sn penceresiyle kullanıcı ≥%90 geçmeli, misafir ≥%90 durmalı; gömme ≤100 ms
  sürmeli; yankı parçaları ya `MinSpeechRms` altında kalmalı ya da kullanıcı sayılmalı. "Ada" en az 9/10 yakalanmalı.
  **Tutmazsa DUR** ve sonuçları kullanıcıya götür (ör. B yolu: yalnız adla seslenme).
- [ ] **Step 10: Keep only the chosen model.** Ötekileri sil, `VoiceModels.SpeakerModel`'i ayarla, `LICENSE-models.txt`'e modelin
  ve Silero VAD'ın lisansını ve kaynağını yaz.
- [ ] **Step 11: Commit**

```bash
git add src/CamAda.Core/Assistant/Voiceprint.cs src/CamAda.Core/Assistant/VoiceEnrollmentText.cs tests/CamAda.Core.Tests/Assistant/VoiceprintTests.cs src/CamAda/Assistant/Audio/VoiceModels.cs src/CamAda/Assistant/Audio/SpeakerEmbedder.cs src/CamAda/Assistant/AdaProbes.Speaker.cs src/CamAda/Assistant/AdaProbes.cs src/CamAda/CamAda.csproj src/CamAda/Assets/Speaker docs/superpowers/plans/2026-11-26-ada-misafir-kipi.md
git commit -m "feat: ses izi puanı, konuşmacı modeli ve ölçüm denemesi (misafir kipi)"
```

---

### Task 2: Misafir kipi durumu ve küçük Core eklemeleri

**Files:**
- Create: `src/CamAda.Core/Assistant/GuestMode.cs`
- Modify: `src/CamAda.Core/Settings/AssistantSettings.cs`, `src/CamAda.Core/Settings/SettingsStore.cs` (`SanitizeAssistant`)
- Modify: `src/CamAda.Core/Assistant/AdaText.cs`, `src/CamAda.Core/Assistant/UserConfirmGate.cs`, `src/CamAda.Core/Assistant/WakeWord.cs`
- Test: `tests/CamAda.Core.Tests/Assistant/GuestModeTests.cs`, `AdaTextTests.cs`, `UserConfirmGateTests.cs`, `WakeWordTests.cs`,
  `tests/CamAda.Core.Tests/Settings/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class GuestMode(TimeProvider time, TimeSpan autoDuration)`:
    - `bool IsOn`: elle açıksa ya da son başka sesten bu yana `autoDuration` geçmediyse.
    - `bool IsManual`.
    - `bool OtherVoiceHeard()`: son başka ses zamanını yeniler. Kip kapalıyken açtıysa true (ton bir kez çalsın diye).
    - `bool SetManual(bool on)`: `IsOn` değiştiyse true. `false` kendiliğinden açılmış kipi de kapatır.
  - `AssistantSettings`: `int GuestModeMinutes` (varsayılan `DefaultGuestModeMinutes = 2`, aralık 1–30),
    `double SpeakerThreshold` (varsayılan `DefaultSpeakerThreshold` = Görev 1 sonucu, aralık 0.10–0.90).
  - `AdaText.EndsWithQuestion(string text)`: sondaki boşluk ve tırnaklar atıldıktan sonra `?` ile bitiyor mu.
  - `AdaText.ListeningLine(bool guest)`: `“Ada” deyin, dinliyorum` ya da `Dinliyorum…`.
  - `UserConfirmGate.Armed`: soru sorulmuş ve süre dolmamış.
  - `WakeWord.NameTokens = "▁A D A"`, `WakeWord.NameLabel = "ADA"`,
    `WakeWord.NameKeywordLine(double threshold, double boost)` → `"▁A D A :{boost} #{threshold} @ADA"` (InvariantCulture).

- [ ] **Step 1: Write the failing tests**

```csharp
// GuestModeTests (FakeTimeProvider, autoDuration 2 dk)
[Fact] public void Other_voice_turns_on_once()        // 1. çağrı true, 2. false, IsOn true, IsManual false
[Fact] public void Auto_ends_two_minutes_after_last_other() // 0 sn ve 60 sn'de duyuldu: 179,9 sn açık, 180,1 sn kapalı
[Fact] public void Manual_on_has_no_expiry()          // SetManual(true) true; 1 saat sonra açık; IsManual
[Fact] public void Manual_off_clears_auto_and_auto_can_return() // duyuldu; SetManual(false) true, kapalı; OtherVoiceHeard() true
[Fact] public void Manual_on_while_auto_on_keeps_it_on() // duyuldu; SetManual(true) false; 10 dk sonra açık
[Fact] public void Same_manual_state_reports_no_change() // kapalıyken SetManual(false) false

// AdaTextTests
[Theory, InlineData("Emin misin?", true), InlineData("Emin misin? ", true), InlineData("“Kapatayım mı?”", true),
 InlineData("Sesi kıstım.", false), InlineData("", false), InlineData("Kıstım. Başka bir şey?", true)]
public void Ends_with_question(string text, bool expected)
[Fact] public void Listening_line_mentions_name_in_guest_mode() // ListeningLine(true) == "“Ada” deyin, dinliyorum"; false → "Dinliyorum…"

// UserConfirmGateTests
[Fact] public void Armed_after_ask_until_window_or_reset() // Press → Armed; 31 sn → değil; Press, Reset → değil

// WakeWordTests
[Fact] public void Name_line_has_tokens_boost_threshold_label() =>
    Assert.Equal("▁A D A :1.2 #0.25 @ADA", WakeWord.NameKeywordLine(0.25, 1.2));

// SettingsStoreTests: Assistant_has_spec_defaults'a GuestModeMinutes 2 ve SpeakerThreshold eklenir;
// aralık dışı (0, 31; 0.05, 0.95) varsayılana döner.
```

- [ ] **Step 2: Run** `dotnet test tests/CamAda.Core.Tests` → yeni testler FAIL.
- [ ] **Step 3: Implement** yukarıdaki üyeleri.
- [ ] **Step 4: Run** → tümü PASS.
- [ ] **Step 5: Commit** `feat: misafir kipi durumu ve yardımcılar (Core)`.

---

### Task 3: Ses süzgecinin kararları (Core `VoiceGate`)

**Files:**
- Create: `src/CamAda.Core/Assistant/VoiceGate.cs`
- Test: `tests/CamAda.Core.Tests/Assistant/VoiceGateTests.cs`

**Interfaces:**
- Consumes: yok (Görev 2'den bağımsız).
- Produces:

```csharp
public enum SpeakerVerdict { User, Other }

/// Akış hâlinde çağrılır: her parça sırayla bir kez IsSpeech ve HeardName'e verilir.
public interface IVoiceAnalyzer
{
    bool IsSpeech(short[] chunk);              // konuşma algılayıcı (VAD) şu an konuşma görüyor mu
    bool HeardName(short[] chunk);             // "Ada" bu parçada bitti mi
    SpeakerVerdict Verify(short[] segment);    // ses izine göre karar; ses izi yoksa User
}

public sealed record VoiceGateOptions(
    int ChunkSamples = 1600, int LookbackSamples = 8000, int DecideAfterSamples = 12800,
    int RecheckEverySamples = 16000, int NameWindowSamples = 24000, int MinVerifySamples = 8000, double MinSpeechRms = 0.02);

public readonly record struct GateInput(bool GuestMode, bool NoteActive);

public sealed record GateOutput(IReadOnlyList<short[]> Send, bool OtherVoiceHeard);

public sealed class VoiceGate(IVoiceAnalyzer analyzer, VoiceGateOptions options)
{
    public GateOutput Push(short[] samples, GateInput input);  // her uzunluk; ChunkSamples'lık parçalara bölünür, artan taşınır
    public void AllowNextWithoutName(TimeSpan within);         // bundan sonra `within` içinde başlayan ilk söz adsız geçer
}
```

Saat, işlenen örnek sayısıdır (16 kHz). Duvar saati kullanılmaz.

**Karar düzeni** (imzadan çıkmadığı için yazılı):

- **Boşta:** Konuşma yoksa parça son `LookbackSamples`'lık geriye bakışa eklenir ve aynı uzunlukta sıfır gider.
  Konuşma başlarsa parça başlar. Bekletilen ses = geriye bakış + parça. Parça başı = saat − geriye bakış uzunluğu.
  `AllowNextWithoutName` hakkı süresindeyse bu parça için kullanılır ve biter (`free`).
- **Bekletme:** Gelen parçalar bekletilir, `Send` boş kalır. Karar şu anlardan birinde verilir:
  - konuşma `DecideAfterSamples`'a ulaşınca,
  - parça bitince (ilk konuşmasız parça),
  - kipte ad bekleniyorsa en geç `NameWindowSamples`'ta.
- **Karar:**
  1. Konuşma kısmının RMS'i `MinSpeechRms` altındaysa: sıfır, `Verify` yok, rapor yok.
  2. `needName = input.GuestMode && !free && !input.NoteActive`.
     `nameOk` = "Ada" parça başı ile parça başı + `NameWindowSamples` arasında bittiyse.
     `needName && !nameOk` iken pencere dolmadı ve parça sürüyorsa beklemeye devam edilir.
  3. `needName && !nameOk` ise engellenir. Konuşma ≥ `MinVerifySamples` ise yine `Verify` çağrılır. Sonuç `Other` ve konuşma
     ≥ `DecideAfterSamples` ise `OtherVoiceHeard`.
  4. Konuşma < `MinVerifySamples` ise denetlenmeden geçer.
  5. Değilse `Verify`:
     - `User` ise geçer (ilk karar = User).
     - `Other` ve `nameOk` ise geçer (güvenlik payı, ilk karar = Other), `OtherVoiceHeard`.
     - `Other` ve ad yoksa engellenir. Konuşma ≥ `DecideAfterSamples` ise `OtherVoiceHeard`.
- **Geçme:** Bekletilen tüm parçalar (geriye bakış dahil) olduğu gibi tek çıktıda gider, sonrakiler hemen gider.
  İlk karar User ise her `RecheckEverySamples`'ta son `RecheckEverySamples`'lık ses yeniden denetlenir. `Other` çıkarsa o andan
  sonra engellenir ve `OtherVoiceHeard`.
- **Engelleme:** Bekletilen konuşma parçaları kadar sıfır gider (geriye bakış zaten sıfır gitmişti), sonrakiler sıfır.
- Parça bitince (konuşmasız parça) boşta düzenine dönülür. O parça boşta kuralıyla işlenir.
- `OtherVoiceHeard` her parçada en çok bir kez true olur.

- [ ] **Step 1: Write the failing tests**

Sahte çözümleyici parçanın değerine bakar (parça tek değerle dolu): `0`/`50` sessizlik, `300` kısık konuşma, `1000` kullanıcı,
`2000` başkası, `3000` kullanıcı "Ada", `4000` başkası "Ada". `IsSpeech`: |değer| ≥ 100. `HeardName`: 3000 ya da 4000.
`Verify`: çoğunluk 2000/4000 ise Other. Çağrı sayısını tutar. Testler `VoiceGateOptions()` varsayılanlarını açıkça yazar
(Görev 1 varsayılanı değiştirse de testler değişmez).

```csharp
[Fact] public void Silence_is_sent_as_zeros()                         // 5×50 → her çıktı tek 1600'lük sıfır dizi
[Fact] public void User_speech_is_held_until_decided_then_sent_with_lookback()
    // 5×50, 8×1000: ilk 7 kullanıcı parçasında Send boş; 8.'de 13 dizi (5 geriye bakış ham + 8 kullanıcı); 9.'da [1000]
[Fact] public void Other_voice_is_never_sent_and_reported_once()      // 20×2000: hiçbir ham 2000 yok, sıfır örnek = 20×1600, rapor 1 kez
[Fact] public void Short_segment_passes_unverified_when_guest_mode_is_off() // 3×1000 + 50: ham gider, Verify 0
[Fact] public void Short_other_segment_is_blocked_without_guest_report()  // 6×2000 + 50: sıfır, rapor yok, Verify 1
[Fact] public void Guest_speaking_right_after_user_is_cut_within_one_second() // 8×1000 + 20×2000: ham 2000 ≤ 10, rapor 1
[Fact] public void Quiet_speech_is_ignored()                          // 10×300: sıfır, Verify 0, rapor yok
[Fact] public void Guest_mode_blocks_user_without_name()               // kip: 20×1000 → ham yok, rapor yok
[Fact] public void Guest_mode_passes_user_starting_with_name()         // kip: 3000 + 12×1000 → hepsi ham
[Fact] public void Name_from_unknown_voice_passes_and_reports_guest()  // kip: 4000 + 12×2000 → ham, rapor 1
[Fact] public void Name_late_in_sentence_does_not_count()             // kip: 20×1000, 3000, 5×1000 → ham yok
[Fact] public void Allowance_lets_next_sentence_through_once()        // kip, Allow(8 sn): 12×1000 ham; 10×50; 12×1000 ham yok
[Fact] public void Allowance_expires()                                // kip, Allow(2 sn): 30×50; 12×1000 → ham yok
[Fact] public void Note_mode_needs_no_name_but_still_blocks_others()  // kip+not: 12×1000 ham; 10×50; 12×2000 ham yok
[Fact] public void Long_user_speech_keeps_passing_and_is_rechecked()  // 40×1000: hepsi ham, Verify ≥ 3
[Fact] public void Input_of_any_length_is_split_into_chunks()         // 4000 + 800 örnek sessizlik → gönderilen toplam 3200, sonra 4800
```

- [ ] **Step 2: Run** `dotnet test tests/CamAda.Core.Tests --filter VoiceGateTests` → FAIL.
- [ ] **Step 3: Implement** `VoiceGate.cs` (yukarıdaki türler ve karar düzeni).
- [ ] **Step 4: Run** → PASS. Ardından tüm testler.
- [ ] **Step 5: Commit** `feat: misafir kipi ses süzgeci kararları (Core)`.

---

### Task 4: Yerel çözümleyici ve süzgeç iş parçacığı

**Files:**
- Create: `src/CamAda/Assistant/Audio/SherpaVoiceAnalyzer.cs`
- Create: `src/CamAda/Assistant/Audio/VoiceFilter.cs`
- Create: `src/CamAda/Assistant/AdaProbes.Filter.cs` (`#if DEBUG`)
- Modify: `src/CamAda/Assistant/AdaProbes.cs` (`--ada-suzgec-sina` kaydı)

**Interfaces:**
- Consumes: `IVoiceAnalyzer`, `VoiceGate`, `GateInput`, `GateOutput` (Görev 3); `SpeakerEmbedder`, `VoiceModels`, `Voiceprint`
  (Görev 1); `WakeWord.NameKeywordLine` (Görev 2).
- Produces:
  - `internal sealed class SherpaVoiceAnalyzer : IVoiceAnalyzer, IDisposable`:
    - `static SherpaVoiceAnalyzer? TryCreate(string speakerDir, string wakeModelDir, Action<Exception> log)`: yerel nesne kurmadan
      önce `VoiceModels.Present` ve uyandırma dosyalarını denetler, eksikse null.
    - `Voiceprint? Voiceprint { get; set; }`, `double Threshold { get; set; }`.
    - `void Reset()`: VAD durumunu ve "Ada" akışını sıfırlar (her oturum başı).
    - Yalnız tek iş parçacığından çağrılır (süzgeç iş parçacığı; `Reset` oturum açılmadan önce).
  - `internal sealed class VoiceFilter : IDisposable`:
    - `VoiceFilter(VoiceGate gate, Action<GateOutput> output, Action<Exception> log)`: kendi arka plan iş parçacığını açar
      ("Ada ses süzgeci", sıralı kuyruk).
    - `void Post(short[] samples, GateInput input)`, `void AllowNextWithoutName(TimeSpan within)`: sıraya girer.
    - `output` süzgeç iş parçacığında çağrılır.
    - Çözümleyici atarsa günlüğe bir kez yazılır, süzgeç geçirgen olur (ham ses gider; Ada bugünkü gibi sürer).
    - `Dispose`: kuyruğu kapatır, en çok 1 sn bekler.

Ayrıntılar:
- VAD: Silero, eşik 0.5, `MinSilenceDuration` 0.25 sn, `MinSpeechDuration` 0.25 sn, `WindowSize` 512.
  `IsSpeech` her parçada `AcceptWaveform` sonra `IsSpeechDetected()`. Biten parça kuyruğu her seferinde boşaltılır (büyümesin).
- "Ada": uyandırma modeliyle ayrı `KeywordSpotter`. Anahtar satırı UTF-8 (BOM'suz) `%LOCALAPPDATA%\CamAda\ada-ad.txt` dosyasıyla
  verilir (`KeywordsBuf` ANSI sorunu; `WakeWordListener`'daki gibi). Eşik/güçlendirme Görev 1 tablosundan.
- `Verify`: `Voiceprint` null ise User. Değilse `SpeakerEmbedder.Compute` ve `Score ≥ Threshold` ise User.

- [ ] **Step 1: Write the Debug probe `--ada-suzgec-sina`** (önce başarısız olan denetim):
  - `--bos-klasor`: boş bir geçici klasörle `TryCreate` çağırır, null döndüğünü yazar (çökmeden).
  - Varsayılan: `deneme\tanitma.wav`'dan ses izi kurar. `kullanici.wav` ve `misafir.wav`'dan bir oda kurar: 4'er sn'lik parçalar
    sırayla, önce aralarında 1 sn sessizlikle, sonra arasız. Odayı `VoiceFilter` + gerçek çözümleyiciyle 100 ms'lik parçalar
    hâlinde (gerçek zamandan hızlı) geçirir.
  - Her parça için başlangıç, karar, gecikme (ms) ve `OtherVoiceHeard` yazılır. Özet: gönderilen kullanıcı sesi %'si,
    gönderilen misafir sesi %'si, ortalama ve en çok karar gecikmesi. `--misafir` kipi açık sayar, `ada.wav`'ı da ekler.
- [ ] **Step 2: Run** → derleme hatası ya da "tür yok" (FAIL).
- [ ] **Step 3: Implement** `SherpaVoiceAnalyzer` ve `VoiceFilter`.
- [ ] **Step 4: Run the probe**. Beklenen:
  - `--bos-klasor` null.
  - Kip kapalı: kullanıcı ≥%90 gider. Misafir sesinin gönderilen kısmı yalnız arasız geçişlerde, geçiş başına ≤1 sn.
  - Kip açık: "Ada" demeyen kullanıcı sesi ~%0. `ada.wav` cümleleri ≥9/10 gider.
  - Ortalama karar gecikmesi ≤1 sn.
- [ ] **Step 5: Delete the measurement recordings** (`%LOCALAPPDATA%\CamAda\deneme\*.wav`; misafirin sesi dahil). Sildiğini yaz.
- [ ] **Step 6: Commit** `feat: yerel ses çözümleyici ve süzgeç (misafir kipi)`.

---

### Task 5: `misafir_kipi` işlevi ve talimat

**Files:**
- Modify: `src/CamAda.Core/Assistant/AdaTools.cs`, `src/CamAda.Core/Assistant/AdaInstruction.cs`
- Modify: `src/CamAda/Assistant/AdaToolContext.cs` (`Func<bool, bool> SetGuestMode`),
  `src/CamAda/Assistant/AdaToolRunner.cs`, `src/CamAda/App.xaml.cs`, `src/CamAda/Assistant/AdaProbes.Room.cs`,
  `src/CamAda/Assistant/AdaProbes.Tools.cs` (bağlam kurucuları)
- Test: `tests/CamAda.Core.Tests/Assistant/AdaToolsTests.cs`, `AdaInstructionTests.cs`

**Interfaces:**
- Consumes: `UserConfirmGate.Armed` (Görev 2).
- Produces:
  - `AdaTools.MisafirKipi = "misafir_kipi"`.
  - `AdaToolContext.SetGuestMode`: `true/false` ister. Kip kullanılamıyorsa (çözümleyici yok) false döner. Varsayılan bağlamlarda
    `_ => false`. `AdaAssistant` kendi işleviyle değiştirir (Görev 6).
  - `AdaToolRunner.ConfirmPending` (`_shutdownGate.Armed || _rebootGate.Armed`).

Tam metinler:
- Tanım: `"Misafir kipini açar ya da kapatır. Kullanıcı 'misafir modunu aç', 'kalabalık bir ortamdayım', 'yanımda biri var' derse
  true, 'misafir modunu kapat', 'yalnızım' derse false ile çağır. Açıkken yalnız 'Ada' diye başlayan sözler dinlenir."`,
  parametre `("acik", "BOOLEAN", "true: açık")`.
- Sonuçlar:
  - açınca `"tamam: misafir kipi açık; kullanıcıya yalnız 'Misafir kipi açık, bana adımla seslen.' de"`;
  - kapatınca `"tamam: misafir kipi kapalı; kullanıcıya yalnız 'Misafir kipi kapandı.' de"`;
  - kullanılamıyorsa `"hata: misafir kipi şu an kullanılamıyor (ses tanıma yüklenemedi)"`.
- Talimat satırı (NOT satırından önce): `"MİSAFİR KİPİ: Kullanıcı yanında biri olduğunu ya da kalabalık bir ortamda olduğunu
  söylerse, ya da misafir modunu açmanı veya kapatmanı isterse misafir_kipi işlevini çağır."`

- [ ] **Step 1: Write the failing tests**: `Names_are_unique_snake_case` sayısı 29, `Assert.Contains(AdaTools.MisafirKipi, names)`.
  `misafir_kipi` açıklamasında "kalabalık" ve "Ada" geçer, `acik` BOOLEAN. `AdaInstructionTests`: talimat `"misafir_kipi"`
  içerir.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** tanım, talimat, bağlam alanı, yürütücü (`AdaTools.MisafirKipi => Done(GuestMode(Bool(args, "acik")))`)
  ve `ConfirmPending`. `--ada-islev-sina`'ya ekle: sahte `SetGuestMode` ile true/false/kullanılamaz, üç sonuç yazılır.
- [ ] **Step 4: Run** testler PASS. `--ada-islev-sina` üç sonucu tam metinle yazar.
- [ ] **Step 5: Commit** `feat: misafir_kipi işlevi ve talimat`.

---

### Task 6: Oturuma bağlama (`AdaAssistant`), ses izi dosyası ve ton

**Files:**
- Create: `src/CamAda/Assistant/VoiceprintFile.cs`
- Modify: `src/CamAda/Assistant/AdaAssistant.cs`, `src/CamAda/Assistant/Audio/AdaSpeaker.cs` (`AdaCue.Guest`),
  `src/CamAda/App.xaml.cs`, `src/CamAda/Assistant/AdaProbes.Room.cs`

**Interfaces:**
- Consumes: Görev 1–5'in hepsi.
- Produces:
  - `internal sealed class VoiceprintFile(string directory)`: dosya `ada-ses-izi.bin`, DPAPI `CurrentUser`.
    - `Voiceprint? Load(string model)`: yok, bozuk ya da başka model ise null.
    - `VoiceprintStatus Status(string model)` (`None`, `Ready`, `Stale`).
    - `bool Save(Voiceprint print)`, `bool Delete()`, `event Action? Changed`.
  - `AdaDependencies` + `VoiceprintFile Voiceprint`, `string VoiceModelDir`.
  - `AdaAssistant`: `bool IsGuestMode`, `event EventHandler<bool>? GuestModeChanged` (arayüz iş parçacığında, yalnız değişince).
  - `AdaCue.Guest`.

Değişiklikler (`AdaAssistant`):
- `_guest = new GuestMode(TimeProvider.System, TimeSpan.FromMinutes(settings.GuestModeMinutes))`.
- `_voice = SherpaVoiceAnalyzer.TryCreate(...)`: ilk `Start`'ta arka planda, bir kez. Null ise günlüğe bir kez yazılır.
- `UpdateGuest()`: `_guest.IsOn` gösterilenden farklıysa `GuestModeChanged`. Çağrıldığı yerler: `OnCheck` (2 dk dolması),
  `OnFiltered`, elle değiştirme, oturum açılışı.
- Oturum açılışı (`OpenCoreAsync`), `_voice` varsa:
  - `Reset`, `Voiceprint = _deps.Voiceprint.Load(VoiceModels.SpeakerModel)`, `Threshold = settings.SpeakerThreshold`.
  - `session.Filter = new VoiceFilter(new VoiceGate(_voice, options), o => _dispatcher.BeginInvoke(() => OnFiltered(session, o)), _deps.Log)`.
    `options` = `new VoiceGateOptions(DecideAfterSamples: …, MinSpeechRms: …)`, "Ölçümle belirlenen değerler" tablosundan.
  - `session.Filter.AllowNextWithoutName(TimeSpan.FromSeconds(settings.NoSpeechSeconds))`: ilk söz adsız geçer ("Hey Ada" da
    kısayol da Ada'ya seslenmektir; karar).
- `Send(session, samples)`: Google'a giden her ses buradan geçer: ön kayıt, köprü, mikrofon.
  - Süzgeç varsa `Post(samples, new GateInput(_guest.IsOn, _runner.Notes.Active))`.
  - Yoksa `Queue` ve bugünkü not ses düzeyi denetimi.
- `OnMicSamples`:
  - Yarı çift yönlü sıfırlama süzgeçten önce kalır (Ada'nın sesi çözümleyiciye hiç gitmez).
  - Ham sesle yerel söz kesme yalnız `!_guest.IsOn` ya da süzgeç yokken. Hold/Resume anahtarı `ApplyBargeIn` olarak ayrılır.
- `OnFiltered(session, output)`: `session != _session || session.Closing` ise atılır. Değilse her parça için:
  - `Queue`;
  - kip açık ve yarı çift yönlü değilse `ApplyBargeIn(session.BargeIn.OnMic(Pcm.Rms(chunk), adaTalking))`;
  - not açıksa ve RMS > `NoteVoiceRms` ise `Notes.UserSpoke()`.

  `output.OtherVoiceHeard` ise: `_guest.OtherVoiceHeard()` true dönerse `Speaker.PlayCue(AdaCue.Guest)`. Ardından `UpdateGuest()`
  ve `TraceDebug("misafir sesi")`.
- `OnCheck`, Ada'nın yanıtı bitince: `_runner.ConfirmPending || AdaText.EndsWithQuestion(session.Transcript.Ada)` ise
  `session.Filter?.AllowNextWithoutName(TimeSpan.FromSeconds(session.Settings.SilenceSeconds))` ve
  `TraceDebug("soru: sonraki söz adsız")`.
- Araç bağlamı: `SetGuestMode = on => { if (_voice is null) return false; _guest.SetManual(on); UpdateGuest(); return true; }`.
- `CloseAsync`: `session.Filter` arka planda atılır. Kip durumu korunur.
- `AdaCue.Guest`: `Tone(784, 90)` + 40 ms sessizlik + `Tone(784, 90)`.

- [ ] **Step 1: Extend the room probe** (`--ada-oda-sina`; önce başarısız olan denetim):
  - `--ses-izi-wav=<wav>`: WAV'dan ses izi kurar ve **geçici bir klasördeki** `VoiceprintFile`'a kaydeder (kullanıcının gerçek
    dosyasına dokunmaz).
  - `--misafir`: kipi baştan elle açar.
  - `GuestModeChanged` olaylarını yazar.
- [ ] **Step 2: Prepare TTS rooms** (scratchpad, PowerShell `System.Speech`, Zira, 16 kHz mono):
  - `zira-tanitma.wav`.
  - `odaMisafir.wav`, sırayla:
    1. "… market, hey Ada, set the volume to thirty."
    2. 3 sn bekleme.
    3. "Başka" ses (SSML `<prosody pitch="x-low" rate="-20%">` ve 0,8 kat örnekleme hızıyla perdesi düşürülmüş Zira): ~4 sn konuşma.
    4. 2 sn bekleme.
    5. "What time is it?" (Zira, adsız).
    6. 4 sn bekleme.
    7. "Ada, what time is it?"

  Deneme önce "başka" sesin puanını yazar. Eşiğin üstündeyse (Zira sayılıyorsa) misafir adımı `--misafir` ile denenir.
- [ ] **Step 3: Run** `CAMADA_ADA_FAKE_ROOM=odaMisafir.wav`, `--ada-oda-sina --ses-izi-wav=zira-tanitma.wav --oturum=1 --sure=60`
  → yeni seçenekler yokken FAIL.
- [ ] **Step 4: Implement** `VoiceprintFile`, `AdaCue.Guest` ve yukarıdaki `AdaAssistant` değişiklikleri. `App.xaml.cs`'te
  bağımlılıklar.
- [ ] **Step 5: Run the room probe again.** Beklenen günlük:
  - ses komutu yürür;
  - "başka" seste `misafir sesi` izi ve `GuestModeChanged true`;
  - adsız "What time is it?" için canlı yazı yok (Ada yanıtlamaz);
  - "Ada, what time is it?" yanıtlanır;
  - oturum sessizlikle kapanır;
  - kapanıştan sonra `misafir sesi` izi yok.

  `--misafir` ile ikinci çalıştırma: adsız komut yanıtlanmaz, adlı yanıtlanır.
- [ ] **Step 6: Run** `dotnet test tests/CamAda.Core.Tests` ve `dotnet build src/CamAda -c Release` (0 uyarı).
- [ ] **Step 7: Commit** `feat: misafir kipi oturumda (süzgeç, ton, elle açma)`.

---

### Task 7: Adada gösterim

**Files:**
- Modify: `src/CamAda/Island/IslandWindow.xaml` (`AdaRow`), `src/CamAda/Island/IslandWindow.xaml.cs`, `src/CamAda/App.xaml.cs`

**Interfaces:**
- Consumes: `AdaAssistant.GuestModeChanged`, `IsGuestMode` (Görev 6); `AdaText.ListeningLine` (Görev 2).
- Produces: `IslandWindow.ShowAdaGuest(bool on)`.

Ayrıntılar:
- `AdaRow`'a, `AdaDot`'un soluna `AdaGuestGlyph` gelir: Segoe Fluent "People" (`&#xE716;`), 13 px, `TextSecondaryBrush`,
  sağ boşluk 8. Yalnız kip açıkken görünür.
- `ShowAda`'nın varsayılan dinleme metni `AdaText.ListeningLine(_adaGuest)` olur.
- `ShowAdaGuest` durumu saklar ve satır açıksa metni ve simgeyi hemen yeniler.
- `AdaRow`'un erişilebilirlik adı kip açıkken `"Misafir kipi. "` ile başlar.
- `App.xaml.cs`: `_ada.GuestModeChanged += (_, on) => _island?.ShowAdaGuest(on);` ve ada kurulurken `ShowAdaGuest(_ada.IsGuestMode)`.

- [ ] **Step 1: Implement** yukarıdakileri.
- [ ] **Step 2: Run** `dotnet build src/CamAda -c Release` → 0 uyarı. Görsel denetim Görev 9'daki canlı denemede.
- [ ] **Step 3: Commit** `feat: adada misafir kipi simgesi`.

---

### Task 8: Sesini tanıtma (Ada paneli)

**Files:**
- Create: `src/CamAda.Core/Assistant/EnrollmentCheck.cs`
- Test: `tests/CamAda.Core.Tests/Assistant/EnrollmentCheckTests.cs`
- Create: `src/CamAda/Assistant/VoiceEnrollment.cs`
- Modify: `src/CamAda/Panels/AdaPanel.xaml`, `src/CamAda/Panels/AdaPanel.xaml.cs` (`AdaPanelServices` +
  `VoiceprintFile Voiceprint`, `Func<IDisposable?> ReserveMic`), `src/CamAda/Assistant/AdaAssistant.cs` (`ReserveMic`),
  `src/CamAda/App.xaml.cs`

**Interfaces:**
- Consumes: `VoiceEnrollmentText`, `Voiceprint` (Görev 1); `SpeakerEmbedder`, `VoiceModels` (Görev 1); `VoiceprintFile` (Görev 6).
- Produces:
  - `public enum SentenceCheck { Ok, TooQuiet, TooShort }`. `EnrollmentCheck.Check(int speechSamples, double speechRms)`:
    RMS < 0.02 ise `TooQuiet` (önce bakılır); konuşma < 16000 örnek (1 sn) ise `TooShort`.
  - `internal sealed class VoiceEnrollment : IDisposable`:
    - `static Task<VoiceEnrollment?> OpenAsync(string? micHint, string speakerDir)`: oturumla aynı mikrofon zinciri (yankı
      engelleyici, olmazsa düz; MTA). Mikrofon yoksa null.
    - `Task<SentenceResult> RecordSentenceAsync(CancellationToken ct)`: en çok 8 sn dinler. ≥1 sn konuşmadan sonra konuşma 0,8 sn
      durunca biter. Konuşma kısmını denetler, `Ok` ise gömme hesaplar.
  - `public sealed record SentenceResult(SentenceCheck Check, float[]? Embedding)`.
  - `AdaAssistant.ReserveMic()`: oturum varsa null. Yoksa dinlemeyi durdurur. Geri verilene kadar `Trigger` ve uyanma yok sayılır.
    Dönen `IDisposable` dinlemeyi geri açar.

Panel ("Mikrofon" listesinden sonra, "Sesim" başlığı):
- Açıklama: `Ada başka bir ses duyunca yalnız "Ada" diye başlayan sözlerini dinler. Kaydın saklanmaz; yalnız sayısal ses izin bu
  bilgisayarda şifreli tutulur.`
- Durum: `Sesin tanıtıldı` / `Henüz tanıtılmadı` / `Ses izi eski — yeniden tanıt` (`VoiceprintFile.Status`).
- Düğmeler: `Sesimi tanıt` (tanıtılmışsa `Yeniden tanıt`), `Sil` (iki adımlı, panelin `ConfirmGate`'iyle, 4 sn).
- Tanıtma alanı (yalnız sürerken):
  - cümle (15 px, kalın), `1/5`, durum satırı, `Vazgeç`;
  - her cümle için `RecordSentenceAsync`;
  - `TooQuiet` → `Ses çok kısık — mikrofona biraz yaklaşıp yeniden oku`; `TooShort` → `Cümlenin tamamını oku`;
  - cümle başına en çok 3 deneme, sonra `Tanıtılamadı — yeniden dene`;
  - beş cümle bitince `Voiceprint(VoiceModels.SpeakerModel, gömmeler)` → `Save` → `Sesin tanıtıldı`.
- `ReserveMic` null → `Ada konuşurken tanıtılamaz`. Mikrofon yok → `Mikrofon açılamadı`. Panel kapanırsa tanıtma iptal.
  Her durumda mikrofon ve rezervasyon `finally`'de bırakılır.

- [ ] **Step 1: Write the failing tests** (`EnrollmentCheckTests`)

```csharp
[Theory, InlineData(16000, 0.05, SentenceCheck.Ok), InlineData(8000, 0.05, SentenceCheck.TooShort),
 InlineData(16000, 0.01, SentenceCheck.TooQuiet), InlineData(8000, 0.01, SentenceCheck.TooQuiet)]
public void Check_sentence(int speech, double rms, SentenceCheck expected) => Assert.Equal(expected, EnrollmentCheck.Check(speech, rms));
```

- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** `EnrollmentCheck`, `VoiceEnrollment`, `ReserveMic`, panel ve bağlantılar.
- [ ] **Step 4: Run** testler PASS. `dotnet build src/CamAda -c Release` 0 uyarı.
- [ ] **Step 5: Commit** `feat: Ada panelinde sesini tanıtma`.

---

### Task 9: Kurulum ve kullanıcının canlı denemesi

**Files:**
- Modify: `docs/superpowers/specs/2026-11-25-sesli-asistan-design.md` ("Uyanma ve oturum" sonuna misafir kipine bir satırlık
  gönderme)

- [ ] **Step 1: Run** tüm testler ve `dotnet build src/CamAda -c Release` → PASS, 0 uyarı.
- [ ] **Step 2: Install** `.\tools\install.ps1`. Kurulumda `Assets\Speaker` dosyalarının çıktı klasöründe olduğunu denetle.
- [ ] **Step 3: USER STEP — canlı deneme.** Kullanıcıya listeyi ver, sonuçları al:
  1. Ada panelinden sesini tanıt (5 cümle).
  2. Yalnızken: "Ada" demeden doğal konuşma eskisi gibi. Ada uzun bir yanıt verirken ton çalmaz, simge çıkmaz.
  3. Komut verirken biri girip konuşsun: onun sözü komuta karışmaz; ton ve adada iki kişi simgesi, `“Ada” deyin, dinliyorum`.
  4. O kişiye yanıt ver: Ada susar. "Ada, …" ile istek yürür.
  5. "Bilgisayarı kapat" → "emin misin?" → "hayır" adsız geçer.
  6. Misafir gidince 2 dk sonra simge kalkar. Bu sürede yeni "Hey Ada"da simge baştan görünür.
  7. Kimse yokken "misafir modunu aç": Ada söyler, kip oturumlar arasında sürer. "Ada, misafir modunu kapat": kapanır.
  8. Tam ekranda (kısayolla oturum) ton duyulur.
  9. Biri konuşurken not alma: yalnız kullanıcının sözleri nota girer.
- [ ] **Step 4: Fix** kullanıcının bulduklarını (yeni görev gerekiyorsa dur ve sor).
- [ ] **Step 5: Commit** `docs: ana tasarıma misafir kipi`.
