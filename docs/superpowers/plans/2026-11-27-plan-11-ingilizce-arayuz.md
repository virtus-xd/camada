# Plan 11 — İngilizce Arayüz (1. adım) Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Camada'nın ekranda görünen ve ekran okuyucunun okuduğu bütün metinlerini İngilizce + Türkçe yapmak; dil Windows'un görüntü dilinden gelir, sağ tık menüsünden değişir. Ada'nın konuşması Türkçe kalır.

**Architecture:** İki `.resx` çifti (Core: `CoreStrings`, UI: `UiStrings`), İngilizce temel + `tr` uydu derlemesi, MSBuild'in ürettiği tipli sınıflar. Core'daki metin üreten fonksiyonlar kültürü parametre alır; UI `CurrentUICulture` ile çağırır. Dil başlangıçta bir kez uygulanır, değişince uygulama yeniden başlar. Bir "mandal" testi (`NoHardcodedTurkishTests`) bekleyen dosya listesini görev görev boşaltır.

**Tech Stack:** .NET 10, WPF, xUnit, MSBuild `GenerateResource` (StronglyTyped*), MSIX `makepri`.

**Spec:** `docs/superpowers/specs/2026-11-27-ingilizce-arayuz-design.md`

## Global Constraints

- Temel (nötr) dil İngilizce: `<NeutralLanguage>en</NeutralLanguage>`; Türkçe `*.tr.resx`. Eksik çeviri İngilizceye düşer, asla anahtar adı göstermez.
- `UiLanguage.Resolve(setting, windowsUi)`: `"tr"`→`tr-TR`, `"en"`→`en-US`, diğer/`null` → Windows görüntü dili Türkçe ise `tr-TR`, değilse `en-US`.
- Saat: Türkçe arayüzde `HH:mm`; diğerinde bölge kültürünün `"t"` deseni.
- Ada'nın modele giden metinleri, `AdaProbes*`, `tools/*.ps1`, kurtarma kısayolu adı değişmez. Ada testleri (Türkçe beklentiler) değişmeden geçer.
- Türkçe arayüzdeki her metin bugünküyle aynı kalır (çeviri dosyasına birebir taşınır).
- Alt menü başlığı her dilde "Dil / Language".
- İngilizce metinler kısa arayüz dili, Windows 11'in kendi İngilizce adlarıyla tutarlı ("Quick settings", "Clipboard", "Do not disturb", "Night light" gibi).
- Yüzde ve benzeri sıra değiştiren biçimler kaynakta: tr `"%{0}"`, en `"{0}%"`.
- Her commit mesajı `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` ile biter; yerel kimlik noreply.
- Sürüm 1.1.0 (yalnız Görev 8'de).

## Review Focus

1. **Türkçe metnin taşınırken değişmesi** (yazım, büyük/küçük harf, "…" yerine "..."): her görevde taşınan Türkçe metinler eski dizelerle birebir karşılaştırılır → her görevin son adımı `git diff` ile eski literal ↔ `tr.resx` değeri kontrolü.
2. **İngilizce arayüzde taşan/kesilen metin** (İngilizce çoğu zaman daha uzun ya da kısa; sabit genişlikli düğmeler): Görev 9 canlı denemesi her paneli İngilizce açar; sabit `Width` olan metin öğeleri görevlerde `MinWidth`/kırpma ile ele alınır.
3. **Dil değiştirince görev çubuğu gizli, ada kapalı kalması**: Görev 3 testi + canlı deneme (gizliyken dil değiştir).
4. **Uydu derlemenin pakette olmaması** (Store'da Türkçe kullanıcı İngilizce görür): Görev 8'de MSIX içeriği listelenir, `tr\CamAda.resources.dll` ve `tr\CamAda.Core.resources.dll` aranır.
5. **Ada'nın modele giden metninin yanlışlıkla çevrilmesi** (Ada İngilizce komut alıp Türkçe araçla çalışırken bozulur): Ada testleri her görevde tam takımla koşar; Görev 7'de ekran/model ayrımı dosya bazında yapılır.

---

### Task 1: Altyapı, dil çözümü ve korumalar

**Files:**
- Create: `src/CamAda.Core/Text/CoreStrings.resx`, `CoreStrings.tr.resx`, `src/CamAda.Core/Text/UiLanguage.cs`, `src/CamAda.Core/Text/Plural.cs`, `src/CamAda/Text/UiStrings.resx`, `UiStrings.tr.resx`
- Create: `tests/CamAda.Core.Tests/Text/UiLanguageTests.cs`, `ResourceParityTests.cs`, `NoHardcodedTurkishTests.cs`, `RepoRoot.cs`
- Modify: `src/CamAda.Core/CamAda.Core.csproj`, `src/CamAda/CamAda.csproj`, `src/CamAda.Core/Settings/AppSettings.cs` (+ `SettingsStore.cs` normalizasyon), `src/CamAda/App.xaml.cs`

**Interfaces (Produces):**
- `CamAda.Core.Text.UiLanguage`: `static readonly CultureInfo Turkish` (tr-TR), `English` (en-US); `static CultureInfo Resolve(string? setting, CultureInfo windowsUiCulture)`; `static bool IsTurkish(CultureInfo c)` (`TwoLetterISOLanguageName == "tr"`).
- `CamAda.Core.Text.Plural.Pick(int n, string one, string other) -> string` (`n == 1 ? one : other`).
- `CamAda.Core.Text.CoreStrings` ve `CamAda.Text.UiStrings`: MSBuild'in ürettiği `public` tipli sınıflar (`ResourceManager`, `Culture`, anahtar başına `string` özelliği). Core içinde açık kültürle okuma: `CoreStrings.ResourceManager.GetString(nameof(CoreStrings.X), culture)!`.
- `AppSettings.Language : string?` (null/"tr"/"en"; `SettingsStore` başka değeri null yapar).
- `App`: `static readonly CultureInfo WindowsUiCulture = CultureInfo.CurrentUICulture;` (sınıf yüklenirken, değişmeden önce); ayar yüklendikten hemen sonra (`_settings = _settingsStore.Load();` satırının altında, ilk pencereden önce) `CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = UiLanguage.Resolve(_settings.Language, WindowsUiCulture)`.
- `NoHardcodedTurkishTests` içindeki `Pending` listesi: sonraki görevler kendi dosyalarını bu listeden siler.

- [ ] **Step 1: Testleri yaz**
  - `UiLanguageTests` (`[Theory]`): (null, tr-TR)→tr-TR; (null, en-US)→en-US; (null, de-DE)→en-US; ("tr", en-US)→tr-TR; ("en", tr-TR)→en-US; ("xx", tr-TR)→tr-TR; ("", en-US)→en-US. `IsTurkish(tr-TR)` true, `IsTurkish(en-GB)` false. `Plural.Pick(1,"app","apps")`=="app", `(0)`=="apps", `(2)`=="apps".
  - `SettingsStoreTests`: `"Language":"tr"` korunur, `"Language":"fr"` → null, alan yoksa null.
  - `RepoRoot.Find()`: test derlemesinin klasöründen yukarı `CamAda.slnx` arar.
  - `ResourceParityTests`: `src/CamAda.Core/Text/CoreStrings*.resx` ve `src/CamAda/Text/UiStrings*.resx` için: anahtar kümeleri eşit; her anahtarda `{n}` yer tutucu kümesi eşit; boş değer yok. Ayrıca `CoreStrings.ResourceManager.GetString(nameof(CoreStrings.Language_MenuTitle), tr-TR)` == "Dil / Language" (uydu derlemenin yüklendiğini kanıtlar).
  - `NoHardcodedTurkishTests`:
    - Tarama: `src/CamAda/**/*.cs|*.xaml`, `src/CamAda.Core/**/*.cs` (`bin/`, `obj/` hariç).
    - C#: `//` ile başlayan satırlar atlanır; dize sabitleri (`"…"`, `$"…"`, `@"…"`, `$@"…"`) içinde `[çğıöşüÇĞİÖŞÜ]` varsa ihlal.
    - XAML (dilden bağımsız): `Text|Header|Content|ToolTip|Title|AutomationProperties.Name|AutomationProperties.HelpText` özniteliklerinde `{` ile başlamayan, harf içeren değer; ve `>…<` arası harf içeren düz metin → ihlal. Marka/simge izin listesi: `Wi‑Fi`, `Wi-Fi`, `Bluetooth`, `Spotify`, `Camada`, `Ada`, yalnız simge/sayı/noktalama içeren değerler.
    - `Permanent` (2. adım + geliştirici): `src/CamAda.Core/Assistant/AdaInstruction.cs`, `AdaTools.cs`, `AppAliases.cs`, `NameMatch.cs`, `SensitiveText.cs`, `MemoryBook.cs`, `Reminders.cs`, `ToolArgs.cs`, `src/CamAda.Core/Weather/WeatherAnswer.cs` (Görev 2.de oluşur), `src/CamAda/Assistant/AdaToolRunner.cs`, `src/CamAda/Assistant/CurrentInfoClient.cs`, `src/CamAda/Assistant/AdaSpeech.cs` (Görev 7'de oluşur), `src/CamAda/Assistant/AdaProbes*.cs`.
    - `Pending`: Step 2'de bugünkü ihlallerden üretilen dosya listesi (Permanent hariç).
    - Assert 1: Pending ∪ Permanent dışında ihlal yok. Assert 2: Pending'deki her dosya hâlâ ihlal içeriyor (liste sıkı kalsın; temizlenen dosya listeden silinmeli). Hata mesajı dosya:satır listeler.
- [ ] **Step 2: Kırmızıyı gör, Pending'i doldur.** `dotnet test tests/CamAda.Core.Tests` → UiLanguage/Plural/Parity derlenmez ya da kırmızı. Tarayıcıyı bir kez çalıştırıp çıkan dosya listesini `Pending`'e yaz; Assert 1–2 yeşil olmalı.
- [ ] **Step 3: Uygula.** csproj'lara `<NeutralLanguage>en</NeutralLanguage>` ve `EmbeddedResource Update` ile `StronglyTypedLanguage=CSharp`, `StronglyTypedClassName`, `StronglyTypedNamespace`, `StronglyTypedFileName=$(IntermediateOutputPath)<Ad>.Designer.cs`, `PublicClass=true`. Üretilen dosya derlemeye girmezse `<Compile Include>` ile eklenir; MSBuild bunu desteklemezse yedek: elle yazılmış `public static class` (her anahtar `=> ResourceManager.GetString(nameof(X), Culture)!`) — hangisi olduysa `Ruling:` olarak deftere. İlk anahtarlar: `Language_MenuTitle` (en/tr ikisi de "Dil / Language"), `Language_Automatic` ("Automatic (Windows)" / "Otomatik (Windows)"), `Language_Turkish` ("Türkçe"/"Türkçe"), `Language_English` ("English"/"English"). `UiLanguage`, `Plural`, `AppSettings.Language`, `App` kültür uygulaması.
- [ ] **Step 4: Yeşil.** `dotnet build CamAda.slnx` "0 Hata"; `ls src/CamAda/bin/Debug/net10.0-windows10.0.22621.0/tr/` → `CamAda.resources.dll`, `CamAda.Core.resources.dll`; `dotnet test tests/CamAda.Core.Tests` → 825 + yeniler, 0 başarısız.
- [ ] **Step 5: Commit** `feat: dil altyapısı (resx, UiLanguage, mandal testi)`.

### Task 2: Core metin üreticileri

**Files:** Modify `src/CamAda.Core/Time/ClockFormatter.cs`, `Power/BatteryText.cs`, `Clipboard/ClipAge.cs`, `Calendar/MonthGrid.cs`, `Network/NetworkIcons.cs`, `Bluetooth/BluetoothKinds.cs`, `Search/SearchRanker.cs`, `Search/SettingsPages.cs`, `Clipboard/ScreenshotName.cs`, `Weather/OpenMeteo.cs`; Create `Weather/WeatherAnswer.cs`; Pending'de kalan diğer Core dosyaları (`Audio/*`, `Apps/*`, `Media/*`, `Spotify/*`, `Island/*`, `Glass/*`, `Record/*`, `Settings/RecordSettings.cs`, `Startup/StartupTaskDecision.cs`, `Clipboard/*`): ekranda görünen dize varsa kaynağa, yoksa (iç anahtar, günlük) olduğu gibi bırakıp gerekçeyi yorumla yaz; testleri `tests/CamAda.Core.Tests/<Alan>/`.

**Interfaces (Produces):**
- `ClockFormatter.Time(DateTime local, CultureInfo ui, CultureInfo region) -> string`; `ClockFormatter.Date(DateTime local, CultureInfo ui) -> string` (desen `CoreStrings.Format_IslandDate`: tr `"ddd, d MMMM"`, en `"ddd, MMMM d"`).
- Metin üreten her public fonksiyona son parametre `CultureInfo ui` eklenir (pil, pano yaşı, takvim başlığı, ağ/Bluetooth özetleri, "Web'de ara", hava açıklaması `WeatherText.Describe(int code, CultureInfo ui)`).
- `SettingsPages`: her sayfa `(string NameEn, string NameTr, string Uri)`; `Name(CultureInfo ui)`; arama iki adı da eşleştirir.
- `ScreenshotName.For(DateTime now, CultureInfo ui)` → tr "Ekran görüntüsü …png", en "Screenshot …png".
- `WeatherAnswer` (Ada cevabı; `OpenMeteo`'dan taşınır, davranış aynı, içeride `UiLanguage.Turkish` sabit).

- [ ] **Step 1:** Her fonksiyon için iki dilli testler: `ClockFormatter.Time(14:12, tr-TR, en-US)`=="14:12", `(en-US, en-US)`=="2:12 PM", `(en-US, en-GB)`=="14:12"; `Date(Cuma 27 Kasım 2026, tr-TR)`=="Cum, 27 Kasım", `(en-US)`=="Fri, November 27"; pil: tr "Şarj oluyor · %80", en "Charging · 80%"; pano yaşı: tr "şimdi"/"2 dk"/"dün", en "now"/"2 min"/"yesterday"; takvim başlığı tr "Kasım 2026", en "November 2026"; hava kodu 2: tr "Parçalı bulutlu", en "Partly cloudy"; Bluetooth 2 cihaz: tr "2 cihaz bağlı", en "2 devices connected", 1 cihaz en "Buds connected"; ağ: en "Wi‑Fi: connected"/"Wi‑Fi off"/"No network connection"; "Web'de ara: “x”" ↔ "Search the web: “x”"; ayarlar araması "sound" ve "ses" ikisi de "Ses ayarları"/"Sound settings"'i bulur; ekran görüntüsü adı iki dilde. Var olan Türkçe testler `tr-TR` geçirilerek aynen korunur. Pending'den bu görevin Core dosyalarını sil.
- [ ] **Step 2:** `dotnet test` → yeni testler kırmızı (imza yok / İngilizce yok), mandal Assert 1 kırmızı.
- [ ] **Step 3:** Uygula; Türkçe değerler eski literal'lerden birebir kopyalanır; UI çağrı yerleri (`CurrentUICulture`, saat için `CurrentCulture`) güncellenir ki derleme geçsin.
- [ ] **Step 4:** `dotnet test` tamamı yeşil (Ada'nın hava testleri dahil, değişmeden); `dotnet build CamAda.slnx` 0 hata.
- [ ] **Step 5:** Review Focus 1: `git diff` ile kaldırılan Türkçe literal'lerin `CoreStrings.tr.resx`'te birebir bulunduğunu betikle kontrol et. Commit `feat: çekirdek metinleri iki dilli`.

### Task 3: Dil menüsü ve yeniden başlatma

**Files:** Modify `src/CamAda/Island/IslandWindow.xaml(.cs)`, `src/CamAda/Island/IslandCommands.cs`, `src/CamAda/App.xaml.cs`; Create `src/CamAda.Core/Text/LanguageMenu.cs`, test `tests/CamAda.Core.Tests/Text/LanguageMenuTests.cs`.

**Interfaces:**
- `LanguageMenu.Options` = `[(null, Language_Automatic), ("tr", Language_Turkish), ("en", Language_English)]`; `LanguageMenu.IsChecked(string? current, string? option) -> bool`; `LanguageMenu.NeedsRestart(string? current, string? chosen) -> bool` (aynı değer → false).
- `IslandCommands` + `Func<string?> GetLanguage`, `Action<string?> SetLanguage`.
- `App.SetLanguage(string? lang)`: ayarı kaydet → `Restart()` (mevcut; görev çubuğu durumu ayarla korunur).

- [ ] **Step 1:** `LanguageMenuTests`: IsChecked tablosu; NeedsRestart(null,null)=false, (null,"en")=true, ("tr","tr")=false.
- [ ] **Step 2:** kırmızı → **Step 3:** uygula; menüde "Cam görünümü"nün altına "Dil / Language" alt menüsü (başlık `CoreStrings.Language_MenuTitle`).
- [ ] **Step 4:** testler yeşil; build 0 hata. **Step 5:** commit `feat: Dil / Language menüsü`.

### Task 4: Ada ve sağ tık menüsü, şerit, plak, tepsi, pencereler, başlat/arama

**Files:** `src/CamAda/Island/*.xaml(.cs)`, `src/CamAda/Shell/StripAppBar.xaml`, `src/CamAda/Record/*.xaml(.cs)`, `src/CamAda/Panels/TrayPanel.xaml(.cs)`, `WindowsPanel.xaml(.cs)`, `StartPanel.xaml(.cs)`, `src/CamAda/Media/NowPlayingCard.xaml(.cs)`, `src/CamAda/App.xaml.cs`'teki ekran metinleri (mesaj kutuları, ipuçları), `QuickSettings/QuickGlyphs.cs` erişilebilirlik adları.

- [ ] **Step 1:** Bu dosyaları Pending'den sil → `dotnet test` mandal kırmızı (dosya:satır listesi = iş listesi).
- [ ] **Step 2:** Her metni `UiStrings`'e taşı (anahtar `Alan_Öğe`, tr = eski metin birebir, en = yeni); XAML `{x:Static t:UiStrings.X}`; sayılı metinler `Plural.Pick`; sabit genişlikli metin öğelerinde İngilizce uzunluğunu kontrol et.
- [ ] **Step 3:** `dotnet test` yeşil (mandal + parity), build 0 hata; Review Focus 1 betiği.
- [ ] **Step 4:** commit `feat: ada, menü ve başlat paneli iki dilli`.

### Task 5: Sistem panelleri

**Files:** `src/CamAda/Panels/QuickSettingsPanel*`, `QuickTile*`, `SoundPanel*`, `ClipboardPanel*`, `WifiListView*`, `BluetoothListView*`, `CalendarPanel*`, `src/CamAda/QuickSettings/*`, `src/CamAda/Audio/*`, `src/CamAda/Clips/*` (Pending'de olanlar).

- [ ] Adımlar Görev 4 ile aynı (Pending'den sil → kırmızı → taşı → yeşil + Review Focus 1 → commit `feat: sistem panelleri iki dilli`). Erişilebilirlik adları (`AutomationProperties.SetName`) dahil; takvimin gün adı okuma metni (`"d MMMM yyyy, dddd"` + ", bugün") kaynak desenle.

### Task 6: Spotify

**Files:** `src/CamAda/Spotify/SpotifyConnectView.xaml(.cs)`, `LoopbackCallback.cs` (tarayıcıdaki dönüş sayfası; HTML'deki `lang` özniteliği UI diline göre).

- [ ] Görev 4 adımları; commit `feat: Spotify bağlama ekranı iki dilli`.

### Task 7: Ada'nın ekrandaki yüzü

**Files:** `src/CamAda/Panels/AdaPanel.xaml(.cs)`, `src/CamAda/Assistant/AdaState.cs`, `src/CamAda/Assistant/AdaAssistant.cs`, `src/CamAda.Core/Assistant/LiveErrors.cs`, `ApiKeyText.cs` ve Pending'de kalan diğer Ada dosyaları; Create `src/CamAda/Assistant/AdaSpeech.cs` (modele giden dizeler buraya taşınır, Permanent listede).

- [ ] **Step 1:** Dosya dosya ayır: ekranda görünen (durum satırı, hata, panel etiketi, düğme) → `UiStrings`/`CoreStrings`; modele giden (istem, araç adı/açıklaması, modele dönen sonuç) → `AdaSpeech` sabitleri, aynen. Şüpheliyi ekranda görünen say.
- [ ] **Step 2:** İngilizce arayüzde Ada panelinin üstünde not: `UiStrings.AdaPanel_TurkishOnlyNote` = en "Ada speaks Turkish for now — English is coming.", tr değeri boş olamaz (parity) → tr "Ada şimdilik Türkçe konuşuyor." ve not yalnız `!UiLanguage.IsTurkish(CurrentUICulture)` iken görünür.
- [ ] **Step 3:** Pending'den sil → kırmızı → taşı → `dotnet test` tamamı yeşil (Ada testleri değişmeden) → Review Focus 1 → commit `feat: Ada paneli ve durum satırları iki dilli`.

### Task 8: Store paketi

**Files:** Create `packaging/Strings/en-US/Resources.resw`, `packaging/Strings/tr-TR/Resources.resw`; Modify `packaging/Package.appxmanifest`, `tools/package.ps1`, `packaging/store-identity.json` (gerekiyorsa), sürüm.

- [ ] **Step 1:** Manifestte `DisplayName`, `Description` ve "Görev çubuğunu geri getir" girişi `ms-resource:` olur; `Resources` içinde `en-US` (varsayılan) ve `tr-TR`; sürüm `1.1.0.0`.
- [ ] **Step 2:** `package.ps1`: yayım klasörüne `makepri createconfig /cf priconfig.xml /dq en-US` + `makepri new /pr <staging> /cf priconfig.xml /mn AppxManifest.xml /of resources.pri`; `makeappx pack` aynı.
- [ ] **Step 3 (doğrulama):** `.\tools\package.ps1` → hatasız; `makeappx unpack` ya da zip listesi ile pakette `resources.pri`, `tr\CamAda.resources.dll`, `tr\CamAda.Core.resources.dll` var; `makepri dump` çıktısında `RestoreTaskbar` için en-US ve tr-TR değerleri var.
- [ ] **Step 4:** commit `build: Store paketi yerelleştirmesi, 1.1.0`.

### Task 9: Mandal kapanışı, belgeler, canlı deneme

- [ ] **Step 1:** `Pending` boş olmalı; boş değilse kalan dosyaları ilgili göreve göre bitir. `dotnet test` tamamı yeşil; `dotnet build CamAda.slnx -c Release` 0 uyarı/0 hata.
- [ ] **Step 2:** README.md / README.tr.md "Status": "The UI is available in English and Turkish; Ada speaks Turkish for now (English is next)." / Türkçe karşılığı; CONTRIBUTING "Language" maddesi: yeni metin = iki `.resx`'e anahtar; `packaging/store-listing.md`'ye İngilizce liste (kısa açıklama, açıklama, 7 özellik, arama terimleri).
- [ ] **Step 3:** Commit `docs: iki dilli arayüz`.
- [ ] **Step 4 (kullanıcıyla canlı deneme):** `tools/install.ps1` ile kur; menüden English → yeniden başlar, görev çubuğu durumu aynı; her panel İngilizce, taşma yok, saat bölgeye göre (kullanıcıda "2:12 PM" İngilizcede, Türkçede "14:12"); "Dil / Language" → Türkçe → bugünküyle aynı; Otomatik; görev çubuğu gizliyken dil değiştir. Bulunanlar düzeltilir (TDD), sonra son inceleme.
