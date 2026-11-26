# Plan 10 — Store paketi ve güvenli kaldırma (tasarım)

Tarih: 2026-11-26 · Durum: kullanıcı onayı bekliyor · Dal: `plan-10-store-paketi`

## 1. Amaç ve neden

CamAda ürün olarak Microsoft Store'dan dağıtılacak (karar: 26.11, ayrıntı hafızada "yayın stratejisi"). Store, paketi
Microsoft'un kendisi imzaladığı için kullanıcı SmartScreen uyarısı görmez; sertifika satın almak, güven deposuna
sertifika eklemek ya da geliştirici modu istemek gerekmez. Bu, projenin "güveni zedeleyen kurulum adımı yok" kuralıyla
birebir örtüşür. Bu plan, bugün `tools/install.ps1` ile kurulan uygulamayı Store'a gönderilebilir bir MSIX paketine
çevirir ve paketli uygulamanın kurulum, açılışta başlama, kurtarma ve kaldırma davranışlarını güvenli hâle getirir.

Bu iş, yayına hazırlığın beş işinden ilkidir (sonrakiler: ada kısayolları + ilk açılış ekranı, Spotify/hava, Ada beta
sunucusu, gizlilik/lisans/site; ardından Store'un gizli yayınıyla kapalı beta, sonra İngilizce).

## 2. Hedefler

1. `tools/package.ps1` tek komutla Store'a yüklenebilir bir `.msix` üretir (x64, .NET içinde).
2. Paketli uygulama bugünkü özelliklerin tamamıyla çalışır (ada, paneller, pano, ekran görüntüsü, hızlı ayarlar, Ada).
3. Açılışta başlama paketin kendi yöntemiyle (StartupTask) çalışır ve ayardaki anahtarla açılıp kapanır.
4. Başlat menüsünde "Görev çubuğunu geri getir" girişi vardır ve çalışır.
5. Görev çubuğu gizliyken uygulama kaldırılır ya da güncellenirse görev çubuğu geri gelir; güncellemeden sonra
   CamAda kendiliğinden yeniden açılır.
6. Paket, Store'a gizli (yalnız kullanıcının kendi hesabına görünen) olarak gönderilir ve sertifikasyondan geçer; gerçek
   Store kurulumunda doğrulama listesi (§8) yapılır.

## 3. Hedef olmayanlar

- İlk açılış ekranı, ada kısayollarının seçilebilmesi (2. iş).
- Spotify'ın varsayılan yolunun değişmesi, hava servisinin değişmesi (3. iş).
- Ada sunucusu, abonelik, Store'dan satın alma (4. iş ve sonrası).
- Gerçek gizlilik politikası metni ve tanıtım sitesi (5. iş); bu planda yalnız geçici bir gizlilik sayfası.
- İngilizce arayüz, ARM64 paketi, paket boyutunu küçültme çalışmaları.
- `tools/install.ps1` geliştirme kurulumunun kaldırılması (geliştirme için kalır).

## 4. Başarısızlık sayılacaklar (anti-hedefler)

- Kurulumun herhangi bir yerinde sertifika, güven deposu, geliştirici modu ya da yönetici izni gerekmesi.
- Uygulama kaldırıldıktan ya da güncellendikten sonra kullanıcının görev çubuğunun gizli kalması.
- Paketli sürümün sessizce çökmesi (örn. eksik bir derleme kütüphanesi yüzünden), Ada'nın mikrofonsuz kalması.
- Kaldırmadan sonra gerçek `HKCU\...\Run` altında var olmayan bir yolu gösteren değer kalması.
- İlk açılışta kullanıcının hiç dokunmadığı bir özellik yüzünden izin penceresi (konum vb.) çıkması.

## 5. Kısıtlar

- Store bireysel hesap: açıldı (26.11). Paket kimliği (`Identity Name`, `Publisher`, `PublisherDisplayName`)
  Partner Center'da ayrılan uygulama adından gelir; manifestte bu değerler derleme sırasında yerleştirilir.
- Store sürüm numarası `A.B.C.0` olmalı (dördüncü hane 0).
- Paket çıktısı `%TEMP%` altında tutulmaz: 26.11 denemesinde yeniden başlatma sırasında Windows geçici klasördeki .NET
  dosyalarını sildi. Çıktı depoda `out/` (git dışı).
- Bu bilgisayarda Windows Sandbox kurulamıyor (bileşen deposunda onarılamayan bir bozuk dosya); yerel paket denemesi
  geliştirici modunu yalnız deneme süresince açıp kapatarak yapılır. Asıl doğrulama Store'un gizli yayınıyla.
- Mikrofon açan otomatik deneme yok (mevcut kural): Ada'nın paketli mikrofon izni kullanıcıyla elle denenir.

## 6. Yaklaşımlar

| Yol | Ne | Neden seçilmedi / seçildi |
|---|---|---|
| **A. MSIX, .NET içinde, Store imzalı** | `dotnet publish --self-contained` + manifest + `makeappx` | **Seçildi.** Store imzalar, uyarı yok, sertifika yok; 26.11 denemesinde çalıştı. |
| B. Store'a EXE/MSI kurulum dosyası | Kendi kurulum dosyamızı Store'a göndermek | Kendi kod imzalama sertifikamızı istiyor; Türkiye'deki bireysel geliştirici için Azure Artifact Signing yok, EV sertifika pahalı ve uyarıyı hemen kaldırmıyor. |
| C. MSIX, .NET ayrı kurulur | Framework'e bağımlı paket | Store'da .NET 10 masaüstü çalışma zamanı paket bağımlılığı olarak verilemiyor; kullanıcı "önce .NET kurun" uyarısı görür (26.11'de görüldü). |

Paketleme aracı olarak Visual Studio'nun paketleme projesi (`.wapproj`) yerine kendi betiğimiz: depoda Visual Studio
yok, betik denemede kanıtlandı ve neyin pakete girdiği açıkça görülüyor.

## 7. Tasarım

### 7.1 26.11 denemesinin bulguları (gevşek kayıtla, klasörden)

- Kaldırma, görev çubuğu gizliyken bile onu 10 sn içinde geri getirdi; geride süreç kalmadı; otomatik gizleme durumu
  değişmedi.
- `uap10:Parameters="--restore-taskbar"` ile ikinci Başlat girişi çalıştı: ada kapandı, görev çubuğu geri geldi.
- `windows.startupTask` ilk açılıştan sonra açık (State=2) kaydoldu.
- Uygulamanın `HKCU\...\Run` yazımı paket içinde sanallaşıyor: gerçek değer değişmedi → pakette Run anahtarı etkisiz.
- `%LOCALAPPDATA%\CamAda` ve `%APPDATA%\CamAda` yazımları `Packages\<PFN>\LocalCache\{Local,Roaming}` altına gidiyor;
  kaldırınca siliniyor. Belgeler/Resimler altındaki kullanıcı dosyaları (Ada Notları, Ekran Görüntüleri) gerçek yerde.
- Bekçi süreç (`--watchdog`, ebeveyni Explorer) paket içinde başladı.
- Hata 1: .NET içinde yayımda ManagedShell `System.Windows.Forms` istiyor; pakete yalnız WPF girdiği için açılışta
  çöktü.
- Hata 2: `WiFiAdapter.RequestAccessAsync` paketli uygulamada açılışta "tam konumunuza erişilsin mi?" penceresi açtı.
- sherpa-onnx / onnxruntime Visual C++ çalışma zamanı içe aktarmıyor (VCLibs bağımlılığı gerekmez).
- Paket: 226 MB açık, 107 MB `.msix`.

### 7.2 Paket yapısı

`packaging/` klasörü (yeni):

- `Package.appxmanifest` — şablon; derlemede `{Name}`, `{Publisher}`, `{PublisherDisplayName}`, `{Version}`
  yerleştirilir. Değerler `packaging/store-identity.json` dosyasından okunur (Partner Center'daki "Ürün kimliği"
  sayfasındaki değerler; gizli değildir, depoya girer). Dosya yoksa yerel denemeler için `CamAda.Dev` /
  `CN=CamAdaDev` yer tutucusu kullanılır ve betik "bu paket Store'a yüklenemez" uyarısı verir.
- `Assets/` — Store'un istediği simgeler (44, 150, 50 ve geniş/büyük karo boyutları). İlk sürümde mevcut cam ada
  görselinden üretilmiş sade bir simge; son simge tasarımı 5. işte.
- Uygulama girişleri:
  - `CamAda` — ana giriş; `desktop:StartupTask TaskId="CamAdaStartup" Enabled="true"`.
  - `Kurtar` — "Görev çubuğunu geri getir", `uap10:Parameters="--restore-taskbar"`.
- Yetenekler: `rescap:runFullTrust`, `microphone`, `location`, `wiFiControl`, `bluetooth`, `radios`
  (kullanılan WinRT API'leri: `Windows.Devices.WiFi`, `.Bluetooth`, `.Radios`, `.Enumeration`). Gereksiz çıkan
  yetenek doğrulamada çıkarılır.
- `TargetDeviceFamily Windows.Desktop MinVersion 10.0.22621.0` (proje hedefiyle aynı).
- Dil: `tr-TR` (İngilizce 6. işte eklenecek).

### 7.3 Derleme betiği `tools/package.ps1`

1. Sürümü `src/CamAda/CamAda.csproj` içindeki `<Version>` özelliğinden okur (yeni; `1.0.0` ile başlar) →
   manifestte `1.0.0.0`.
2. `dotnet publish src/CamAda -c Release -r win-x64 --self-contained true -o out/msix/layout`.
3. `.pdb` dosyalarını katmandan çıkarır; manifest şablonunu doldurur, `Assets`'i kopyalar.
4. Windows SDK'daki `makeappx.exe` ile `out/msix/CamAda_<sürüm>_x64.msix` üretir (SDK yolu bulunamazsa açık hata).
5. `-Register` anahtarı (yalnız geliştirici için): katmanı gevşek kayıtla kurar; geliştirici modu kapalıysa bunu
   söyleyip durur (kendisi açmaz).

`out/` `.gitignore`'a eklenir.

### 7.4 Proje değişiklikleri

- `CamAda.csproj`: `<UseWindowsForms>true</UseWindowsForms>` ve otomatik eklenen ad alanlarından
  `<Using Remove="System.Windows.Forms" />`, `<Using Remove="System.Drawing" />` (denemede ad çakışmalarını böyle
  çözdük). `<Version>1.0.0</Version>`.
- Paketli mi algısı: `PackageContext.IsPackaged` (`GetCurrentPackageFullName` → `APPMODEL_ERROR_NO_PACKAGE`
  dışındaysa paketli). Saf karar kodu `CamAda.Core`'da test edilir.

### 7.5 Açılışta başlama

- Paketli: `Windows.ApplicationModel.StartupTask.GetAsync("CamAdaStartup")`. Ayar açıkken durum `Disabled` ise
  `RequestEnableAsync()`; ayar kapalıysa `Disable()`. Durum `DisabledByUser` / `DisabledByPolicy` ise uygulama
  açamaz: menüdeki "Windows ile başlat" kapalı görünür ve tıklanınca "Ayarlar > Uygulamalar > Başlangıç'tan açın"
  bilgisi gösterilir (`ms-settings:startupapps` açılır).
- Paketsiz (geliştirme kurulumu): bugünkü Run anahtarı davranışı aynen kalır.
- Paketliyken Run anahtarına hiç yazılmaz.

### 7.6 Kurtarma

- Paketli kurulumda masaüstü kısayolu oluşturulmaz; kurtarma Başlat'taki "Görev çubuğunu geri getir" girişidir.
- `RestoreRunner` "CamAda" adlı tüm süreçleri kapatır; bu, geliştirme ve Store sürümleri yan yana kuruluysa ikisini de
  kapatır — istenen davranış.
- Tek örnek kilidi `Local\CamAda.Main` tam güvenli (AppContainer dışı) paketli uygulamada da aynı ad alanında olduğu
  için iki sürüm aynı anda çalışamaz; ikinci açılan sessizce kapanır (bugünkü davranış).

### 7.7 Güncelleme ve kaldırma

- Açılışta `RegisterApplicationRestart` (bekçi ve kurtarma kiplerinde değil): Store güncellemesi uygulamayı kapattıktan
  sonra Windows CamAda'yı yeniden açar.
- Güncelleme/kaldırma uygulamayı kapattığında görev çubuğu geri gelmeli: denemede kaldırmada geldi; güncellemede
  §8'de doğrulanır. Gelmiyorsa çözüm aranır (örn. paket katalog olayları); bu, planın kapanış koşuludur.

### 7.8 Konum izni zamanlaması

- Paketliyken Wi‑Fi listesi için `WiFiAdapter.RequestAccessAsync` açılışta değil, hızlı ayarlar paneli ilk kez
  açıldığında çağrılır (Wi‑Fi kutucuğu o ana dek "liste için dokunun" durumunda). Paketsizde bugünkü davranış kalır.
- İzin reddedilirse Wi‑Fi listesi yerine "Konum izni kapalı — Ayarlar" bağlantısı (`ms-settings:privacy-location`).

### 7.9 Yeni kullanıcıda varsayılan

- `AppSettings.HideWindowsTaskbar` varsayılanı `false` olur (yalnız ayar dosyası olmayan yeni kullanıcıyı etkiler;
  mevcut kullanıcının dosyasında `true` yazılı). İlk açılış ekranı (2. iş) gelene kadar görev çubuğu, kullanıcı ada
  menüsünden "Görev çubuğunu gizle"yi seçince gizlenir. Gerekçe: Store sertifikasyonu ve ilk izlenim — kullanıcı
  onaylamadan sistem arayüzünü gizlemeyelim.

### 7.10 Geliştirme kurulumundan geçiş (yalnız bu bilgisayar)

- Paketli uygulama, paket kurulmadan önce var olan `AppData` dosyalarını okuyabiliyorsa (MSIX'in beklenen davranışı)
  mevcut ayarlar, Ada hafızası ve anahtarlar kendiliğinden görünür, yeni yazımlar paketin klasörüne gider. Doğrulamada
  bu denenir; okunamıyorsa ilk paketli açılışta tek seferlik içe aktarma eklenir.
- Store sürümüne geçerken geliştirme kurulumu kaldırılır: `tools/uninstall-dev.ps1` (görev çubuğunu geri getirir, Run
  değerini ve `Programs\CamAda`'yı siler, masaüstü kurtarma kısayolunu siler; veri klasörlerine dokunmaz).

### 7.11 Store'a gizli gönderim

- Partner Center'da ad ayrılır → `store-identity.json` doldurulur.
- Gönderim: görünürlük "Özel hedef kitle" (yalnız kullanıcının Microsoft hesabı), fiyat ücretsiz.
- `runFullTrust` gerekçesi: "Uygulama Windows görev çubuğunun yerine geçen bir masaüstü kabuğudur; pencereleri, sistem
  tepsisini ve görev çubuğunu yönetmek için tam güven gerekir."
- Sertifikasyon notu: görev çubuğunu yalnız kullanıcı isteyince gizlediği, Başlat'taki kurtarma girişi, kaldırınca
  görev çubuğunun geri geldiği.
- Mikrofon/konum kullandığı için gizlilik politikası adresi zorunlu: 5. işe kadar geçici, dürüst ve kısa bir sayfa
  (Ada'nın uyandırma sözcüğünden sonra sesi Google Gemini'ye gönderdiği, başka hiçbir veri toplanmadığı). Yeri: kullanıcının
  Vercel hesabı ya da GitHub Pages — uygulayıcı seçer.

## 8. Doğrulama listesi

Yerel (gevşek kayıt, geliştirici modu yalnız deneme süresince):
- [ ] İlk açılış: çökme yok, izin penceresi yok, görev çubuğu gizlenmiyor (yeni varsayılan).
- [ ] Menüden gizle → kurtarma girişi → görev çubuğu geri, süreç yok.
- [ ] Gizliyken kaldır → görev çubuğu geri, süreç yok, gerçek Run değeri yok/değişmemiş.
- [ ] 1.0.0 kuruluyken 1.0.1 kaydı (güncelleme) → görev çubuğu geri gelir, CamAda yeniden açılır.
- [ ] Hızlı ayarlar paneli ilk açılışta konum izni sorar; Wi‑Fi, Bluetooth, ses, parlaklık çalışır.
- [ ] Uygulama arama (son kullanılanlar dahil), pano, ekran görüntüsü, takvim, medya kartı çalışır.

Gerçek Store kurulumu (gizli yayın):
- [ ] Kurulum uyarısız; bekçi `WindowsApps` klasöründen başlıyor.
- [ ] Oturum açılışında başlıyor; "Windows ile başlat" kapat/aç çalışıyor.
- [ ] Gizliyken kaldırma ve Store güncellemesi görev çubuğunu geri getiriyor.
- [ ] Ada (kullanıcıyla elle): ilk mikrofon izni, uyandırma, bir komut, kapatma.
- [ ] Geliştirme kurulumundan geçiş (§7.10).

## 9. Uygulayıcıya kalanlar

- Simge görsellerinin üretimi (geçici), manifestteki açıklama metinleri.
- Yeteneklerin doğrulamada daraltılması.
- `makeappx` yolunun bulunması (en yeni SDK sürümü).
- Geçici gizlilik sayfasının barındırılacağı yer.

## 10. Açık soru (kullanıcının)

- Store'daki uygulama adı (Partner Center'da ayrılacak ad; paket kimliği buna bağlı).
