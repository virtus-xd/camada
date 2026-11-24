# Pano ve ekran görüntüleri (Plan 4.2) — tasarım

Tarih: 2026-11-24 · Durum: kullanıcı onaylı (24.11.2026) · Bağlı tasarım: `2026-11-23-cam-ada-design.md` (§7.2, §7.4 "Pano", §7.5 "Ekran görüntüsü alındı", §8, §10, §11)

## Amaç ve neden

Kullanıcı Windows görev çubuğunu kapatıp günlük işlerini yalnızca ada üzerinden yapmak istiyor (ana tasarım §3). Pano geçmişi bu işlerden biri; kullanıcı "Ekler"den bilerek seçti. Ekran görüntüsünü sık alıyor (`Resimler\Screenshots`'ta 93 dosya, en sonuncusu 15.11.2026); ana tasarım son ekran görüntülerinin Pano panelinde bir şeritte durmasını ve yeni ekran görüntüsünde adada kısa önizleme çıkmasını öngörüyor.

Bulgular (24.11.2026): Windows'un kendi pano geçmişi kullanıcıda **kapalı** (`EnableClipboardHistory` yok). Ekran Alıntısı Aracı otomatik kaydediyor; ekran görüntüsü klasörü `C:\Users\<kullanıcı>\Pictures\Screenshots` (FOLDERID_Screenshots, varsayılan konum). Print Screen tuşu Ekran Alıntısı Aracı'nı açıyor (`PrintScreenKeyForSnippingEnabled = 1`).

## Hedefler

1. Genişlemiş adada **pano düğmesi** (^ ile ses düğmesi arasında, ana tasarım §7.2) ve altında açılan **Pano paneli**.
2. Panel: son kopyalananlar (metin, bağlantı, görsel, kod); sabitleme; tek öğe silme; "Sabitlenmeyenleri sil"; altta son ekran görüntüleri şeridi.
3. **Win+Alt+V** her yerden paneli açar (ana tasarımdaki Ctrl+Shift+V'nin yerine, kullanıcı kararı).
4. Öğe seçince:
   - panel **Win+Alt+V ile** açıldıysa: panel kapanır, önceki pencere öne gelir ve öğe **oraya yapıştırılır**;
   - panel **adadaki düğmeyle** açıldıysa: öğe yalnızca panoya alınır, satırda "Kopyalandı" görünür.
5. Şifre yöneticilerinin "geçmişe kaydetme" diye işaretlediği kopyalar hiç saklanmaz.
6. Yeni ekran görüntüsünde adada ~5 sn **tıklanabilir önizleme**: küçük resim + Kopyala + Düzenle.
7. Boştayken ~%0 işlemci; kopyalamak başka uygulamalarda yavaşlamaz.

## Hedef dışı

- Win+V ve Windows'un kendi pano geçmişi: dokunulmaz, açılıp kapatılmaz, okunmaz.
- Dosya kopyaları (Gezgin'de Ctrl+C ile kopyalanan dosyalar): geçmişe girmez.
- Geçmişte arama, öğe düzenleme, bulut eşitleme.
- Ekran görüntüsünü bizim almamız (Ekran Alıntısı Aracı alır; hızlı ayarlardaki "Ekran görüntüsü" düğmesi Plan 4.3'te).
- Ekran görüntüsü dosyalarını silmek ya da taşımak.
- Ekran Alıntısı Aracı'nın kendi bildirimini gizlemek (ses göstergesindeki "ikisi kalsın" kararıyla tutarlı).

## Olmaması gerekenler (çalışsa bile başarısızlık sayılır)

- Yapıştırmanın **yanlış pencereye** gitmesi ya da Win/Alt basılıyken gönderilip başka bir kısayola dönüşmesi.
- İşaretli bir şifrenin geçmişe, belleğe ya da diske düşmesi.
- CamAda'nın panoyu uzun süre açık tutup başka uygulamaların kopyala/yapıştırını bozması.
- Sabitlenmemiş kopyaların diske yazılması.

## Yaklaşımlar

1. **Kendi geçmişimiz (seçilen).** CamAda pano değişikliklerini kendisi dinler (Windows pano dinleyicisi, ana tasarım §8), içeriği kendisi okuyup saklar. Windows ayarından bağımsız; sabitleme ve silme tam bizim elimizde; işaretli kopyalar okunmadan elenebilir. Bedeli: Win+V ile ada ayrı listeler (Win+V zaten kapalı).
2. **Windows'un pano geçmişi (WinRT `Clipboard.GetHistoryItemsAsync`).** Win+V ile aynı liste olurdu; ama kullanıcının Windows'ta geçmişi açması gerekir, dışarıdan sabitleme API'si yok (adada "sabitle" olamaz ya da ikinci bir kayıt gerekir), 25 öğe ve 4 MB sınırı var, arka plandaki uygulamaya erişim kısıtlı. Sabitlemeyi kaybettirdiği için elendi.

Kullanıcı 1'i seçti. Yapıştırma için tek yol: önceki pencereyi öne alıp Ctrl+V tuşlaması göndermek (yalnız standart metin kutularında çalışan `WM_PASTE` Chrome'da çalışmaz). Ekran görüntüleri için: ekran görüntüsü klasörünü izlemek.

## Kullanıcının verdiği kararlar

- Kısayol **Win+Alt+V** (boş olduğu denendi; Ctrl+Shift+V Chrome'un "biçimlendirmeden yapıştır"ını ve Terminal'in yapıştırmasını bozacağı için). Ayardan değiştirilebilir.
- Yapıştırma davranışı yukarıdaki Hedef 4 (kısayolla açıldıysa yapıştır, düğmeyle açıldıysa yalnız kopyala).
- Kendi geçmişimiz (Yaklaşım 1).
- Aşağıdaki "Bizim kararlarımız" listesindeki saklama, şerit, önizleme ve tekilleştirme kuralları kullanıcıya özetlendi, "hepsi doğru" dedi.

## Davranış

### Geçmiş

- **Ne girer:** metin (biçimi varsa HTML/RTF biçimleriyle birlikte; yeniden kopyalanınca biçim korunur), görsel. Tür gösterimi: tek satırlık `http(s)://…` → **bağlantı**; koda benzeyen metin → **kod** (eşit aralıklı yazıyla); görsel → küçük resim + "Görsel 1280×720"; gerisi **metin** (ilk 2 satır, üç noktayla).
- **Ne girmez:** pano biçimleri arasında `ExcludeClipboardContentFromMonitorProcessing`, `Clipboard Viewer Ignore` olan ya da `CanIncludeInClipboardHistory` = 0 olan kopyalar (içerik okunmaz bile); boş/yalnız boşluk metin; dosya listeleri; tek başına 16 MB'ı aşan öğe.
- **Sayı ve sıra:** sabitlenmemiş en fazla 30 öğe, en yenisi üstte; 31. gelince en eskisi düşer. Sabitlenenler ayrı bölümde en üstte, sayı sınırı yok.
- **Tekilleştirme:** aynı içerik (metin için aynı düz metin, görsel için aynı pikseller) yeniden kopyalanırsa yeni satır açılmaz, mevcut satır en üste çıkar ve biçimleri (HTML/RTF) en son kopyalananla güncellenir (sabitliyse sabit bölümde kalır). CamAda'nın kendi yeniden kopyalaması da bu kurala uyar.
- **16 MB sınırı** saklanan biçim üzerinden ölçülür (görselde PNG, metinde tüm biçimlerin toplamı).
- **Saklama:** sabitlenmemişler yalnız bellekte, CamAda kapanınca (bilgisayar yeniden başlayınca dahil) silinir. Sabitlenenler `%APPDATA%\CamAda\clipboard\` altında (metin ve biçimleri JSON, görseller PNG; şifrelemesiz, yalnız kullanıcının hesabı okuyabilir) kalır; sabit kaldırılınca ya da silinince diskten de silinir.
- "Sabitlenmeyenleri sil" yalnız geçmiş listesini temizler; ekran görüntüsü şeridine ve dosyalara dokunmaz. Şerit öğeleri sabitlenmez ve silinmez.

### Panel

```
┌─ Pano ─────────────────────── Sabitlenmeyenleri sil ┐
│ 📌 adres: Atatürk Cad. No:12 …                     │
│ ─────────────────────────────────────────────────── │
│ ▸ https://github.com/…                  2 dk     📌 │
│   dotnet test CamAda.slnx --nologo      5 dk     📌 │
│   [küçük resim]  Görsel 1280×720        12 dk    📌 │
│   "toplantı yarın 14:00'te"             1 sa     📌 │
│ ─ Ekran görüntüleri ──────────────── Klasörü aç ─── │
│ [▢][▢][▢][▢][▢] →                                   │
└─────────────────────────────────────────────────────┘
```

- Her satırda: tür simgesi/küçük resim, önizleme, ne zaman kopyalandığı ("şimdi", "2 dk", "1 sa", "dün"), üzerine gelince sabitle ve sil (×) düğmeleri.
- Geçmiş boşsa: "Kopyaladıkların burada görünür."
- **Klavye:** panel açılınca ilk öğe seçili; ↑/↓ gezinir, Enter seçer, Delete siler, Esc kapatır. Kısayolla açıldıysa Esc ile kapanınca önceki pencere öne gelir.
- **Kopyalandı:** düğmeyle açılmış panelde seçilen satırda kısa süre "Kopyalandı" görünür; panel açık kalır (dışarı tıklayınca kapanır, diğer paneller gibi).
- Panel açıkken yeni kopya ya da yeni ekran görüntüsü gelirse liste/şerit canlı güncellenir.

### Win+Alt+V ve yapıştırma

- Kısayol basıldığında öndeki pencere hatırlanır, panel açılır (ada genişler). Panel açıkken kısayola yeniden basmak paneli kapatır ve önceki pencereye döner.
- Seçince: öğe panoya yazılır → panel kapanır → hatırlanan pencere öne alınır → Win, Alt, Shift, Ctrl tuşları bırakılana kadar beklenir → Ctrl+V gönderilir.
- Kısayol basıldığında öndeki pencere masaüstü ya da CamAda'nın kendi penceresiyse panel düğmeyle açılmış gibi davranır (yalnız kopyalar), çünkü yapıştırılacak yer yok.
- Görsel ya da şeritteki ekran görüntüsü yapıştırılırken panoya görsel olarak konur (uygulama görseli kabul etmiyorsa Ctrl+V bir şey yapmaz; bu kabul edilir).
- Hatırlanan pencere kapanmışsa ya da yönetici olarak çalışıyorsa (Windows tuş göndermeye izin vermez): yapıştırılmaz, öğe panoda kalır ve adada "Kopyalandı — Ctrl+V ile yapıştır" kısa göstergesi çıkar.
- Tam ekran uygulamadayken de kısayol paneli açar (kullanıcının açık isteği); ada gizli kalır, panel yine üst ortada açılır.
- Kısayol başka bir uygulamaya kayıtlıysa CamAda çalışmaya devam eder, açılışta adada bir kez "Win+Alt+V kullanılamıyor" görünür (Ctrl+Shift+M ile aynı).

### Ekran görüntüleri

- **Kaynak:** Windows'un ekran görüntüsü klasörü (FOLDERID_Screenshots; kullanıcı taşırsa yeni yeri izlenir). Klasöre yeni `.png`/`.jpg` düşmesi = yeni ekran görüntüsü.
- **Yalnız panoya giden ekran görüntüsü:** Ekran Alıntısı Aracı'nın panoya koyduğu görsel birkaç saniye içinde klasörde bir dosyayla eşleşmezse (otomatik kaydetme kapalıysa) o da yeni ekran görüntüsü sayılır.
- **Tek kayıt:** aynı ekran görüntüsü hem panoya hem klasöre düştüğünde şeritte bir kez görünür, geçmiş listesine ayrıca eklenmez. Yalnız panoya gittiyse (dosyası yoksa) geçmiş listesinde görsel olarak görünür.
- **Şerit:** klasördeki en yeni 10 görüntü, en yenisi solda, küçük resim olarak. Tıklamak geçmiş öğesi gibi çalışır (kopyalar; kısayolla açıldıysa yapıştırır). Sağ tık: "Düzenle", "Klasörde göster". Başlıkta "Klasörü aç".
- **Önizleme (ana tasarım §7.5):** yeni ekran görüntüsünde ada ~5 sn kısa genişleme gösterir: küçük resim + "Ekran görüntüsü" + **Kopyala** + **Düzenle**.
  - Diğer kısa göstergelerden farklı olarak ada örtülüyken de **tıklanabilir**; imleç üzerindeyken kapanmaz, imleç çekilince kısa süre sonra kapanır.
  - Kopyala görüntüyü panoya alır ("Kopyalandı"). Düzenle görüntüyü Ekran Alıntısı Aracı'nda açar; açılamazsa Paint'te. Yalnız panoya giden ekran görüntüsünde Düzenle önce geçici bir PNG'ye kaydeder.
  - Tam ekran uygulamada, ada genişlemişken ya da bir panel açıkken önizleme çıkmaz (panel açıksa şerit zaten güncellenir).
  - "Animasyonları azalt" açıksa belirip kaybolma geçişi yok.

## Bizim kararlarımız (kullanıcı özetini onayladı)

- 30 öğe sınırı, yalnız sabitlenenlerin diske yazılması, aynı içeriğin en üste çıkması, ekran görüntüsünün tek kayıt sayılması, şeridin 10 görüntü olması, önizlemenin ~5 sn ve tıklanabilir olması, Düzenle'nin Ekran Alıntısı Aracı'nda açması.
- Satırda tek tek silme (×) ve Delete tuşu (kopyalanan bir şifreyi hemen silebilmek için).
- Düğmeyle açılmış panelde seçimden sonra panelin açık kalması.
- Tam ekranda kısayolun paneli yine açması.
- 16 MB tek öğe sınırı.
- Aynılığın düz metinle ölçülmesi (biçim farkı yeni satır açmaz).
- Sabitlenenlerin diskte şifrelemesiz durması (ayar dosyası gibi kullanıcının klasöründe).
- Masaüstü önde iken kısayolun yalnız kopyalaması.

## Ana tasarımda değişen

- §7.4 ve §11: "Ctrl+Shift+V her yerden açar" → **Win+Alt+V**. Win+V'ye dokunulmaması aynı.
- §7.4 "Tıklayınca yeniden panoya alınır" → Hedef 4'teki iki durumlu davranış.
- §7.5: ekran görüntüsü önizlemesi diğer kısa göstergelerden farklı olarak örtülüyken de tıklanabilir.

## Kısıtlar

- Plan 1–4.1'in kısıtları geçerli: Windows 11 build 26300; `CamAda.Core` WPF'siz ve birim testli; Explorer'a enjeksiyon yok; arayüz Türkçe; Manrope + Segoe Fluent Icons; derleme uyarısız.
- Pano okunurken pano kilitliyse kısa aralıklarla birkaç kez yeniden denenir, pano hiçbir zaman uzun süre açık tutulmaz; büyük görseller arayüz iş parçacığını kilitlemeden işlenir.
- Olay tabanlı: pano dinleyicisi ve klasör izleyicisi; yoklama yok.
- Ayar: `Hotkeys.ClipboardOpen`, varsayılan `"Win+Alt+V"`; geçersiz metin varsayılana döner.
- Canlı denemeler kullanıcının panosunu değiştirirse deneme sonunda eski içerik geri konur; yapıştırma denemesi kullanıcının uygulamalarıyla değil scratchpad'de derlenen bir deneme penceresiyle yapılır; ekran görüntüsü denemesi kullanıcının klasörüne dosya bırakırsa deneme sonunda silinir.

## Yapıcıya bırakılanlar

- Görev bölümü (yaklaşık 4–5 görev; ör. Core geçmiş modeli → dinleyici + düğme + panel → kısayol + yapıştırma → ekran görüntüleri şeridi + önizleme).
- "Koda benzeyen metin" sezgisi (Core'da birim testli), zaman etiketi biçimi, satır ve şerit ölçüleri, panel genişliği, pano simgesi.
- Görsellerin bellekte nasıl tutulacağı (ör. PNG sıkıştırılmış) ve küçük resimlerin nasıl üretileceği.
- Ekran görüntüsü ile pano görselinin eşleştirilme yöntemi ve bekleme süresi.
- Önizlemenin adadaki yerleşimi ve ölçüsü (küçük hap 40 DIP yüksek; küçük resmin okunabilmesi için hap önizleme boyunca büyüyebilir, ada penceresi 160 DIP yüksek).
- Ekran Alıntısı Aracı'nda dosya açmanın yolu (yoksa Paint).
- Saf mantık (geçmiş listesi: ekleme, tekilleştirme, sınır, sabitleme, silme; tür belirleme; zaman etiketi; ekran görüntüsü eşleştirme kararı) için Core testleri; pano, kısayol, yapıştırma ve klasör izleme canlı denemeyle doğrulanır.
