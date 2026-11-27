# İngilizce arayüz (1. adım) — tasarım

Tarih: 27.11.2026 · Boyut: proje · Sonraki adım (ayrı tasarım): Ada'nın İngilizce konuşması

## Amaç ve neden

Camada açık kaynak oldu ve GitHub sayfası İngilizce; Store'da da global yayın hedefleniyor. Şu an arayüz yalnız Türkçe, bu yüzden Türkçe bilmeyen biri uygulamayı kullanamıyor. Bu adımın sonunda Türkçe bilmeyen biri Camada'yı kurup her menüyü, paneli ve ayarı İngilizce görür; Türkçe kullanıcının deneyimi hiç değişmez. Ada'nın konuşması (talimatı, araç cevapları, uygulama adı eşleştirmesi) ikinci adımdır; bu adımda Ada Türkçe konuşmaya devam eder.

## Hedefler

- Ekranda görünen ve ekran okuyucunun okuduğu her metin iki dilde: ada ve sağ tık menüsü, bütün paneller (hızlı ayarlar, ses ve mikrofon, pano, Wi‑Fi/Bluetooth listeleri, takvim, tepsi, başlat/arama, pencereler), Spotify bağlama ekranı ve tarayıcıda açılan dönüş sayfası, Ada panelinin ayar ekranı, adada görünen Ada durum ve hata satırları, MSIX'in Başlat menüsü girişleri.
- Dil Windows'un görüntü dilinden seçilir (Türkçe → Türkçe, diğer her dil → İngilizce); sağ tık menüsündeki **"Dil / Language"** alt menüsüyle elle değiştirilir.
- Yeni bir dil eklemek yalnız bir çeviri dosyası eklemek olur.

## Hedef olmayanlar

- Ada'nın konuşması: `AdaInstruction`, `AdaTools`, `AdaToolRunner`'ın modele giden metinleri, `AppAliases`, `NameMatch`, `SensitiveText`, `MemoryBook`, `Reminders`'ın konuşma metinleri, `CurrentInfoClient`'ın istemi, `OpenMeteo`'nun Ada cevap metinleri (`WeatherAnswer`) — 2. adım.
- Geliştirici araçları: `AdaProbes*` deneme komutları, `tools/*.ps1` çıktıları (kurtarma kısayolunun adı dahil).
- Üçüncü bir dilin çevirisi; çalışırken (yeniden başlatmadan) dil değiştirme.

## Olmaması gerekenler (çalışsa bile başarısızlık)

- Türkçe arayüzde bir metnin istemeden değişmesi ya da kaybolması.
- Dil dosyası eksik/bozuk olduğunda uygulamanın açılmaması ya da anahtar adı göstermesi (her zaman İngilizceye düşmeli).
- Dil değiştirince görev çubuğunun gizli, adanın kapalı kalması.
- Güvenlik ağı metinlerinin ("Görev çubuğunu gizle", "Görev çubuğunu geri getir", "Çıkış") bir dilde anlaşılmaz olması.
- Ada'nın Türkçe konuşmaya devam ettiğinin İngilizce kullanıcıdan saklanması.

## Kısıtlar

- .NET 10, WPF; Core `net10.0` (Windows'a bağlı değil, xUnit ile test edilir), UI `net10.0-windows10.0.22621.0`.
- Store paketi (MSIX, kendi içinde .NET) `tools/package.ps1` ile üretiliyor; Türkçe uydu derlemesi pakete girmeli.
- Proje kuralları (CONTRIBUTING): güveni zedeleyen kurulum adımı yok; anahtar/kişisel veri commit edilmez.
- Bugün 825 test geçiyor; Ada testleri Türkçe metin bekliyor ve değişmemeli.
- `App.Restart()` (`src/CamAda/App.xaml.cs`) var; görev çubuğu ayarı değişince kullanılıyor (paketliyken `PackagedActivation` ile).

## Yaklaşımlar

| | Yol | Artısı | Eksisi |
|---|---|---|---|
| **A (seçilen)** | .NET kaynak dosyaları (`.resx`), İngilizce temel dil + `tr` uydu derlemesi, derleme zamanında üretilen tipli sınıf | Standart; eksik anahtar derleme hatası; çeviri platformları (Weblate, Crowdin) doğrudan destekler; WPF'te `{x:Static}` ile bağlanır | `.resx` XML'i elle düzenlemesi zahmetli |
| B | Çalışırken okunan JSON dil dosyaları | Çevirmen için çok kolay | Kendi yükleyicimiz; derleme zamanı güvencesi yok; yanlış anahtar çalışırken çıkar |
| C | C# sözlükleri | En basit | Çevirmen dostu değil, büyüdükçe dağılır |

Kullanıcı "nasıl" kısmını bana bıraktı; A'yı seçtim. Dil değişince **yeniden başlatma** seçildi: WPF'te `{x:Static}` bağları çalışırken yenilenmez, yeniden başlatma mekanizması zaten var.

## Kararlar

### Kullanıcının kararları
- İngilizce şimdi, iki adımda: önce arayüz (bu belge), sonra Ada.
- Dil Windows'un diline göre, menüden elle değiştirilebilir; dil değişince uygulama kendini yeniden başlatır.
- Saat: Türkçe arayüzde her zaman 24 saat (`HH:mm`, bugünkü gibi); İngilizce arayüzde Windows'un bölge ayarı. (Gerekçe: kullanıcının kendi Windows'u Türkçe görüntü dili + ABD bölge biçimiyle çalışıyor; "bölge ayarına uy" onun saatini 2:12 PM'e çevirirdi.)
- İngilizce metinleri Claude yazar; Türkçe metinler bugünkü haliyle kalır.

### Dil seçimi
- `AppSettings.Language : string?` — `null` (otomatik), `"tr"`, `"en"`. Sürüm göçü gerekmez (yeni alan, varsayılan `null`); tanınmayan değer `null` sayılır.
- Core'da saf fonksiyon: `UiLanguage.Resolve(string? setting, CultureInfo windowsUiCulture) -> CultureInfo` — `"tr"` → `tr-TR`, `"en"` → `en-US`, `null`: Windows görüntü dili Türkçe ise `tr-TR`, değilse `en-US`.
- `App` kurucusunda, olay döngüsü başlamadan ve eşzamanlı: `CultureInfo.DefaultThreadCurrentUICulture` ve `CurrentUICulture` = sonuç; `CurrentCulture` (bölge biçimi) Windows'ta bırakılır. Dil `SettingsStore.ReadLanguage()` ile okunur (dosyaya yazmaz; bekçi ve kurtarma kiplerinde de çalışır). Async başlangıçta ayarlanmaz: `CurrentUICulture` async akışla taşınır, ilk `await`'te geri alınır ve WPF olay döngüsü Windows dilinde kalır (canlı denemede yaşandı: yalnız başlangıçta kurulan menü çevrildi, paneller Türkçe kaldı).
- Sağ tık menüsüne alt menü: başlığı her zaman iki dilli **"Dil / Language"** (yanlış dil seçen geri dönebilsin), içinde "Otomatik (Windows)" / "Automatic (Windows)", "Türkçe", "English"; seçili olan işaretli. Seçince ayar kaydedilir ve `Restart()`.

### Kaynak dosyaları
- Core: `src/CamAda.Core/Text/CoreStrings.resx` (İngilizce, temel) + `CoreStrings.tr.resx`; UI: `src/CamAda/Text/UiStrings.resx` + `UiStrings.tr.resx`. Tipli sınıf MSBuild ile üretilir (`StronglyTypedLanguage`, `StronglyTypedClassName`, `StronglyTypedNamespace`, `StronglyTypedFileName` meta verileri; `public`). Her iki projede `<NeutralLanguage>en</NeutralLanguage>`.
- Anahtar adları: `Alan_Öğe` (ör. `Menu_HideTaskbar`, `QuickSettings_Title`, `Battery_Charging`). Biçimli metinler `{0}` yer tutucusuyla; yüzde gibi dilden dile sırası değişenler kaynakta (`"%{0}"` / `"{0}%"`).
- Sayıya göre değişen metinler (İngilizcede tekil/çoğul): `_One` / `_Other` anahtar çifti ve tek bir yardımcı `Plural.Pick(int n, string one, string other)`; Türkçede ikisi aynı olabilir.
- XAML: `xmlns:t="clr-namespace:CamAda.Text"` + `{x:Static t:UiStrings.Anahtar}`; arka kod: `UiStrings.Anahtar`, biçimde `string.Format(CultureInfo.CurrentCulture, …)`.
- Ekran okuyucu adları (`AutomationProperties`, bugün ~72 yer) de kaynaktan gelir.

### Biçimler ve kurallar
- Saat: `ClockFormatter.Time(DateTime local, CultureInfo ui, CultureInfo region)` — `ui` Türkçe ise `"HH:mm"`; değilse `region`'ın kısa saat deseni (`"t"`; Windows'ta kullanıcının kendi 12/24 saat tercihi yansır). Test: tr arayüz + en-US bölge → "14:12"; en arayüz + en-US bölge → "2:12 PM"; en arayüz + en-GB bölge → "14:12".
- Ay/gün adı içeren tarihler (adadaki tarih, takvim başlığı, pano "12 Kas"): desen kaynaktan (`Format_IslandDate` = tr `"ddd, d MMMM"` / en `"ddd, MMMM d"` …), ad dili UI kültürü.
- Core'daki metin üreten fonksiyonlar kültürü parametre olarak alır (gizli küresel durum yok), böylece iki dilde test edilir; UI bunları `CultureInfo.CurrentUICulture` ile çağırır.
- Arama/sıralama/eşleştirme (`SearchRanker`, Wi‑Fi ad sıralaması, `AppVolumeGrouping`): UI kültürü (Türkçede i/İ kuralı korunur). Ada dosyalarındaki `tr-TR` sabitleri 2. adıma kadar dokunulmaz.
- Windows ayar sayfaları araması (`SettingsPages`): görünen ad UI dilinde, arama terimleri iki dilin birleşimi (hangi dilde yazılsa bulunur).
- Hava durumu: WMO açıklamaları Core kaynağına taşınır; adadaki satır UI dilinde, Ada'nın `WeatherAnswer` cevabı açıkça `tr-TR` ile çağrılmaya devam eder (Ada testleri değişmez).
- Ekran görüntüsü dosya adı ve Spotify dönüş sayfası UI dilinde.

### Ada bu adımda
- Ada panelinin ayar ekranı, adadaki durum/hata satırları ("Dinliyorum…", bağlantı hataları) çevrilir: ekranda görünen = 1. adım, modele giden = 2. adım.
- İngilizce arayüzde Ada panelinde tek satır not: "Ada speaks Turkish for now — English is coming." (Türkçe arayüzde görünmez.) **Plan 12 ile kalktı:** Ada artık arayüz dilinde açılıyor (`2026-11-27-ada-arayuz-dili-design.md`).

### Paket (MSIX)
- Başlat menüsü girişleri ve paket açıklaması yerelleştirilir: `packaging/Strings/en-US/Resources.resw` ve `packaging/Strings/tr-TR/Resources.resw`; manifestte `ms-resource:` başvuruları ("Görev çubuğunu geri getir" / "Restore taskbar"); `tools/package.ps1` `makepri` ile `resources.pri` üretir; varsayılan dil `en-US`. Sürüm 1.1.0.
- `tr` uydu derlemelerinin yayım çıktısında ve pakette olduğu doğrulanır.

### Testler ve korumalar
- `UiLanguageTests`: ayar × Windows dili tablosu (null/tr/en/bozuk × tr-TR/en-US/de-DE).
- `ResourceParityTests`: iki `.resx` çiftinde anahtar kümeleri eşit, her anahtarın `{n}` yer tutucuları aynı, boş değer yok (hem Core hem UI dosyaları; test dosyaları depodan okur).
- `NoHardcodedTurkishTests`: `src/CamAda` ve `src/CamAda.Core` altındaki `.cs`/`.xaml` dosyalarında Türkçe harf içeren dize sabiti kalmaz; izin listesi: 2. adım dosyaları (yukarıda), `AdaProbes*`, yorumlar ve `///` belgeleri, `.resx`. Bugünkü durumda kırmızı başlar, iş bitince yeşil olur.
- Metin üreten Core fonksiyonları (pil, pano yaşı, takvim başlığı, saat/tarih, hava açıklaması, yüzde) her iki kültürle test edilir; mevcut Türkçe beklentiler `tr-TR` ile çağrılarak korunur.
- Elle canlı deneme (kullanıcıyla): dil "English" → yeniden başlatma → her panel; "Türkçe" → bugünküyle aynı; "Otomatik"; görev çubuğu gizliyken dil değiştirme (görev çubuğu ve ada yerinde kalmalı); paketli deneme (Başlat menüsü girişi İngilizce/Türkçe).

### Yayın
- README (EN/TR): "UI: English and Turkish; Ada speaks Turkish for now." (Plan 12 ile: Ada arayüz dilinde konuşur.)
- `packaging/store-listing.md`: İngilizce liste metni (kısa açıklama, açıklama, özellikler, arama terimleri); Partner Center'da İngilizce listeyi kullanıcı ekler.

## Geliştiriciye kalanlar

- Her dosyada hangi dizenin "ekranda görünen" (1. adım) hangisinin "modele giden" (2. adım) olduğunu ayırmak; şüpheliyi ekranda görünen say.
- Anahtar adlarının tamamı, İngilizce metinlerin kelimesi kelimesine hali (kısa, arayüz dili; Windows'un kendi İngilizce adlarıyla tutarlı: "Quick settings", "Do not disturb", "Clipboard"…).
- `.resx` tipli sınıf üretiminin MSBuild ayrıntısı; `makepri` çağrısının ayrıntısı.
- Görevlerin sırası ve büyüklüğü (öneri: altyapı + dil seçimi + korumalar → Core metinleri → ada/menü → paneller → Ada paneli/durumları → paket + yayın).

## Claude'un kendi verdiği kararlar (kullanıcı okurken kontrol etsin)

1. Temel dil İngilizce; Türkçe uydu. Türkçe Windows dışındaki her dil İngilizce açılır.
2. Alt menü başlığı her zaman "Dil / Language".
3. Ay/gün adları ve tarih deseni arayüz diline uyar (saat kuralı kullanıcının kararı, yukarıda).
8. Var olan kurulumlar için göç yok: dil Windows'un görüntü dilinden gelir (kullanıcının Windows'u `tr-TR` → Türkçe kalır). "Windows görüntü dili" = başlangıçta, uygulama değiştirmeden önceki `CultureInfo.CurrentUICulture`.
9. `ResourceParityTests` ve `NoHardcodedTurkishTests` depo kökünü test derlemesinin klasöründen yukarı `CamAda.slnx` arayarak bulur.
4. Windows ayar araması iki dilde birden arar.
5. İngilizce arayüzde Ada panelinde "Ada speaks Turkish for now" notu.
6. Başlat menüsü girişleri de yerelleştirilir; sürüm 1.1.0.
7. Kurtarma kısayolu adı ve geliştirici betikleri bu adımda Türkçe kalır.
