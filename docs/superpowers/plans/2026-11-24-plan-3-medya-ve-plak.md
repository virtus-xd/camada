# Plan 3: Medya ve Plak Çalar — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Spotify'da çalan müziği adaya (küçük hâlde dönen mini plak + şarkı, genişlemiş hâlde müzik bölümü ve Müzik paneli) ve masaüstünün sol kenarına yarı gömülü, gerçekçi, dönen bir plak çalara taşımak; boştaki adaya tarih ve hava durumunu eklemek; beğen ve sıradaki şarkılar için Spotify Web API'ye bağlanmak.

**Architecture:** Çalma durumu tek kaynaktan gelir: Windows sistem medya oturumu (`Windows.Media.Control`), yalnızca Spotify oturumu seçilir; ada, Müzik paneli, plak ve kart aynı `NowPlayingService`'i dinler. Saf mantık `CamAda.Core`'da birim testli (oturum seçimi, zaman çizelgesi, küçük hâl kararı, plak dönüş fiziği, yerleşim, görünürlük, disk ve yansıma dokusu üretimi, baskın renk, Spotify PKCE ve JSON, hava durumu). Plak, pencerelerin arkasında duran (z-sırasında en altta) saydam bir pencerededir; durgun katmanlar bir kez çizilir, dönen katman `BitmapCache` ile yalnız döndürülür ve dönüş yalnız müzik çalarken ve plak görünürken çalışır.

**Tech Stack:** .NET 10, C#, WPF, ManagedShell 0.0.374, WinRT `Windows.Media.Control` (TFM `net10.0-windows10.0.22621.0` ile hazır), `System.Security.Cryptography.ProtectedData` 10.0.15 (Spotify token'ı DPAPI ile), xUnit, `Microsoft.Extensions.TimeProvider.Testing`.

**Spec:** `docs/superpowers/specs/2026-11-23-cam-ada-design.md` (özellikle §1, §5 Spotify kuralları, §7.1, §7.2 müzik bölümü, §7.4 "Müzik", §7.8 plak çalar, §7.9, §8, §9 risk 4, §10). Tasarım tuvali bu hesaptan açılamadı; tuvalin sabitlemediği görsel değerler aşağıda bu planda sabitlenmiştir ve her görevin elle doğrulama adımında kullanıcıya ekran görüntüsüyle gösterilir.

## Yol haritası (bu plan 3/4)

Plan 1 ve Plan 2 bitti (main). Bu plan: medya ve plak çalar. Plan 4: sistem panelleri (hızlı ayarlar, ses/mikrofon, pano, takvim, bildirimler, kısa genişlemeler, genel kısayol tuşları). Küçük adadaki bildirim sayısı ve mikrofon işareti Plan 4'te eklenir.

## Global Constraints

- Plan 1–2'nin tüm kısıtları geçerli (Windows 11 build 26300, `CamAda.Core` net10.0 WPF'siz, Explorer'a enjeksiyon yok, Türkçe arayüz, Manrope, commit sonu `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`).
- Müzik kaynağı yalnızca **Spotify**: kaynak AppUserModelId'si `spotify` içeren medya oturumu (büyük/küçük harf duyarsız). Başka uygulamaların (tarayıcı, video) oturumları gösterilmez.
- Kapak (ada, panel, plak etiketi) **Windows medya oturumunun küçük resminden** alınır, Spotify Web API'den değil. Web API yalnızca **beğen** ve **sıradaki şarkılar** için.
- Spotify Web API (Şubat 2026 geliştirici modu): PKCE, kullanıcının kendi Client ID'si, yönlendirme adresi `http://127.0.0.1:43821/callback`, kapsamlar `user-library-read user-library-modify user-read-playback-state user-read-currently-playing`. Uç noktalar: `GET /v1/me/player/currently-playing` (parçanın `item.uri`'si), `GET /v1/me/library/contains?uris=spotify:track:<id>` (`[true]`/`[false]`), `PUT` ve `DELETE /v1/me/library?uris=…`, `GET /v1/me/player/queue`. Eski `/me/tracks` uç noktaları kaldırıldı, kullanılmaz. 429'da `Retry-After` süresince istek atılmaz.
- Plak: ana ekranın **sol** kenarı; çapın **%45**'i ekran dışında; alt kenarı ekranın altından **70** DIP yukarıda; çap **320** DIP; etiket çapı çapın **0,3375** katı (320 → **108** DIP). Hepsi ayardan değişir. Ekran dışı kısım hiçbir zaman başka monitörde görünmez (pencere ana ekranın sol kenarından başlar, disk pencere içinde kırpılır).
- Dönüş: **33⅓** dev/dk = **200 °/sn** (tur 1,8 sn); çalmaya başlayınca **0,8** sn'de hızlanır, duraklayınca **1,6** sn'de yavaşlayarak durur; hiçbir şey çalmıyorken durgun. "Animasyonları azalt" açıksa plak ve mini plak dönmez, ekolayzır kıpırdamaz.
- Plak pencerelerin arkasında durur (yalnızca masaüstü görünürken görünür), görev çubuğunda ve Alt+Tab'da yoktur; tıklamak çal/duraklat yapar ama plağı pencerelerin önüne getirmez. Pikap kolu yok; etiketin (kapağın) üzerine yazı eklenmez.
- Müzik kartı: plağın görünen kısmının **16** DIP sağında, plağın merkezine göre dikeyde ortalı, **300** DIP genişlik; plağın ya da kartın üstüne gelince **0,1** sn'de açılır, ikisinden de çıkınca **0,3** sn sonra kapanır; boş durumda metin **"Şu an bir şey çalmıyor"** ve **"Spotify'ı aç"** düğmesi.
- Genişlemiş adada müzik bölümü **150** DIP, uygulamalar ile `^` arasında. Genişlemiş hap genişliği `min(1250, 920 + max(0, n − 5) × 38)`; ada penceresi **1350** DIP genişlik (önceki 770/1100/1200 değerlerinin yerine).
- Küçük ada çalarken: solda **24** DIP dönen mini plak (kapak etiketli), şarkı adı (12,5 kalın) ve sanatçı (11,5 ikincil) tek satırda `"Şarkı · Sanatçı"`, kırpılır; sağda 4 çubuklu ekolayzır (3×12 DIP, `#FFC27A`) ve saat. Boştayken: tarih, hava durumu (ör. `"Pzt, 23 Kasım · 12° Bulutlu"`) ve saat.
- Hava durumu: anahtarsız **Open-Meteo**; konum ayardan (`Weather.Name`; koordinat yoksa bir kez Open-Meteo coğrafi kodlamasıyla bulunup ayara yazılır); 30 dakikada bir yenilenir; ayar yoksa hava durumu gösterilmez.
- Boştayken işlemci ~%0: müzik çalmıyorken ya da plak/ada görünmüyorken (örtülü, tam ekran, çizgi hâli) hiçbir kare animasyonu, zamanlayıcı yoklaması ya da ağ isteği çalışmaz (hava durumu yenilemesi hariç).

## Review Focus

1. **Spotify uygulama çalışırken kapanır ya da sonradan açılır** → ada boşta hâline döner, plak yavaşlayarak durur, kart boş durumu gösterir; Spotify yeniden açılınca CamAda'yı yeniden başlatmadan her şey geri gelir. (Görev 1 testi + elle adım)
2. **Ana ekranın solunda başka bir monitör var** → plağın ekran dışı %45'i o monitörde görünmez; çözünürlük/ölçek değişince plak yeniden yerleşir. (Görev 5 `RecordLayout` testleri + elle adım)
3. **Kapağı olmayan ya da bozuk küçük resimli içerik** (podcast, yerel dosya, reklam) → etiket düz koyu etiket olur, hâle rengi varsayılana düşer, çökme yok. (Görev 1 `DominantColor` testleri, Görev 6 elle adım)
4. **Spotify Web API bağlı değil, token süresi doldu, 401/403/429 ya da internet yok** → beğen ve "Sıradaki" gizlenir ya da "Bağlan" gösterilir; oynat/duraklat/ileri/geri medya oturumuyla çalışmaya devam eder. (Görev 8 testleri)
5. **Win+D (masaüstünü göster) ve uyku sonrası** → plak görünür ve tıklanabilir kalır; uyandıktan sonra dönüş sıçramaz. (Görev 5 elle adım, Görev 6 `RecordSpin` testi)

---

## Dosya yapısı

```
src/CamAda.Core/
  Media/NowPlaying.cs, MediaSourcePicker.cs, MediaTimeline.cs, CompactViewRule.cs, DominantColor.cs
  Record/RecordSpin.cs, RecordLayout.cs, RecordVisibility.cs, DiscTexture.cs, SheenTexture.cs
  Weather/WeatherNow.cs, OpenMeteo.cs, WeatherText.cs
  Spotify/SpotifyAuth.cs, SpotifyToken.cs, OAuthCallback.cs, SpotifyJson.cs
  Settings/AppSettings.cs (Record, Weather, SpotifyClientId), Settings/RecordSettings.cs, Settings/SettingsStore.cs (Sanitize)
  Island/IslandLayout.cs (yeni genişlikler)
src/CamAda/
  Media/NowPlayingService.cs
  Media/MiniRecord.xaml(.cs)          (ada ve müzik bölümündeki küçük dönen plak)
  Media/EqualizerBars.cs
  Media/NowPlayingCard.xaml(.cs)      (Müzik paneli ve plak kartının ortak içeriği)
  Panels/MusicPanel.cs
  Record/RecordWindow.xaml(.cs), Record/RecordVisual.xaml(.cs), Record/RecordCard.cs
  Weather/WeatherService.cs
  Spotify/SpotifyClient.cs, Spotify/SpotifyTokenStore.cs, Spotify/LoopbackCallback.cs, Spotify/SpotifyConnectView.xaml(.cs)
  Island/IslandWindow.xaml(.cs), Island/IslandBar.xaml(.cs), App.xaml.cs
tools/MediaSessionTest/                 (elle denemeler için sahte "Spotify" medya oturumu; çözüme eklenmez)
```

---

### Task 1: Çalma durumu servisi ve deneme medya oturumu

**Files:**
- Create: `src/CamAda.Core/Media/NowPlaying.cs`, `MediaSourcePicker.cs`, `MediaTimeline.cs`, `CompactViewRule.cs`, `DominantColor.cs`, `src/CamAda/Media/NowPlayingService.cs`, `tools/MediaSessionTest/MediaSessionTest.csproj`, `tools/MediaSessionTest/Program.cs`
- Modify: `src/CamAda/App.xaml.cs` (servisi oluştur, kapanışta `Dispose`)
- Test: `tests/CamAda.Core.Tests/Media/MediaTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace CamAda.Core.Media;
  public enum PlaybackState { Stopped, Paused, Playing }
  public sealed record NowPlaying(string SourceAppId, string Title, string Artist, string Album, PlaybackState State,
      TimeSpan Position, TimeSpan Duration, DateTimeOffset PositionUpdatedAt);
  public static class MediaSourcePicker
  {
      // Kaynak kimliği "spotify" içeren (OrdinalIgnoreCase) ilk oturumun sırası; yoksa -1. Windows'un geçerli oturumu
      // başka bir uygulama olsa da Spotify seçilir.
      public static int PickSpotify(IReadOnlyList<string> sourceAppIds);
  }
  public static class MediaTimeline
  {
      // Çalıyorsa Position + (now − PositionUpdatedAt), değilse Position; 0 ile Duration arasına kırpılır (Duration 0 ise üst sınır yok).
      public static TimeSpan PositionAt(NowPlaying track, DateTimeOffset now);
      public static string Format(TimeSpan t);                       // "0:07", "3:07", "1:02:03"
      public static double? Progress(NowPlaying track, DateTimeOffset now); // Duration 0 ise null, değilse 0..1
  }
  public enum CompactView { Idle, Music }
  public static class CompactViewRule
  {
      public static readonly TimeSpan Grace = TimeSpan.FromSeconds(3);
      // Parça yoksa Idle; çalıyorsa Music; duraklamış/durmuşsa son çalmadan beri Grace dolmadıysa Music (şarkı geçişi), sonra Idle.
      public static CompactView Decide(NowPlaying? track, DateTimeOffset? lastPlayingAt, DateTimeOffset now);
  }
  public readonly record struct Rgb(byte R, byte G, byte B);
  public static class DominantColor
  {
      public static readonly Rgb Fallback = new(0x5E, 0x4C, 0x86);
      // BGRA32 pikseller: alfa < 128, en parlak kanalı < 40 olanlar ve doygunluğu < 0,15 olanlar atlanır; kalanlar 12 ton kovasına
      // (30°) ayrılır, en kalabalık kovanın ortalaması döner; uygun piksel yoksa (ya da dizi boşsa) Fallback.
      public static Rgb From(ReadOnlySpan<byte> bgra);
  }
  namespace CamAda.Media;
  public sealed class NowPlayingService : IDisposable
  {
      public static Task<NowPlayingService> CreateAsync(Dispatcher dispatcher);
      public NowPlaying? Current { get; }       // Spotify oturumu yoksa null
      public DateTimeOffset? LastPlayingAt { get; }
      public ImageSource? Cover { get; }          // donmuş, en fazla 300 px; kapak yoksa null
      public Rgb CoverColor { get; }              // kapak yoksa DominantColor.Fallback
      public event EventHandler? Changed;         // UI iş parçacığında: parça, durum, zaman çizelgesi ya da kapak değişince
      public Task TogglePlayPauseAsync();
      public Task NextAsync();
      public Task PreviousAsync();
      public void OpenSpotify();                  // "spotify:" adresini kabukla açar; Spotify yoksa sessiz
  }
  ```

- [ ] **Step 1: Başarısız testleri yaz** (`MediaTests`):
  - `PickSpotify(["Chrome", "Spotify.exe"]) == 1`; `["SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"] == 0`; `["msedge.exe"] == -1`; boş liste `-1`.
  - `PositionAt`: çalarken `Position 10 sn`, 2,5 sn sonra `12,5 sn`; duraklamışken değişmez; `Duration 60 sn` ve 90 sn sonra `60 sn`; `Duration 0` iken kırpılmaz.
  - `Format`: `7 sn → "0:07"`, `187 sn → "3:07"`, `3723 sn → "1:02:03"`; `Progress`: `Duration 0 → null`, yarıda `0.5`.
  - `CompactViewRule`: parça yok → Idle; Playing → Music; Paused ve son çalma 2 sn önce → Music; 4 sn önce → Idle; `lastPlayingAt` null ve Paused → Idle.
  - `DominantColor`: boş dizi → Fallback; tamamı saydam → Fallback; tamamı siyah → Fallback; %70 kırmızı (`#D02020`) + %30 mavi → kırmızıya yakın (R > 180, G < 60, B < 60); gri tonlu görüntü → Fallback.
- [ ] **Step 2:** `dotnet test --filter "FullyQualifiedName~MediaTests"` → derleme hatası.
- [ ] **Step 3:** Core tiplerini uygula; testler geçsin.
- [ ] **Step 4: `NowPlayingService`'i uygula.** `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`; `SessionsChanged`'de `PickSpotify` ile oturum yeniden seçilir ve o oturumun `MediaPropertiesChanged`, `PlaybackInfoChanged`, `TimelinePropertiesChanged` olaylarına abone olunur (eskisinden çıkılır). WinRT olayları arka plan iş parçacığından gelir: durum okunur, `Changed` dispatcher'a gönderilir. Kapak `MediaProperties.Thumbnail.OpenReadAsync()` ile okunur, `BitmapImage` (`DecodePixelWidth = 300`, `CacheOption.OnLoad`) donmuş hâle getirilir; piksellerinden (`FormatConvertedBitmap` → Bgra32) `DominantColor.From`. Kapak okuma hatası kapağı null yapar, çökertmez. Parça ya da durum değişince `LastPlayingAt` Playing iken güncellenir. Denetimler `TryTogglePlayPauseAsync`, `TrySkipNextAsync`, `TrySkipPreviousAsync`; oturum yoksa hiçbir şey yapmaz.
- [ ] **Step 5: Deneme medya oturumu (`tools/MediaSessionTest`).** `net10.0-windows10.0.22621.0` konsol uygulaması, `AssemblyName` `SpotifyDeneme` (exe adı da "spotify" içersin), çözüme eklenmez. Açılışta `SetCurrentProcessExplicitAppUserModelID("CamAda.SpotifyDeneme")` (kimlik "spotify" içerdiği için seçilir). `Windows.Media.Playback.MediaPlayer` ile bellekte üretilen sessiz bir WAV'ı döngüde çalar; `SystemMediaTransportControls` üzerinden 3 sahte parça ("Deneme Şarkı 1–3", sanatçı "CamAda", albüm "Deneme", 300×300 düz renkli kapak: `#D02020`, `#2050D0`, kapaksız) gösterir. Oynat/duraklat/ileri/geri düğmelerine yanıt verir. Argümanlar: `--no-cover` (kapaksız başla), `--seconds N` (N sn sonra çık, varsayılan 600). Çalıştırma: `dotnet run --project tools/MediaSessionTest`.
- [ ] **Step 6: Elle doğrula:** Deneme oturumu açıkken bir doğrulama betiği (`NowPlayingService`'in `Changed` olayında parçayı `%TEMP%` altına yazan geçici iz ya da hata ayıklayıcı) şunları gösterir: başlık "Deneme Şarkı 1", durum Playing, kapak rengi kırmızıya yakın; Windows medya katmanından (Win+A ya da klavye medya tuşu) duraklatınca Paused; deneme uygulaması kapatılınca `Current == null`; yeniden açılınca CamAda yeniden başlatılmadan parça geri gelir. Chrome'da video oynarken bile Spotify oturumu seçilir. Geçici iz commit'ten önce silinir.
- [ ] **Step 7: Commit** — `feat: Spotify çalma durumu servisi`

---

### Task 2: Adanın küçük hâli — müzik ve boşta

**Files:**
- Create: `src/CamAda.Core/Record/RecordSpin.cs`, `src/CamAda/Media/MiniRecord.xaml(.cs)`, `src/CamAda/Media/EqualizerBars.cs`
- Modify: `src/CamAda/Island/IslandWindow.xaml(.cs)`
- Test: `tests/CamAda.Core.Tests/Record/RecordSpinTests.cs`

**Interfaces:**
- Consumes: `NowPlayingService`, `CompactViewRule` (Task 1).
- Produces:
  ```csharp
  namespace CamAda.Core.Record;
  public sealed class RecordSpin
  {
      public const double PlayingDegreesPerSecond = 200;              // 33⅓ dev/dk
      public static readonly TimeSpan SpinUp = TimeSpan.FromSeconds(0.8);
      public static readonly TimeSpan SpinDown = TimeSpan.FromSeconds(1.6);
      public static readonly TimeSpan MaxStep = TimeSpan.FromMilliseconds(100); // uyku/takılma sonrası sıçrama olmasın
      public double Angle { get; }      // [0, 360)
      public double Velocity { get; }   // °/sn, ≥ 0
      public bool IsMoving { get; }     // Velocity > 0
      // elapsed MaxStep ile sınırlanır; hız hedefe (çalıyorsa 200, değilse 0) doğrusal ivmeyle gider; reduceMotion: hız anında 0, açı değişmez.
      public void Advance(TimeSpan elapsed, bool playing, bool reduceMotion);
  }
  namespace CamAda.Media;
  // Kare kapak etiketli küçük dönen plak (Diameter DIP). Dönüş CompositionTarget.Rendering ile yalnız IsMoving ya da çalarken ve
  // IsVisible iken; durunca olaydan çıkar (boşta %0 işlemci).
  public sealed partial class MiniRecord : UserControl
  {
      public double Diameter { get; set; }
      public void Show(ImageSource? cover, bool playing);
  }
  public sealed class EqualizerBars : FrameworkElement   // 4 çubuk, 3×12 DIP, #FFC27A; çalarken 12 kare/sn sözde rastgele, duraklayınca alçak ve durgun
  {
      public bool IsPlaying { get; set; }
  }
  ```

- [ ] **Step 1: Başarısız testleri yaz** (`RecordSpinTests`, adımlar 10 ms): duruştan çalarken 0,4 sn sonra hız 100 ± 1, 0,8 sn sonra 200; tam hızda 1,8 sn daha → açı tam tur ilerler (başlangıç açısına ±1° döner); duraklatınca 1,6 sn sonra hız 0 ve hiçbir adımda negatif değil; açı her zaman `[0, 360)`; `reduceMotion` iken hız 0 ve açı değişmez; tek `Advance(10 sn)` çağrısı açıyı en fazla `200 × 0,1 = 20°` ilerletir.
- [ ] **Step 2:** Testlerin başarısız olduğunu gör.
- [ ] **Step 3:** `RecordSpin`'i uygula; testler geçsin.
- [ ] **Step 4: Küçük hâli kur.** `CompactContent` iki görünüme ayrılır. Müzik (`CompactViewRule.Decide == Music`): solda `MiniRecord` (24 DIP), 8 DIP boşluk, `"Şarkı · Sanatçı"` (şarkı 12,5 kalın, sanatçı 11,5 `TextSecondaryBrush`, tek satır, `CharacterEllipsis`), sağda `EqualizerBars` ve saat. Boşta: mevcut tarih + saat (hava durumu Görev 3'te eklenir). Görünüm değişimi 0,2 sn çapraz geçiş. `MiniRecord` etiketi kapak (daire kırpılmış), kapak yoksa düz koyu etiket; disk `#0D0D10` + ince açık kenar. `Changed` gelince görünüm ve metin güncellenir; `CompactViewRule` ince ayarı için duraklamada `Grace` sonunda bir kez yeniden değerlendirilir (tek atımlık zamanlayıcı). Ada tam ekranda gizliyken, çizgi hâlindeyken ya da genişlemişken mini plak ve ekolayzır kare animasyonu çalışmaz.
- [ ] **Step 5: Elle doğrula** (deneme medya oturumuyla): ekran görüntüsünde küçük adada dönen kırmızı etiketli mini plak, "Deneme Şarkı 1 · CamAda", kıpırdayan ekolayzır ve saat; duraklatınca mini plak yavaşlayarak durur, 3 sn sonra ada tarih + saate döner; "animasyonları azalt" açıkken mini plak dönmez. İşlemci: müzik çalmıyorken CamAda süreci 10 sn ortalamada %0,5'in altında (Get-Counter `\Process(CamAda)\% Processor Time` ya da süreç işlemci süresi farkı).
- [ ] **Step 6: Commit** — `feat: küçük adada çalan şarkı`

---

### Task 3: Boştaki adada hava durumu

**Files:**
- Create: `src/CamAda.Core/Weather/WeatherNow.cs`, `OpenMeteo.cs`, `WeatherText.cs`, `src/CamAda/Weather/WeatherService.cs`
- Modify: `src/CamAda.Core/Settings/AppSettings.cs`, `src/CamAda.Core/Settings/SettingsStore.cs`, `src/CamAda/Island/IslandWindow.xaml(.cs)`, `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Weather/WeatherTests.cs`, `tests/CamAda.Core.Tests/Settings/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace CamAda.Core.Settings;
  public sealed record WeatherLocation(string Name, double? Latitude, double? Longitude);
  // AppSettings: public WeatherLocation? Weather { get; init; }   (null: hava durumu gösterilmez)
  namespace CamAda.Core.Weather;
  public sealed record WeatherNow(double TemperatureC, int WeatherCode);
  public static class OpenMeteo
  {
      // https://api.open-meteo.com/v1/forecast?latitude=41.0082&longitude=28.9784&current=temperature_2m,weather_code (InvariantCulture, 4 ondalık)
      public static string ForecastUrl(double latitude, double longitude);
      // https://geocoding-api.open-meteo.com/v1/search?name=<Uri.EscapeDataString>&count=1&language=tr&format=json
      public static string GeocodeUrl(string name);
      public static WeatherNow? ParseForecast(string json);                             // current.temperature_2m, current.weather_code
      public static (double Latitude, double Longitude, string Name)? ParseGeocode(string json); // results[0]; sonuç yoksa null
  }
  public static class WeatherText
  {
      // WMO kodu: 0 "Açık"; 1–2 "Parçalı bulutlu"; 3 "Bulutlu"; 45, 48 "Sisli"; 51–57 "Çisenti"; 61–67 "Yağmurlu";
      // 71–77 "Karlı"; 80–82 "Sağanak"; 85–86 "Kar sağanağı"; 95–99 "Fırtına"; diğer "".
      public static string Describe(int code);
      public static string Temperature(double celsius);   // yuvarlanır: 12.4 → "12°", -2.6 → "-3°", -0.2 → "0°"
  }
  namespace CamAda.Weather;
  public sealed class WeatherService : IDisposable
  {
      public WeatherService(Func<WeatherLocation?> location, Action<WeatherLocation> saveResolved, Dispatcher dispatcher);
      public WeatherNow? Current { get; }        // en fazla 3 saatlik; daha eskiyse null
      public event EventHandler? Changed;        // UI iş parçacığında
      public void Start();                       // hemen ve 30 dk'da bir; uyanınca (PowerModes.Resume) hemen
  }
  ```

- [ ] **Step 1: Başarısız testleri yaz** (`WeatherTests`): `ForecastUrl(41.00824, 28.97836)` beklenen adresi verir (nokta ondalık); `GeocodeUrl("İzmir")` adı kaçırır; `ParseForecast` örnek JSON'dan `12.4, 3`, eksik alanda null, bozuk JSON'da null; `ParseGeocode` örnek JSON'dan koordinat ve ad, `"results"` yoksa null; `Describe(0) == "Açık"`, `Describe(63) == "Yağmurlu"`, `Describe(96) == "Fırtına"`, `Describe(4) == ""`; `Temperature(12.4) == "12°"`, `(-2.6) == "-3°"`, `(-0.2) == "0°"`. `SettingsStoreTests`: `Weather` yokken null; `{"Weather":{"Name":"İstanbul"}}` → ad korunur, koordinat null; enlem 95 → koordinatlar null'a çekilir (ad kalır).
- [ ] **Step 2:** Testlerin başarısız olduğunu gör.
- [ ] **Step 3:** Uygula; testler geçsin.
- [ ] **Step 4: Servisi ve görünümü bağla.** `WeatherService` tek bir paylaşılan `HttpClient` (10 sn zaman aşımı, kullanıcı aracısı `"CamAda"`) kullanır; koordinat yoksa önce coğrafi kodlar ve `saveResolved` ile ayara yazar. Hata (ağ yok, 5xx, ayrıştırma) son değeri korur, istisna sızdırmaz. Boştaki küçük ada: `"Pzt, 23 Kasım · 12° Bulutlu"` (hava yoksa yalnız tarih); metin sığmazsa önce durum adı düşer (`"Pzt, 23 Kasım · 12°"`).
- [ ] **Step 5: Elle doğrula:** ayara kullanıcının şehri yazılınca (kullanıcıya sorulur) ilk açılışta koordinatlar ayara eklenir ve boştaki adada sıcaklık ile durum görünür; internet kapalıyken açılış çökmez ve hava durumu yalnız gizlenir.
- [ ] **Step 6: Commit** — `feat: boştaki adada hava durumu`

---

### Task 4: Genişlemiş adada müzik bölümü ve Müzik paneli

**Files:**
- Create: `src/CamAda/Media/NowPlayingCard.xaml(.cs)`, `src/CamAda/Panels/MusicPanel.cs`
- Modify: `src/CamAda.Core/Island/IslandLayout.cs`, `src/CamAda/Island/IslandBar.xaml(.cs)`, `src/CamAda/Island/IslandWindow.xaml` (`Width="1350"`), `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Apps/DockOverflowTests.cs` (genişlik beklentileri)

**Interfaces:**
- Consumes: `NowPlayingService`, `MediaTimeline` (Task 1), `MiniRecord` (Task 2), `PanelHost` (Plan 2).
- Produces:
  ```csharp
  namespace CamAda.Core.Island;
  // IslandLayout: WindowWidth = 1350; MaxExpandedWidth = 1250; MusicSectionWidth = 150;
  // ExpandedWidthFor(n) = min(1250, 920 + max(0, n − 5) × 38)
  namespace CamAda.Media;
  // Müzik paneli ve plak kartının ortak içeriği. Parça yoksa boş durum: "Şu an bir şey çalmıyor" + "Spotify'ı aç".
  public sealed partial class NowPlayingCard : UserControl
  {
      public NowPlayingCard(NowPlayingService media, bool showCover, int queueCount);
      // Görev 8 bağlar: beğen düğmesi ve "Sıradaki" yalnız bu kaynak verildiğinde ve bağlıyken görünür.
      public ISpotifyExtras? Extras { get; set; }
  }
  public interface ISpotifyExtras
  {
      bool IsAvailable { get; }                                   // bağlı ve son istek başarılı
      event EventHandler? Changed;                                // UI iş parçacığında
      bool? IsCurrentSaved { get; }                               // bilinmiyorsa null (düğme gizli)
      IReadOnlyList<Core.Spotify.QueueItem> UpNext { get; }       // boş olabilir
      Task ToggleSavedAsync();
      Task RefreshAsync();                                        // kart/panel açılınca
  }
  namespace CamAda.Panels;
  public static class MusicPanel
  {
      public const double Width = 360;
      // footer: Görev 8'de SpotifyConnectView; bu görevde null.
      public static FrameworkElement Create(NowPlayingService media, ISpotifyExtras? extras, FrameworkElement? footer);
  }
  ```
  (`Core.Spotify.QueueItem` Görev 8'de tanımlanır; bu görevde `public sealed record QueueItem(string Title, string Artist);` olarak `src/CamAda.Core/Spotify/SpotifyJson.cs` içinde oluşturulur.)

- [ ] **Step 1: Başarısız testi yaz:** `DockOverflowTests` genişlik beklentileri: `ExpandedWidthFor(0) == 920`, `(5) == 920`, `(6) == 958`, `(11) == 1148`, `(30) == 1250`.
- [ ] **Step 2:** Testin başarısız olduğunu gör.
- [ ] **Step 3:** `IslandLayout`'u güncelle; `IslandWindow.xaml` genişliği 1350; testler geçsin.
- [ ] **Step 4: Müzik bölümü.** `IslandBar`'da uygulamalar ile `^` arasındaki ayırıcıdan sonra 150 DIP düğme: `MiniRecord` (26 DIP) + 8 DIP + şarkı adı (12,5 kalın, en fazla 104 DIP, kırpılır); parça yoksa soluk müzik notası glifi + "Spotify". `AutomationProperties.Name`: `"Müzik: <şarkı>, <sanatçı>"` ya da `"Müzik: çalmıyor"`. Tıklayınca `Panels.Toggle("music", () => MusicPanel.Create(media, extras, footer), MusicPanel.Width)`.
- [ ] **Step 5: `NowPlayingCard`.** Kapak (gösteriliyorsa 72 DIP, 12 köşe), şarkı (15 kalın), `"Sanatçı · Albüm"` (12 ikincil), beğen kalbi (sağ üst, kayıtlıysa `#FFC27A` dolu), ilerleme çubuğu (4 DIP, iz `#24FFFFFF`, dolgu `#FFC27A`) ve iki yanında süreler (tabular rakam), denetimler: önceki / oynat-duraklat (40 DIP `#FFC27A` daire, `#2A1A08` glif) / sonraki (Segoe Fluent Icons), "Sıradaki" başlığı altında `queueCount` satır `"Şarkı — Sanatçı"`. İlerleme ve süreler yalnız kart görünürken ve çalarken 1 sn'de bir güncellenir. Tüm düğmelerde `AutomationProperties.Name` ("Önceki şarkı", "Duraklat"/"Oynat", "Sonraki şarkı", "Beğen"/"Beğenmekten vazgeç", "Spotify'ı aç"). Panel `NowPlayingCard(media, showCover: true, queueCount: 3)` içerir.
- [ ] **Step 6: Elle doğrula** (deneme oturumuyla): genişlemiş adada müzik bölümünde dönen mini plak ve şarkı adı; tıklayınca Müzik paneli kapak, ad, ilerleme ve sürelerle açılır; oynat/duraklat, sonraki, önceki deneme oturumunu değiştirir ve küçük ada aynı anda güncellenir; deneme oturumu kapatılınca panel boş durumu ve "Spotify'ı aç" gösterir; 10 uygulama + "+N" ile ada 1250'yi aşmaz, satır kırpılmaz.
- [ ] **Step 7: Commit** — `feat: müzik bölümü ve Müzik paneli`

---

### Task 5: Plak çalar penceresi — konum, masaüstü katmanı, tıklama

**Files:**
- Create: `src/CamAda.Core/Settings/RecordSettings.cs`, `src/CamAda.Core/Record/RecordLayout.cs`, `RecordVisibility.cs`, `src/CamAda/Record/RecordWindow.xaml(.cs)`
- Modify: `src/CamAda.Core/Settings/AppSettings.cs` (`Record`), `src/CamAda.Core/Settings/SettingsStore.cs` (Sanitize), `src/CamAda/Interop/Native.cs`, `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Record/RecordLayoutTests.cs`, `tests/CamAda.Core.Tests/Settings/SettingsStoreTests.cs`

**Interfaces:**
- Consumes: `NowPlayingService` (Task 1), `RecordSpin` (Task 2), `WindowSample`, `Native.SampleTopLevelWindows` (Plan 1).
- Produces:
  ```csharp
  namespace CamAda.Core.Settings;
  public sealed record RecordSettings
  {
      public bool Enabled { get; init; } = true;
      public double Diameter { get; init; } = 320;          // 160–640
      public double OutsideFraction { get; init; } = 0.45;  // 0–0.8
      public double BottomOffset { get; init; } = 70;       // 0–600
      public double? LabelDiameter { get; init; }           // null: Diameter × 0.3375; 40 ile Diameter × 0.6 arası
      [JsonIgnore] public double EffectiveLabelDiameter { get; }   // null'ı çözer; ayar dosyasına yazılmaz
  }
  // AppSettings: public RecordSettings Record { get; init; } = new();
  namespace CamAda.Core.Record;
  // Tüm dikdörtgenler DIP; Disc ve Card pencereye göre göreli.
  public readonly record struct RecordGeometry(RectD Window, RectD Disc, RectD VisibleDisc, RectD Card);
  public static class RecordLayout
  {
      public const double CardWidth = 300;
      public const double CardHeight = 196;
      public const double CardGap = 16;
      public const double ShadowPad = 40;   // gölge ve hâle için pencere payı (sağ, üst, alt)
      // screen: ana ekran sınırları (DIP). Pencere ana ekranın sol kenarından başlar; disk x = −OutsideFraction × d (kırpılır);
      // disk alt kenarı = screen.Bottom − BottomOffset; kart görünen diskin sağında CardGap, dikeyde disk merkezine ortalı,
      // ekrandan taşarsa dikeyde içeri kaydırılır.
      public static RecordGeometry Compute(RectD screen, RecordSettings settings);
  }
  public static class RecordVisibility
  {
      // Görünen disk dikdörtgenini (ekran, fiziksel piksel) tamamen içine alan uygun bir pencere varsa true
      // (görünür, gizlenmemiş, yok sayılmayan, başlıklı, masaüstü değil, bizim değil).
      public static bool IsCovered(IEnumerable<Island.WindowSample> windows, RectD visibleDiscOnScreen);
  }
  namespace CamAda.Record;
  public sealed partial class RecordWindow : Window
  {
      public RecordWindow(NowPlayingService media, Func<RecordSettings> settings);
      public bool IsRecordVisible { get; }        // kapatılmamış ve örtülü değil
      public event EventHandler? VisibilityChanged;
      public ISpotifyExtras? Extras { get; set; } // Görev 7'de karta iletilir
  }
  ```

- [ ] **Step 1: Başarısız testleri yaz:**
  - `RecordLayoutTests` (ekran `0,0,1536,864` DIP, varsayılanlar): `Window.X == 0` (ana ekranın solu, eksi değil); `Disc == (-144, …, 320, 320)` (−0,45 × 320); `Disc` alt kenarı ekran koordinatında `864 − 70`; `VisibleDisc.Width == 176`; kart `X == 176 + 16`, kartın dikey ortası disk merkezinde; ekran `(-1920, 0, 1920, 1080)` gibi solda ikinci monitör senaryosunda bile `Window.X == screen.Left`; `BottomOffset 0` ve çap 640'ta kart ekrandan taşmaz (alt ≤ ekran altı); `OutsideFraction 0` → `Disc.X == 0`.
  - `RecordVisibility`: plağı tamamen içeren ekranı kaplayan pencere → true; plağın yarısını örten pencere → false; yalnız masaüstü ve bizim pencerelerimiz → false; gizli/cloaked pencere → false.
  - `SettingsStoreTests`: `Record` yokken varsayılanlar; çap 0 → 320; `OutsideFraction 1.5` → 0.45; `LabelDiameter 500` (çap 320) → null (varsayılan 108'e döner); `EffectiveLabelDiameter` 320'de 108.
- [ ] **Step 2:** Testlerin başarısız olduğunu gör.
- [ ] **Step 3:** Uygula; testler geçsin.
- [ ] **Step 4: Pencere.** `WindowStyle None`, `AllowsTransparency`, `ShowInTaskbar False`, `Topmost False`; `WS_EX_TOOLWINDOW`; `WM_WINDOWPOSCHANGING`'de `hwndInsertAfter = HWND_BOTTOM` ve `SWP_NOZORDER` kaldırılarak her zaman en altta tutulur (`Native.WINDOWPOS` mevcut). Ana ekranda `RecordLayout.Compute` ile yerleşir; `SystemEvents.DisplaySettingsChanged` ve DPI değişince yeniden yerleşir. Bu görevde disk geçici olarak düz `#0D0D10` daire + kapak etiketi + `RecordSpin` ile döner (gerçekçi çizim Görev 6). Sol tık ve Enter/Boşluk → `TogglePlayPauseAsync`. Disk odaklanabilir (`Focusable`, görünür odak halkası). Dönüş: `CompositionTarget.Rendering` yalnız `IsRecordVisible` iken ve (çalıyor ya da `IsMoving`) iken; `RecordVisibility` yoklaması 500 ms'de bir yalnız müzik çalarken. `Record.Enabled == false` ise pencere açılmaz.
- [ ] **Step 5: Masaüstü katmanı denemesi (karar adımı).** Ölç: (a) bir uygulama penceresi plağın üstüne gelince plak altta kalır; (b) plağa tıklamak onu öne getirmez; (c) Win+D sonrası plak görünür ve tıklanabilir; (d) Görev Görünümü/Alt+Tab'da plak yok. (c) başarısızsa sırasıyla dene: pencerenin sahibini masaüstü penceresi yap (`SetWindowLongPtr(GWLP_HWNDPARENT, GetShellWindow())`); o da yetmezse ön plan penceresi `Progman`/`WorkerW` olduğunda plağı geçici olarak `HWND_TOP`'a al, başka bir uygulama öne gelince yeniden `HWND_BOTTOM`. Seçilen yöntem ve ölçümler deftere ve spec §9'a bir satır olarak yazılır.
- [ ] **Step 6: Elle doğrula:** ekran görüntüsünde plak sol altta, yarısı ekran dışında, alt kenarı ekranın altından 70 DIP yukarıda; ana ekranın solundaki monitörde plağın parçası görünmez (kullanıcının monitör düzeni kontrol edilir, yoksa ekran ayarı değiştirilmeden atlanır); plağa tıklamak deneme oturumunu duraklatır/oynatır ve ada aynı anda güncellenir; ekranı kaplayan bir pencere açıkken müzik çalsa bile `CompositionTarget.Rendering` aboneliği yoktur (geçici izle doğrulanır, sonra silinir).
- [ ] **Step 7: Commit** — `feat: plak çalar penceresi`

---

### Task 6: Plağın gerçekçi çizimi

**Files:**
- Create: `src/CamAda.Core/Record/DiscTexture.cs`, `SheenTexture.cs`, `src/CamAda/Record/RecordVisual.xaml(.cs)`
- Modify: `src/CamAda/Record/RecordWindow.xaml(.cs)`
- Test: `tests/CamAda.Core.Tests/Record/TextureTests.cs`

**Interfaces:**
- Consumes: `RecordGeometry` (Task 5), `RecordSpin` (Task 2), `NowPlayingService.Cover/CoverColor` (Task 1).
- Produces:
  ```csharp
  namespace CamAda.Core.Record;
  public static class DiscTexture
  {
      // size × size BGRA32, önceden çarpılmış alfa. r = R'ye oranla bölgeler: r > 1 saydam; 0.985–1 kabarık kenar (daha açık, yumuşak
      // kenar yumuşatma); 0.94–0.985 giriş bölgesi (düz); 0.40–0.94 oluklu alan, 6 parça, parçalar arasında 0.006 genişliğinde düz bant;
      // 0.36–0.40 çıkış bölgesi (düz); r < 0.36 saydam (etiket ayrı katman). Oluk: yarıçapa bağlı ince parlaklık dalgalanması (periyot ~1,5 px),
      // düz bölgelerde yok; tohumlu gren ±4; taban renk #0D0D10. Aynı size ve seed aynı baytları üretir.
      public static byte[] Render(int size, int seed = 7);
  }
  public static class SheenTexture
  {
      // Dönmeyen yansıma: merkeze göre karşılıklı iki kama (eksenler 135° ve 315°, yarı açı 14°), r 0.38–0.99; kama ekseninde en parlak
      // (alfa 0.22), kenarlara yumuşak düşüş; yarıçapa göre ton 0°→300° kayan hafif gökkuşağı (doygunluk 0.25). Kama dışı alfa 0.
      public static byte[] Render(int size);
  }
  ```

- [ ] **Step 1: Başarısız testleri yaz** (`TextureTests`, `size = 256`): disk dışında (köşe) alfa 0; oluklu alanda (r = 0.7) alfa 255; etiket alanında (r = 0.2) alfa 0; aynı tohum aynı dizi, farklı tohum farklı dizi; bir yarıçap boyunca parlaklık değişimi (standart sapma) oluklu alanda düz banttan büyük; yansımada 135° ekseni üzerinde (r = 0.7) alfa > 40, 45° ekseninde alfa 0.
- [ ] **Step 2:** Testlerin başarısız olduğunu gör.
- [ ] **Step 3:** Uygula; testler geçsin.
- [ ] **Step 4: `RecordVisual`'ı kur** (alttan üste): masaüstüne düşen gölge (siyah daire, opaklık 0.5, bulanıklık 30, 8,12 DIP kaydırma); kapağın baskın renginden hâle (çapın 1,12 katı daire, `CoverColor`, bulanıklık 40, opaklık 0.35; renk değişince 0,6 sn geçiş); **dönen grup** (`BitmapCache`, `RenderTransform` yalnız açı): `DiscTexture` (fiziksel piksel boyutunda, DPI başına bir kez üretilip `WriteableBitmap` olarak önbelleklenir), kapak etiketi (daire kırpılmış; kapak yoksa `#1A1A22` düz etiket), etiket kenarında 1,5 DIP parlak ince halka, ortada 6 DIP mil deliği (etikete yazı eklenmez); **dönmeyen** katmanlar: `SheenTexture` ve etiket üstünde yumuşak parlama (sol üstten radyal, beyaz alfa 0.12). Şarkı değişince yeni kapak eskisinin üstünde 0,4 sn'de belirir. Durgun katmanlar her karede yeniden çizilmez; kare başına yalnız dönen grubun açısı değişir.
- [ ] **Step 5: Elle doğrula ve ölç:**
  1. Ekran görüntüsü (plağın 2× yakını) kullanıcıya gösterilir: oluklar, parçalar arası bantlar, kenar, etiket halkası, mil deliği, sabit kama yansıması ve gökkuşağı, gölge ve hâle görünür. Kullanıcı mockup'tan daha az gerçekçi bulursa doku parametreleri onunla birlikte ayarlanır.
  2. Dönerken yansıma yerinden kıpırdamaz, etiket ve disk birlikte döner (ardışık iki ekran görüntüsünde yansıma pikselleri aynı, etiket pikselleri farklı).
  3. Kapaksız parçaya (`--no-cover`) geçince düz koyu etiket ve varsayılan hâle; çökme yok.
  4. İşlemci: müzik çalarken ve plak görünürken CamAda 10 sn ortalaması ölçülür. %3'ün üstündeyse dönen grubun `Timeline.DesiredFrameRate` değeri 30'a indirilir ve yeniden ölçülür; sonuç deftere yazılır. Plak örtülüyken ve müzik dururken %0,5'in altında.
- [ ] **Step 6: Commit** — `feat: plağın gerçekçi çizimi`

---

### Task 7: Plağın müzik kartı

**Files:**
- Create: `src/CamAda/Record/RecordCard.cs`
- Modify: `src/CamAda/Record/RecordWindow.xaml(.cs)` (`WallpaperGlass` değişmeden kullanılır: plak penceresi kendi `WallpaperSource`'unu kurar, `Attach(recordWindow, card)`; kartın içinde adadaki gibi bir ton katmanı `Border`'ı bulunur)

**Interfaces:**
- Consumes: `NowPlayingCard` (Task 4), `RecordGeometry.Card` (Task 5), `HoverIntent` (Plan 1), `WallpaperGlass` (Plan 1).
- Produces: `public sealed class RecordCard : Border { public RecordCard(NowPlayingService media); public ISpotifyExtras? Extras { get; set; } }` — 300×196 DIP, köşe 24, kenar `#2BFFFFFF`, arka plan duvar kağıdı camı (kart masaüstünün üstünde durduğu için), içerik `NowPlayingCard(media, showCover: false, queueCount: 1)`.

- [ ] **Step 1: Kartı uygula.** Kart `RecordGeometry.Card` konumunda, başta gizli. `HoverIntent(TimeProvider.System, 100 ms, 300 ms)`: plağın görünen kısmına ve karta giriş/çıkış aynı niyete bildirilir (plaktan karta 16 DIP boşluğu geçerken kapanmaz; boşluğa alfa 1 dolgu konur ki fare olayları kesilmesin). Açılış 0,15 sn saydamlık + 8 DIP sağa kayma, kapanış 0,15 sn; "animasyonları azalt" açıksa anında. Plak klavye odağı alınca kart açılır, odak plaktan ve karttan çıkınca kapanır; kartın düğmeleri Tab ile gezilir. Kart açıkken plağa tıklamak yine çal/duraklat yapar. Kart açılınca `Extras?.RefreshAsync()`.
- [ ] **Step 2: Elle doğrula** (deneme oturumuyla, UIA + ekran görüntüsü): plağın üstüne gelince 0,1 sn civarında kart sağda açılır; karta geçerken kapanmaz; ikisinden de çıkınca ~0,3 sn sonra kapanır ve plak dönmeye devam eder; karttaki sonraki/önceki deneme oturumunu değiştirir; deneme oturumu kapalıyken kart "Şu an bir şey çalmıyor" ve "Spotify'ı aç" gösterir; kart duvar kağıdı camıyla çizilir ve köşelerde dikdörtgen taşma yoktur.
- [ ] **Step 3: Commit** — `feat: plağın müzik kartı`

---

### Task 8: Spotify'a bağlanma — beğen ve sıradaki şarkılar

**Files:**
- Create: `src/CamAda.Core/Spotify/SpotifyAuth.cs`, `SpotifyToken.cs`, `OAuthCallback.cs`, `SpotifyJson.cs` (Görev 4'teki `QueueItem` burada), `src/CamAda/Spotify/SpotifyClient.cs`, `SpotifyTokenStore.cs`, `LoopbackCallback.cs`, `SpotifyExtras.cs`, `SpotifyConnectView.xaml(.cs)`
- Modify: `src/CamAda/CamAda.csproj` (`System.Security.Cryptography.ProtectedData` 10.0.15), `src/CamAda.Core/Settings/AppSettings.cs` (`SpotifyClientId`), `src/CamAda.Core/Settings/SettingsStore.cs`, `src/CamAda/Panels/MusicPanel.cs`, `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Spotify/SpotifyTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace CamAda.Core.Spotify;
  public static class SpotifyAuth
  {
      public const string RedirectUri = "http://127.0.0.1:43821/callback";
      public const string Scopes = "user-library-read user-library-modify user-read-playback-state user-read-currently-playing";
      public static string CreateVerifier();                  // 64 karakter, [A-Za-z0-9-._~], RandomNumberGenerator
      public static string ChallengeFor(string verifier);     // base64url(SHA256(ASCII)), dolgusuz
      public static string CreateState();                     // 32 karakter rastgele
      // https://accounts.spotify.com/authorize?client_id=&response_type=code&redirect_uri=&code_challenge_method=S256&code_challenge=&scope=&state=
      public static string AuthorizeUrl(string clientId, string challenge, string state);
      public static IReadOnlyDictionary<string, string> CodeExchangeForm(string clientId, string code, string verifier); // grant_type=authorization_code
      public static IReadOnlyDictionary<string, string> RefreshForm(string clientId, string refreshToken);            // grant_type=refresh_token
  }
  public sealed record SpotifyToken(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)
  {
      public bool IsExpired(DateTimeOffset now);   // now ≥ ExpiresAt − 60 sn
      // access_token, expires_in; refresh_token yoksa previousRefreshToken; eksik/bozuk yanıtta null
      public static SpotifyToken? Parse(string json, DateTimeOffset now, string? previousRefreshToken);
  }
  public sealed record OAuthCallbackResult(string? Code, string? State, string? Error);
  public static class OAuthCallback
  {
      public static OAuthCallbackResult? Parse(string requestLine);   // "GET /callback?code=..&state=.. HTTP/1.1"; yol /callback değilse null
  }
  public sealed record QueueItem(string Title, string Artist);
  public static class SpotifyJson
  {
      public static (string Uri, string Title)? CurrentTrack(string json);   // item.type "track" ise item.uri ve item.name; yoksa null
      public static bool? Contains(string json);                              // "[true]" → true; bozuksa null
      public static IReadOnlyList<QueueItem> Queue(string json, int max);     // queue[i].name, artists[].name ", " ile
  }
  namespace CamAda.Spotify;
  public sealed class SpotifyClient   // ISpotifyExtras'ı besleyen HTTP katmanı
  {
      public SpotifyClient(Func<string?> clientId, SpotifyTokenStore store, TimeProvider time);
      public bool IsConfigured { get; }
      public bool IsConnected { get; }
      public Task<bool> ConnectAsync(CancellationToken cancellation);   // tarayıcı + LoopbackCallback (2 dk), state doğrulanır
      public Task<(string Uri, string Title)?> CurrentTrackAsync();
      public Task<bool?> IsSavedAsync(string uri);
      public Task<bool> SetSavedAsync(string uri, bool saved);
      public Task<IReadOnlyList<QueueItem>> QueueAsync(int max);
  }
  public sealed class SpotifyExtras : Media.ISpotifyExtras
  {
      public SpotifyExtras(SpotifyClient client, NowPlayingService media, Dispatcher dispatcher);
  }
  public sealed partial class SpotifyConnectView : UserControl
  {
      // saveClientId: Client ID'yi ayara yazar; bağlanınca Connected olayı (görünüm gizlenir, Extras yenilenir).
      public SpotifyConnectView(SpotifyClient client, Action<string> saveClientId);
      public event EventHandler? Connected;
  }
  ```

- [ ] **Step 1: Kullanıcı adımı (kontrol noktası).** Kullanıcıdan Spotify geliştirici panelinde (`https://developer.spotify.com/dashboard`) bir uygulama oluşturmasını, "Web API"yi seçmesini, yönlendirme adresi olarak `http://127.0.0.1:43821/callback` eklemesini ve Client ID'yi paylaşmasını iste. Client ID gelmeden Step 6'ya geçilmez; Step 2–5 beklemeden yapılır.
- [ ] **Step 2: Başarısız testleri yaz** (`SpotifyTests`):
  - `CreateVerifier()` 64 karakter ve yalnız izinli karakterler; RFC 7636 örneği: `ChallengeFor("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk") == "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"`.
  - `AuthorizeUrl` `response_type=code`, `code_challenge_method=S256`, kaçırılmış `redirect_uri` (`http%3A%2F%2F127.0.0.1%3A43821%2Fcallback`) ve kapsamları içerir.
  - `SpotifyToken.Parse`: `expires_in 3600` → `ExpiresAt = now + 1 sa`; `refresh_token` yoksa önceki korunur; `IsExpired` bitişe 59 sn kala true, 61 sn kala false; bozuk JSON → null.
  - `OAuthCallback.Parse("GET /callback?code=abc&state=xyz HTTP/1.1")` → code `abc`, state `xyz`; `?error=access_denied&state=xyz` → Error; `GET /favicon.ico HTTP/1.1` → null.
  - `SpotifyJson`: currently-playing örneğinden uri `spotify:track:…` ve ad; `item` null ya da `"type":"episode"` → null; `Contains("[true]") == true`, `"[false]"` → false, `"x"` → null; kuyruk örneğinden en fazla `max` öğe, sanatçılar `"A, B"`.
  - `SettingsStoreTests`: `SpotifyClientId` boşluklarıyla yazılmışsa kırpılır; boş metin null olur.
- [ ] **Step 3:** Testlerin başarısız olduğunu gör.
- [ ] **Step 4:** Core'u uygula; testler geçsin.
- [ ] **Step 5: İstemciyi uygula.** `SpotifyTokenStore`: `%LOCALAPPDATA%\CamAda\spotify-token.bin`, `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)`; çözülemezse silinir. `LoopbackCallback`: `TcpListener(IPAddress.Loopback, 43821)`, tek istek okur, `OAuthCallback.Parse`, tarayıcıya UTF-8 HTML yanıtı `"CamAda Spotify'a bağlandı. Bu sekmeyi kapatabilirsin."` (hata varsa `"Bağlanılamadı: <hata>"`) döner, kapanır; port meşgulse `ConnectAsync` false. `SpotifyClient`: istekten önce token süresi dolmuşsa yeniler; 401'de bir kez yenileyip tekrar dener; yenileme 400/401 dönerse token silinir (`IsConnected` false); 429'da `Retry-After` süresince yeni istek atmaz ve null/boş döner; ağ hatası null/boş. `SpotifyExtras : ISpotifyExtras`: `NowPlayingService.Changed`'de parça başlığı değişince 1 sn bekleyip `CurrentTrackAsync`; dönen ad medya oturumundaki başlıkla eşleşmezse 2 sn sonra bir kez daha dener; eşleşince `IsSavedAsync`. `ToggleSavedAsync` iyimser günceller, başarısızsa geri alır. Bağlı değilken hiçbir istek atılmaz.
- [ ] **Step 6: Uç nokta denemesi (spec §9 risk 4, karar adımı).** Kullanıcının Client ID'siyle bağlan (tarayıcıda Spotify izin ekranını kullanıcı onaylar). Gerçek Spotify'da çalan bir şarkı için sırayla: `currently-playing` uri döner; `library/contains` `[false]`/`[true]` döner; `PUT /me/library` ardından `contains` `[true]`, `DELETE` ardından `[false]` (şarkının başlangıçtaki beğeni durumu en sonda geri yüklenir); `queue` en az bir öğe döner. Her yanıtın durum kodu deftere yazılır. Bir uç nokta kullanılamıyorsa ilgili kontrol (`IsCurrentSaved` ya da `UpNext`) kalıcı olarak gizli kalır ve spec §9'a not düşülür.
- [ ] **Step 7: Bağlantı arayüzü.** Müzik panelinin altında `SpotifyConnectView`: Client ID yoksa "Beğen ve Sıradaki için Spotify'a bağlan" + "Bağla"; tıklanınca üç adım: (1) "Spotify geliştirici panelinde bir uygulama oluştur" + "Paneli aç", (2) "Yönlendirme adresi olarak ekle: http://127.0.0.1:43821/callback" + "Kopyala", (3) Client ID kutusu + "Bağlan" (Client ID ayara yazılır, `ConnectAsync`). Client ID var ama bağlı değilse yalnız "Spotify'a bağlan". Bağlıyken görünüm gizlidir. `NowPlayingCard.Extras` panelde ve plak kartında `SpotifyExtras`'a bağlanır.
- [ ] **Step 8: Elle doğrula** (gerçek Spotify, kullanıcıyla): panelde ve plak kartında kalp şarkının gerçek beğeni durumunu gösterir; kalbe basınca Spotify uygulamasında da "Beğenilenler"e eklenir/çıkar (sonra eski hâline getirilir); "Sıradaki" Spotify'daki sıradaki şarkıyı gösterir; internet kapatılınca kalp ve "Sıradaki" gizlenir, oynat/duraklat çalışmaya devam eder; CamAda yeniden başlatılınca yeniden giriş istenmez.
- [ ] **Step 9: Commit** — `feat: Spotify beğen ve sıradaki şarkılar`

---

## Plan 3 bittiğinde

- `dotnet test` tamamı geçer; derleme uyarısız; `tools/install.ps1` ile kurulur.
- Spotify çalarken küçük adada dönen mini plak, şarkı ve ekolayzır; boştayken tarih, hava durumu ve saat. Genişlemiş adada müzik bölümü ve Müzik paneli (kapak, ilerleme, denetimler, beğen, sıradaki).
- Sol altta yarı gömülü, gerçekçi, 33⅓ devirde dönen, yavaşlayarak duran plak; üzerine gelince müzik kartı; tıklayınca çal/duraklat; pencerelerin arkasında kalır, Win+D'de görünür.
- Müzik çalmıyorken ya da plak görünmüyorken işlemci ~%0.
