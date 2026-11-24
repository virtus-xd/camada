# Hızlı ayarlar ve pil (Plan 4.3) — tasarım

Tarih: 2026-11-24 · Durum: kullanıcı onaylı (24.11.2026) · Bağlı tasarım: `2026-11-23-cam-ada-design.md` (§7.1, §7.2, §7.4 "Hızlı ayarlar", §7.5)

## Amaç ve neden

Kullanıcı Windows görev çubuğunu tamamen kapatıp günlük işlerini yalnız ada üzerinden yapmak istiyor (ana tasarım §3). Görev çubuğu kapanınca Windows'un Hızlı Ayarlar'ı da gözden çıkıyor. Wi‑Fi'ye bağlanmak, kulaklığı bağlamak, parlaklığı değiştirmek ve pile bakmak gibi günlük işler için adada bir karşılık gerekiyor. Kullanıcı bu işler için Ayarlar'a gitmek istemiyor; bu yüzden Wi‑Fi ve Bluetooth listeleri de panelde olacak.

Bulgular (24.11.2026, yalnız okuyarak):

- **Bilgisayar dizüstü:** Lenovo, kasa türü 10, pil `L20D4PC0`, 48,9 Wh. Görev çubuğu kapalıyken pil yüzdesi hiçbir yerde görünmüyor; ana tasarımda da yoktu. Bu plana eklendi (kullanıcı onaylı).
- **Ağ:** MediaTek MT7921 Wi‑Fi bağlı (WPA2-Personal ev ağı); Realtek Ethernet takılı değil. Windows 11 24H2'den beri masaüstü uygulamalarının ağ adlarını görebilmesi için konum izni gerekiyor. Kullanıcıda konum ve "masaüstü uygulamaları konuma erişebilsin" açık.
- **Bluetooth:** MediaTek adaptör var, Bluetooth şu an **kapalı**. Eşleşmiş cihazlar:
  - Buds2 kulaklık; iki ses ucu var: "Kulaklıklar (Buds2)" ve "Kulaklık (Buds2 Hands-Free)".
  - R1mouse1 (fare).
  - Wireless Controller (oyun kolu).
- **Ekran:** Yalnız dahili panel bağlı (`LEN9056`). Parlaklığı Windows'un ekran parlaklığı yoluyla (WMI) okunuyor, şu an %80. Ses uçlarında `X24F165S` ve `webOS TV` görünüyor; demek ki zaman zaman harici monitör ya da TV bağlanıyor.
- **Resmi yollar canlı yoklandı:**
  - Telsiz erişimi `Allowed` (Wi‑Fi açık, Bluetooth kapalı).
  - Wi‑Fi erişimi `Allowed`.
  - `PowerManager` pil durumunu veriyor.
  - `FocusSessionManager.IsSupported = True` (Odak şu an kapalı).
- **Tema:** Koyu (`AppsUseLightTheme = 0`, `SystemUsesLightTheme = 0`).

## Hedefler

1. Genişlemiş adaya bir **Wi‑Fi simgesi** (pano ile ses arasına) ve bir **pil simgesi** (ses ile saat arasına) eklenir. İkisine de tıklamak adanın altında aynı **Hızlı ayarlar** panelini açar.
2. Panelin içeriği:
   - dört kutucuk: **Wi‑Fi, Bluetooth, Odak, Koyu tema**;
   - **parlaklık** kaydırıcısı;
   - dört düğme: **Ekran görüntüsü, Kilitle, Uyku, Görev Yöneticisi**;
   - başlıkta **pil** durumu.
3. Wi‑Fi ve Bluetooth kutucuklarındaki ok (›), panelin içinde bir liste açar:
   - Wi‑Fi ağlarına bağlanma: kayıtlı ağa tek tık, yeni ağa şifreyle;
   - eşleşmiş Bluetooth ses cihazlarını bağlama ve ayırma.
4. Pil düşükken (≤ %20 ve şarjda değil) küçük adada uyarı görünür.
5. Parlaklık Fn tuşlarıyla değişince adada kısa bir gösterge çıkar (ana tasarım §7.5).
6. Boştayken işlemci kullanımı ~%0 olur; ağ taraması yalnız Wi‑Fi listesi açıkken yapılır.

## Hedef dışı

- **Gece ışığı ve Uçak modu.** İkisinin de resmi yolu yok; kullanıcı kararıyla çıkarıldı (ana tasarım §7.4'teki listeden düşer).
- **Win+A ve Windows'un kendi Hızlı Ayarlar'ı.** Dokunulmaz; Win+A yine Windows'unkini açar.
- **Bluetooth'ta:**
  - yeni cihaz eşleştirme (Windows'un ekranı açılır);
  - cihazı kaldırma;
  - cihazların pil düzeyini gösterme;
  - kendiliğinden bağlanan cihazları (fare, klavye, oyun kolu) elle bağlama ya da ayırma. Bunlarda yalnız durum görünür.
- **Wi‑Fi'de:**
  - kurumsal (kullanıcı adı isteyen) ve gizli ağlara panelden bağlanma; bunlarda Windows Ayarları açılır;
  - kayıtlı bir ağı unutma;
  - VPN, mobil erişim noktası açma ve otel tarzı giriş sayfaları. Otel tarzı giriş sayfasını Windows kendisi tarayıcıda açar.
- **Güç modu ve Enerji tasarrufu.** Resmi ayarlama yolları yok; panelde yer almazlar.
- **CamAda'nın kendi görünümünün temaya uyması.** Ada ve paneller hep koyu cam kalır.
- **Windows'un kendi parlaklık göstergesini gizlemek.** Sesteki "ikisi kalsın" kararıyla tutarlı.

## Olmaması gerekenler (çalışsa bile başarısızlık sayılır)

- Bir kutucuğun gerçek durumdan farklı görünmesi (ör. Wi‑Fi kapalıyken "Açık" yazması).
- Bir düğmenin sessizce hiçbir şey yapmaması. Başarısız olan her işlem, kutucukta ya da satırda kısa bir hata metni gösterir.
- Wi‑Fi şifresinin CamAda tarafından diske, ayar dosyasına ya da günlüğe yazılması. Şifreyi Windows kendi ağ profilinde saklar.
- Ağ taraması, bağlanma, harici monitör ya da Bluetooth işlemleri sırasında adanın ya da panelin donması.
- Koyu tema dışında, resmi olmayan bir yolun kullanılması.

## Yaklaşımlar

1. **CamAda'nın kendi paneli ve Windows'un belgelenmiş yolları (seçilen).** Panel diğer cam panellerle aynı görünür ve aynı yerde açılır. Her kutucuk Windows'un resmi yoluyla çalışır:
   - aç/kapat için telsiz yönetimi (`Windows.Devices.Radios`);
   - Wi‑Fi ağları ve bağlantı;
   - Bluetooth cihaz listesi;
   - pil için `PowerManager`;
   - Odak için `FocusSessionManager`;
   - parlaklık için dahili ekranda WMI, harici ekranda DDC/CI.

   Tek istisna Koyu tema: Windows'un yıllardır değişmeyen, açıkça okunabilen tek ayarı. Kullanıcı bunu bilerek onayladı.
2. **Bazı kutucukları Windows ekranlarına yönlendirmek** (Wi‑Fi listesi için Windows'un ağ penceresi, Bluetooth için Ayarlar). Elendi: kullanıcı listelerin panelde olmasını seçti, çünkü amaç adadan çıkmamak.
3. **Adadan Windows'un kendi Hızlı Ayarlar'ını açmak.** Elendi: o panel ekranın sağ altında, Windows'un görünümüyle açılıyor ve adayla bağı kopuyor.
4. **Gece ışığı ve Uçak modu için Windows'un iç ayarlarını değiştirmek.** Elendi (kullanıcı kararı): Windows güncellemeleriyle bozulabilir.

Kullanıcı nasıl yapılacağını bize bıraktı; 1 bizim seçimimiz.

## Kullanıcının verdiği kararlar

- Wi‑Fi ve Bluetooth listeleri panelde olacak (kayıtlı ağa tek tık, yeni ağa şifre; ses cihazlarını bağla/ayır).
- Pil adaya eklenecek.
- Gece ışığı ve Uçak modu olmayacak.
- Koyu tema kalacak.
- Panel taslağı ve şu ayrıntılar kullanıcıya özetlendi, kullanıcı "tamam" dedi:
  - simge yerleri;
  - küçük adada pilin yalnız %20'nin altında görünmesi;
  - liste davranışları;
  - Odak;
  - Windows'un parlaklık göstergesinin de çıkmaya devam etmesi;
  - Ekran görüntüsü, Kilitle ve Uyku düğmelerinin davranışı.

## Davranış

### Adada

- **Genişlemiş satır:** `^ | pano | Wi‑Fi | ses | pil | saat`. Pil yoksa (ör. masaüstü bilgisayarda) pil simgesi hiç çıkmaz.
- **Wi‑Fi simgesi**, internete hangi bağlantıyla çıkıldığını gösterir:

  | Durum | Görünüm |
  |---|---|
  | Ethernet | kablo simgesi |
  | Wi‑Fi bağlı | sinyal gücüne göre 1–4 çubuk |
  | Bağlı ama internet yok | çubuklar + küçük uyarı işareti |
  | Wi‑Fi açık, bağlı değil | boş çubuklar |
  | Wi‑Fi kapalı | kapalı simgesi |

  Erişilebilirlik adı durumu söyler, ör. "Wi‑Fi: <ağ adı>, internet var".
- **Pil simgesi:**
  - doluluk 10'luk adımlarla gösterilir;
  - şarjdayken şimşek eklenir;
  - ≤ %20 ve şarjda değilken uyarı renginde (`WarningBrush`) görünür;
  - erişilebilirlik adı ör. "Pil %76, 3 sa 10 dk kaldı".
- **Küçük ada (ana tasarım §7.1):** Pil yalnız ≤ %20 ve şarjda değilken sağ sütunda, mikrofon işaretinin yanında görünür: uyarı renginde simge + "%18". Şarja takılınca kaybolur. Küçük adanın sığma hesabı bu simgeyi de saymalıdır (bkz. Plan 4.1 ertelenen 3).
- **Küçük adada Wi‑Fi:** Wi‑Fi simgesi küçük adada görünmez; küçük ada yalnız düşük pili gösterir.
- **Panel açma:** Wi‑Fi ya da pil simgesine tıklamak aynı Hızlı ayarlar panelini açar. Panel açıkken ikisinden birine yeniden tıklamak paneli kapatır. Paneli açan ayrı bir klavye kısayolu yoktur (bizim kararımız; Win+A Windows'ta kalır).

### Panel

```
┌─ Hızlı ayarlar ───────────────── 🔋 %76 · 3 sa 10 dk ┐
│ [📶 Wi‑Fi        › ]   [ᛒ Bluetooth     › ]          │
│   <ağ adı>               Buds2 bağlı                 │
│ [◐ Odak            ]   [◑ Koyu tema       ]          │
│   Kapalı                 Açık                        │
│ ☀  ━━━━━━━━━━━━━○─────  %80                          │
│ [Ekran görüntüsü] [Kilitle] [Uyku] [Görev Yöneticisi]│
└──────────────────────────────────────────────────────┘
```

- **Başlık:** Solda "Hızlı ayarlar", sağda pil durumu, şu biçimlerden biri:
  - "%76 · 3 sa 10 dk"
  - "Şarj oluyor · %76"
  - "Tam dolu"
  - "Prize takılı · %80" (şarj etmiyor)

  Windows kalan süreyi tahmin edemiyorsa yalnız yüzde görünür. Pil durumuna tıklamak Windows'un pil ayarlarını açar (`ms-settings:batterysaver`). Pil yoksa başlıkta yalnız "Hızlı ayarlar" yazar.
- **Kutucuklar (2×2):** Açık olan kutucuk vurgu renginde (`AccentBrush`) görünür. Kutucuğun ana alanına tıklamak aç/kapat yapar. Wi‑Fi ve Bluetooth'ta sağdaki ›, panelin içinde listeyi açar.

  | Kutucuk | Alt yazı |
  |---|---|
  | Wi‑Fi | bağlı ağın adı / "Bağlı değil" / "Kapalı" |
  | Bluetooth | "Buds2 bağlı" / "2 cihaz bağlı" / "Açık" / "Kapalı" |
  | Odak | "Kapalı" / "18 dk kaldı" (süresi bilinmiyorsa "Açık") |
  | Koyu tema | "Açık" / "Kapalı" |

  Wi‑Fi ya da Bluetooth adaptörü yoksa kutucuk soluk görünür ve "Yok" yazar.
- **Parlaklık:** Bkz. "Parlaklık" bölümü.
- **Düğmeler:** Her biri önce paneli kapatır. Bkz. "Düğmeler" bölümü.
- **Canlı güncelleme:** Panel açıkken durum başka bir yerden değişirse (ör. Fn tuşu, Windows Ayarları) panel bunu izler.
- **Klavye:**
  - Tab gezinir;
  - Boşluk ya da Enter kutucuğu değiştirir;
  - → ya da › listeyi açar;
  - Esc liste sayfasında ana sayfaya döner, ana sayfada paneli kapatır.
- **Geçişler:** Liste sayfasına geçiş kısa bir kaydırma animasyonuyla olur; "Animasyonları azalt" açıksa animasyon yoktur.

### Wi‑Fi listesi

```
┌─ ‹  Wi‑Fi ──────────────────────────── [Açık ●] ┐
│ ✓ <ev ağı>             ▂▄▆█  Bağlı              │
│                              [Bağlantıyı kes]   │
│   Telefon              ▂▄▆   🔒                 │
│   Kafe_Misafir         ▂▄    Açık ağ            │
│ ─────────────────────────────────────────────── │
│ Diğer ağlar ve Wi‑Fi ayarları ›                 │
└─────────────────────────────────────────────────┘
```

- **Tarama:** Liste açılınca bir tarama başlar ve liste açık kaldıkça yaklaşık 10 sn'de bir yenilenir. Liste kapanınca tarama durur.
- **Tekilleştirme:** Aynı ağ adı birden çok erişim noktasından görünüyorsa tek satır olur (en güçlü sinyal). Adı olmayan (gizli) ağlar listelenmez.
- **Sıralama:** Önce bağlı ağ, sonra kayıtlı ağlar, sonra diğerleri; her grup kendi içinde sinyal gücüne göre sıralanır.
- **Satıra tıklayınca satır genişler:**
  - kayıtlı ya da şifresiz ağda **Bağlan** düğmesi çıkar;
  - şifreli yeni ağda şifre kutusu (göster/gizle ile) ve **Bağlan** çıkar;
  - bağlı ağda **Bağlantıyı kes** çıkar.

  Yeni ağ "otomatik bağlan" açık olarak kaydedilir (Windows'taki varsayılan).
- **Bağlanma sonucu:** Bağlanırken satırda "Bağlanıyor…" yazar. Sonunda "Bağlandı", "Bağlanamadı" ya da "Şifre yanlış olabilir" görünür.
- **Şifresiz ağlar:** Satırda "Açık ağ" yazar.
- **Kurumsal ağlar:** Satıra tıklamak Windows'un Wi‑Fi ayarlarını açar (`ms-settings:network-wifi`).
- **Alt satır:** "Diğer ağlar ve Wi‑Fi ayarları", aynı ayar sayfasını açar. Gizli ağa bağlanmak oradan yapılır.
- **Wi‑Fi kapalıyken:** "Wi‑Fi kapalı" yazar; başlıktaki anahtar Wi‑Fi'yi açar.
- **Konum izni kapalıyken** (ağ adları gelmiyorsa): "Ağ adlarını görmek için Windows konum izni gerekiyor" yazar ve "Konum ayarlarını aç" bağlantısı çıkar (`ms-settings:privacy-location`).
- **Şifrenin güvenliği:** Şifre yalnız bağlanma çağrısına verilir. CamAda onu saklamaz ve günlüğe yazmaz. Bağlanma bitince ya da satır kapanınca şifre kutusu temizlenir.

### Bluetooth listesi

```
┌─ ‹  Bluetooth ──────────────────────── [Açık ●] ┐
│ 🎧 Buds2                 Bağlı         [Ayır]   │
│ 🖱 R1mouse1              Bağlı                  │
│ 🎮 Wireless Controller   Bağlı değil            │
│ ─────────────────────────────────────────────── │
│ Yeni cihaz ekle ›                               │
└─────────────────────────────────────────────────┘
```

- **Sıralama:** Eşleşmiş cihazlar listelenir, bağlı olanlar üstte.
- **Düğmeler:** Ses cihazlarında (kulaklık, hoparlör) **Bağlan** ya da **Ayır** düğmesi olur. Diğer cihazlarda yalnız durum yazar.
- **Bağlanma sonucu:** Bağlanırken "Bağlanıyor…" yazar. Sonunda durum güncellenir ya da "Bağlanamadı — cihaz açık ve yakında mı?" görünür.
- **Bluetooth kapalıyken:** "Bluetooth kapalı" yazar; başlıktaki anahtar Bluetooth'u açar.
- **Yeni cihaz ekle:** Windows'un Bluetooth ayarlarını açar (`ms-settings:bluetooth`); eşleştirme orada yapılır.
- **Canlı güncelleme:** Bir cihaz kendiliğinden bağlanır ya da koparsa liste güncellenir.

### Odak

- **Başlatma:** Kapalıyken tıklamak Windows'un odak oturumunu **30 dakikalığına** başlatır. 30 dk Windows'un varsayılan süresidir; ayar dosyasında `QuickSettings.FocusMinutes` ile değiştirilebilir. Windows'un kendi Odak ayarındaki süre resmi bir yolla okunamadığı için kullanıcının o ayarı izlenmez.
- **Açıkken:** Kalan süre görünür ve dakikada bir güncellenir (sayaç yalnız panel açıkken çalışır). Tıklamak oturumu bitirir.
- **Dışarıdan değişirse:** Odak başka bir yerden (ör. Saat uygulamasından) başlatılır ya da bitirilirse kutucuk bunu izler (`IsFocusActiveChanged`). Böyle bir oturumun bitiş zamanı bilinmiyorsa kutucukta yalnız "Açık" yazar.
- **Bildirimler:** Odak açıkken bildirimleri Windows kendisi susturur; CamAda ayrıca bir şey yapmaz.

### Koyu tema

- **Ne zaman açık sayılır:** Windows'ta uygulamalar koyu moddaysa kutucuk açık görünür.
- **Tıklayınca:** Uygulamalar ve Windows modu birlikte değişir, tıpkı Ayarlar'daki "Modunuzu seçin: Koyu/Açık" gibi. Ardından Windows'a ayarın değiştiği duyurulur (`WM_SETTINGCHANGE`, `"ImmersiveColorSet"`). İki ayar farklıysa (Windows'taki "Özel" mod), tıklamak ikisini birden eşitler.
- **Dışarıdan değişirse:** Ayar başka bir yerden değişirse kutucuk bunu izler.

### Parlaklık

- **Dahili ekran:** Windows'un ekran parlaklığı yolu (WMI) kullanılır.
- **Harici ekran:** Monitör DDC/CI destekliyorsa, Windows'un monitör ayar yoluyla (`dxva2`) monitörün adıyla ayrı bir kaydırıcı çıkar.
- **Ayarlanamayan ekranlar:** Hiçbir ekran ayarlanamıyorsa parlaklık satırı hiç çıkmaz. Ekranlar birden fazlaysa her biri kendi satırında görünür; dahili ekranın adı "Ekran" olur.
- **Sürükleme:** Sürüklerken parlaklık canlı değişir; ekrana gönderim seyrekleştirilir. DDC/CI yavaş olabildiği için arayüz kilitlenmez. Harici ekranın değeri panel açılırken okunur; monitörün kendi düğmeleriyle yapılan değişiklikler izlenmez.
- **Adadaki gösterge:** Dahili parlaklık Fn tuşlarıyla ya da Windows'tan değiştiğinde, panel kapalıysa adada kısa gösterge çıkar: ☀ + çubuk + "%80". Ses göstergesiyle aynı yerde, aynı sürede görünür ve aynı kurallara uyar (tam ekranda, ada genişken ve panel açıkken çıkmaz). CamAda'nın kendi kaydırıcısından gelen değişiklik gösterge çıkarmaz.
- **Windows'un göstergesi:** Windows'un kendi parlaklık göstergesi de çıkmaya devam eder.

### Düğmeler

- **Ekran görüntüsü:** Panel kapanır. Panelin görüntüye girmemesi için kısa bir bekleme olur, sonra Windows'un ekran alıntısı aracı açılır (`ms-screenclip:`), tıpkı Win+Shift+S gibi. Sonrasını Plan 4.2'nin önizlemesi ve şeridi karşılar.
- **Kilitle:** Başlat panelindeki gibi çalışır (`LockWorkStation`, onaysız).
- **Uyku:** Başlat panelindeki gibi çalışır (`PowerHelper.Sleep`, onaysız).
- **Görev Yöneticisi:** `taskmgr.exe` açılır.

## Bizim kararlarımız

**Kullanıcıya özetlenip onaylananlar:**

- simge yerleri;
- küçük adada pil eşiğinin %20 olması;
- listelerin panelin içinde ayrı bir sayfa olması;
- Bluetooth'ta yalnız ses cihazlarında Bağlan/Ayır olması;
- Odak'ın oturumu başlatıp kalan süreyi göstermesi;
- harici monitörün DDC/CI destekliyorsa kendi kaydırıcısını alması;
- Windows'un parlaklık göstergesinin kalması;
- düğmelerin davranışı.

**Bu dokümanda eklenenler (kullanıcının okurken kontrol etmesi için):**

- Odak süresi 30 dk ve sabit; Windows'un kendi ayarındaki süre izlenmez (önceki "Saat uygulamasındaki varsayılan süre" önerisinin düzeltmesi; kullanıcı "30 dakika kalsın" dedi).
- Wi‑Fi ve pil simgesi aynı paneli açar.
- Kurumsal ve gizli ağlarda Windows Ayarları açılır; adı olmayan ağlar listelenmez.
- Yeni ağ "otomatik bağlan" açık olarak kaydedilir.
- Liste açıkken tarama yaklaşık 10 sn'de bir yapılır.
- Pil durumuna tıklamak Windows'un pil ayarlarını açar.
- Koyu temada uygulama ve Windows modu birlikte değişir.

## Ana tasarımda değişen

- **§7.4 "Hızlı ayarlar":** Gece ışığı ve Uçak modu çıkarıldı. Pil durumu ile Wi‑Fi ve Bluetooth listeleri eklendi.
- **§7.2:** Genişlemiş satıra ses ile saat arasına pil simgesi eklendi.
- **§7.1:** Küçük adada, pil düşükken uyarı simgesi ve yüzdesi görünür.
- **§7.5 "Ses ya da parlaklık değişti":** Parlaklık göstergesi bu planda yapılıyor; Windows'un kendi göstergesi kalıyor (Plan 4.1'deki ses kararıyla aynı).

## Kısıtlar

- **Önceki planlardan devralınanlar (Plan 1–4.2):**
  - Windows 11 build 26300;
  - `CamAda.Core` WPF'siz ve birim testli;
  - Explorer'a enjeksiyon yok;
  - arayüz Türkçe;
  - yazı tipi Manrope, simgeler Segoe Fluent Icons;
  - derleme uyarısız.
- **Yalnız Windows'un belgelenmiş yolları kullanılır** (kullanıcı kuralı; tek istisna Koyu tema).
  - **Bluetooth ses bağlama:** Microsoft'un belgelediği Bluetooth ses sürücüsü komutu kullanılır (`KSPROPSETID_BtAudio`: `KSPROPERTY_ONESHOT_RECONNECT` / `KSPROPERTY_ONESHOT_DISCONNECT`, ses ucunun `IKsControl`'ü üzerinden). Ses ucu ile Bluetooth cihazı kapsayıcı kimliğiyle (ContainerId) eşleştirilir.
- **Henüz canlı doğrulanmayan iki riskli yol** ilk görevde denenecek:
  - **Odak:** Paketsiz bir masaüstü uygulamasının `FocusSessionManager.TryStartFocusSession` ile oturum başlatabildiği doğrulanmadı; yalnız `IsSupported` denendi. Çalışmazsa Odak kutucuğu Windows'un Odak ayarlarını açar.
  - **Bluetooth ses bağlama:** Yukarıdaki komut çalışmazsa Bağlan/Ayır yerine "Bluetooth ayarlarında bağlan" bağlantısı çıkar.

  Herhangi bir geri çekilme olursa kullanıcıya söylenir.
- **Olay tabanlı çalışma, yoklama yok.** Dinlenen olaylar:
  - ağ durumu: `NetworkInformation.NetworkStatusChanged` ve Wi‑Fi bağlantı bildirimleri;
  - telsizler: `Radio.StateChanged`;
  - pil: `PowerManager` olayları;
  - Odak: `IsFocusActiveChanged`;
  - tema: `WM_SETTINGCHANGE`;
  - parlaklık: WMI parlaklık olayı.

  Yalnız iki şey zamanlayıcıyla çalışır: liste açıkken Wi‑Fi taraması ve panel açıkken Odak'ın dakikalık sayacı.
- **Uzun işler arayüz iş parçacığı dışında yapılır** (tarama, bağlanma, DDC/CI, Bluetooth komutları). Panel kapandıktan sonra gelen sonuçlar arayüze yazılmaz, ama işlemin kendisi (ör. bağlanma) sürer.
- **Ayar:** `QuickSettings.FocusMinutes`, varsayılan 30. 5–240 dışındaki değer varsayılana döner.
- **Ada genişliği:** `IslandLayout.SystemSectionWidth`, Wi‑Fi ve pil düğmeleri için büyür. En geniş hap sınırı (1250 DIP) korunur.
- **Canlı deneme kuralları:**
  - **Wi‑Fi:** Wi‑Fi'yi kapatan ya da bağlantıyı kesen her deneme, **aynı komutun içinde** Wi‑Fi'yi geri açar, ev ağına yeniden bağlanır ve internetin geldiğini doğrulamadan bitmez. Claude Code oturumu bu bağlantıyla çalışıyor; bağlantı açık kalırsa kimse düzeltemez.
  - **Durum değiştiren denemeler** (Bluetooth, parlaklık, Koyu tema, Odak): Önce özgün durum kaydedilir, deneme sonunda geri konur. Bluetooth'un özgün durumu şu an kapalı.
  - **Buds2:** Buds2'ye bağlanma denemesi yalnız kulaklık açık ve yakındaysa yapılır; değilse kullanıcının elle deneme listesine girer.
  - **Canlıda basılmayan düğmeler:**
    - Kilitle ve Uyku: oturumu kilitler ya da uyutur;
    - Görev Yöneticisi: otomatik yükseltilir ve kapatılamaz;
    - Ekran görüntüsü: ekran alıntısı aracı ESC tuşu göndermeden kapatılamaz.

    Bu dört düğme, gerçek işlem yerine sahte bir başlatıcıyla denenir ve kullanıcının elle deneme listesine girer.
  - **Yeni ağa şifreyle bağlanma:** Ortamda ikinci bir ağ olmadığı için canlıda denenemez. Kullanıcının elle denemesidir (ör. telefonun erişim noktası).

## Yapıcıya bırakılanlar

- **Görev bölümü** (yaklaşık 5–6 görev). Örnek bir sıra:
  1. Riskli iki yolun (Odak başlatma, Bluetooth ses bağlama) canlı doğrulanması ve Core kuralları.
  2. Adadaki Wi‑Fi ve pil simgeleri, panel iskeleti, pil başlığı, Odak ve Koyu tema kutucukları, düğmeler.
  3. Parlaklık kaydırıcıları ve adadaki gösterge.
  4. Wi‑Fi kutucuğu, liste ve bağlanma.
  5. Bluetooth kutucuğu, liste ve bağlama.
  6. Küçük adada pil uyarısı.
- **Görünüm ayrıntıları:** ölçüler; Segoe Fluent glifleri; panel genişliği (diğer paneller ~360 DIP); kutucuk ve liste satırı görünümü (mevcut `Panels.xaml` stilleri); liste sayfasının en büyük yüksekliği ve kaydırma.
- **Wi‑Fi teknolojisi:** WinRT `WiFiAdapter` ile yerel `wlanapi` arasında seçim ya da ikisinin karışımı (kayıtlı profil bilgisi, bağlantı kesme ve bildirimler için).
- **Biçimler:** Pil kalan süresinin biçimi ("3 sa 10 dk", "45 dk"); harici monitör adının nereden alınacağı (EDID adı, yoksa "Harici ekran").
- **Core'da birim testli saf mantık:**
  - Wi‑Fi simgesi seçimi;
  - ağ listesinin tekilleştirilmesi ve sıralaması;
  - pil metni, pil simgesi ve küçük ada eşiği;
  - Odak kalan süre metni;
  - parlaklık seyrekleştirmesi;
  - tema durumunun yorumu;
  - Bluetooth cihaz türünün "bağlanabilir mi" kararı;
  - `FocusMinutes` doğrulaması.

  Telsizler, ağ, Bluetooth, parlaklık ve Odak canlı denemeyle doğrulanır (yukarıdaki kurallarla).
