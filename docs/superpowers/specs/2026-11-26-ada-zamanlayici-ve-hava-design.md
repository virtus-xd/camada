# Ada'da zamanlayıcı, hatırlatıcı ve hava durumu — tasarım

Tarih: 26.11.2026 · Kapsam: Ada'ya (Plan 5) iki ekleme

## Amaç ve neden

1. **Zamanlayıcı ve hatırlatıcı:** Kullanıcı Ada'ya hem kısa süreler ("10 dakika sonra makarna için haber ver") hem ileri
   saatler ("yarın 9'da toplantıyı hatırlat") kurdurmak istiyor. Bugün Ada'da böyle bir şey yok. Süre dolunca Ada kendi sesiyle
   söyler (kullanıcı kararı).
2. **Hava durumu:** Bugün "hava nasıl" sorusu `guncel_bilgi` ile Google aramasına gidiyor (ücretli, ~1,3–2,5 sn). Ada bunu adanın
   zaten kullandığı ücretsiz Open-Meteo servisinden yanıtlarsa daha hızlı ve ücretsiz olur. "Yarın yağmur var mı" ve başka
   şehirler de sorulabilsin.

## Hedefler

- "N dakika/saat sonra" ve "saat HH:MM'de (bugün, yarın ya da bir tarihte)" hatırlatma kurmak. Ada kurduğu zamanı doğrular.
- Süre dolunca ton, adada yazı ve Ada'nın kendi sesiyle söylemesi.
- Hatırlatmaları sormak, iptal etmek, son dolanı ertelemek ("5 dakika ertele").
- Hatırlatmalar yeniden başlatmada kaybolmaz. Bilgisayar uykudayken dolanlar uyanınca söylenir.
- Bir saatten kısa kalan zamanlayıcı küçük adada geri sayar.
- Ada'nın hava sorularını Open-Meteo'dan yanıtlaması: anlık, bugün, yarın, 7 güne kadar, başka şehir.

## Hedef dışı

- Tekrarlanan hatırlatmalar ("her gün", "her pazartesi"). İstenirse sonra eklenir.
- Windows bildirimleri (Plan 4.4 iptal edildi: imza gerektiriyor, proje public olacak).
- Uyarı sırasında mikrofonu açıp yanıt beklemek. Erteleme "Hey Ada, 5 dakika ertele" ile yapılır.
- Hava durumunda saatlik tahmin, rüzgâr, nem, uyarılar.
- CamAda kapalıyken hatırlatma.

## Olmaması gerekenler

- Kullanıcının istemediği anda mikrofonun Google'a açılması. Uyarı oturumunda mikrofon hiç açılmaz.
- Hatırlatmanın sessizce kaybolması: yeniden başlatma, uyku, Ada'nın o an kapalı ya da internetin yok olması. İnternet ya da
  anahtar yoksa en azından ton ve yazı çıkar.
- Ada'nın kurulmamış ya da yanlış saate kurulmuş bir hatırlatmayı "kurdum" demesi. Saati uygulama hesaplar ve sonuç gerçek
  zamanı söyler.

## Kısıtlar

- Ada'nın sesi Gemini Live'dan gelir. Oturum dışında konuşmak için kısa bir oturum gerekir. Uyarı başına ~0,002 $, internet
  gerekir.
- Talimata bugün yalnız tarih giriyor; model saati bilmiyor. Bu yüzden saati uygulama hesaplar. Ayrıca talimata oturum başındaki
  saat de eklenir (benim kararım: "akşam", "3 saat sonra" gibi ifadeleri model daha iyi çözsün).
- Kilitte ve uykuda Ada `Stop` edilir (dinleme ve oturum biter). Uyarı yalnız konuşur, dinlemez.
- Adanın hava servisi (`WeatherService`) yalnız kayıtlı şehrin anlık sıcaklığını ve hava kodunu çeker (30 dk'da bir). Open-Meteo
  aynı uçta günlük tahmin de veriyor (`daily=` alanları) ve ücretsiz, anahtarsız. Şehir adından konum için Open-Meteo'nun ad
  araması (`OpenMeteo.GeocodeUrl`) zaten projede.

## Yaklaşımlar

| Yol | Ne kazandırır | Bedeli |
|---|---|---|
| **A. Uygulama içi zamanlayıcı, Ada kısa bir oturumla söyler (seçilen)** | Her şey CamAda'da. Ada'nın sesi. Mevcut parçalar (Live istemcisi, yazılı not, ada göstergesi) kullanılır. | CamAda kapalıyken çalışmaz. Uyarı başına ~0,002 $. |
| B. Windows Görev Zamanlayıcı | CamAda kapalıyken de tetikler. | Sisteme görev yazar (public projede güven sorunu). Konuşmak için yine CamAda gerekir. |
| C. Windows bildirimi | Sistemin bildirim merkezi. | Plan 4.4'te iptal edildi (imza gerektiriyor). |

Uyarının sesi için kullanıcı A'yı (Ada'nın kendi sesi) seçti. B'ye (yalnız ton ve yazı) ve C'ye (Windows'un Tolga sesi) karşı.
Hava için tek mantıklı yol Open-Meteo'yu doğrudan kullanmak. Google araması pahalı ve yavaş, başka bir ücretsiz servis
eklemenin anlamı yok.

## Tasarım

### 1. Hatırlatmaları kurma ve yönetme (Ada işlevleri)

| İşlev | Ne yapar |
|---|---|
| `hatirlatici_kur(metin, dakika?, saat?, tarih?)` | `dakika` (1–1440) ya da `saat` ("HH:MM", isteğe bağlı `tarih` "YYYY-MM-DD"). Saat verilip tarih verilmezse bugün, saat geçtiyse yarın. Sonuç gerçek zamanı söyler: "tamam: yarın 27 Kasım Cuma 09.00'da 'toplantı' hatırlatılacak". |
| `hatirlaticilari_listele()` | Bekleyenler, yakından uzağa: "makarna: 9 dakika sonra (14.41); toplantı: yarın 09.00". Yoksa "bekleyen hatırlatma yok". |
| `hatirlatici_iptal(metin)` | Metne en çok benzeyeni siler (projedeki ad eşleştirmesiyle). `metin` "hepsi" ise hepsini. Birden çok eşleşme varsa hangisi olduğunu sorar. |
| `hatirlatici_ertele(dakika)` | Son 10 dakikada dolan hatırlatmayı `dakika` sonra yeniden kurar. Yoksa hata. |

- **Sınırlar (benim kararım):** en çok 20 bekleyen hatırlatma, en çok 30 gün ileri, metin en çok 100 karakter. Aşılırsa hata ve
  nedeni.
- **Saklama (benim kararım):** `%LOCALAPPDATA%\CamAda\ada-hatirlatmalar.bin`, DPAPI (hafıza gibi; hatırlatma metni kişisel
  olabilir). Her değişiklikte yazılır. Yazılamazsa işlev hata döner (Ada "kurdum" demez).
- **Talimat:** "Kullanıcı süre ya da saat söyleyip haber vermeni ya da hatırlatmanı isterse hatirlatici_kur çağır. Saati kendin
  hesaplama, işlevin döndürdüğü zamanı söyle."

### 2. Süre dolunca

- **Saat:** Hatırlatmalar duvar saatine göre denetlenir (birkaç saniyede bir ve uykudan uyanınca). Böylece uyku ve saat
  değişikliği kaçırılmaz.
- **Uyarı:**
  1. Ton çalar (yeni ton, ötekilerden farklı).
  2. Adada "Süre doldu: makarna" görünür (8 sn).
  3. Ada söyler: "Süre doldu: makarna."
  4. Aynı anda birden çok hatırlatma dolduysa tek uyarıda hepsi söylenir.
- **Ada'nın sesi:**
  - **Oturum yokken:** Kısa bir *söyleme oturumu* açılır: Live'a bağlanılır, kısa bir talimat (işlevsiz) ve yazılı not
    gönderilir, Ada'nın yanıtı çalınır, oturum kapanır. Mikrofon açılmaz. Maliyet kullanım kaydına girer.
  - **Oturum varken:** Aynı oturuma yazılı not gider ("hatırlatma zamanı geldi: makarna; yalnız 'Süre doldu: makarna.' de").
  - **Not alınıyorsa:** Not bitince söylenir.
  - **Söyleme oturumu sırasında "Hey Ada" ya da kısayol:** Söyleme oturumu biter, normal oturum açılır (yazı adada zaten
    görünmüştür).
- **Ada'nın sesi yoksa** (Ada kapalı, anahtar yok, internet yok, Live hatası): yalnız ton ve yazı. Hatırlatma "dolmuş" sayılır,
  yinelenmez.
- **Tam ekranda:** Ada söyler (ses), ada gizli olduğundan yazı görünmez.
- **Kilitliyken (benim kararım):** Ada söyler (yalnız ses, mikrofon kapalı). Mutfak zamanlayıcısında kullanıcı genelde
  yakındadır.
- **Uykudan uyanınca:** Uykudayken dolanlar "kaçırılan" olarak hemen söylenir ("Kaçırılan hatırlatma: toplantı, 09.00").
- **CamAda açılınca (benim kararım):** Kapalıyken dolanlar da aynı biçimde "kaçırılan" olarak söylenir; sessizce silinmez.
- **Ada durdurulmuşken** (kilit, kullanıcı Ada'yı kapatmamışsa): Söyleme oturumu yine açılabilir. Durdurma yalnız dinlemeyi ve
  normal oturumu kapsar.

### 3. Adada geri sayım

Bir saatten az kalan en yakın zamanlayıcı, küçük adada saatin solunda geri sayar. Daha uzun hatırlatmalar görünmez.

```
( ♪ Şarkı adı …        ⏱ 9:41   14:32 )
```

Biçim (benim kararım): 10 dakikadan fazlaysa "⏱ 42 dk", altındaysa "⏱ 9:41" (saniyeli). Erişilebilirlik adı "Zamanlayıcı:
makarna, 9 dakika 41 saniye". Genişlemiş adada görünmez.

### 4. Hava durumu (Ada işlevi)

- **İşlev:** `hava_durumu(sehir?, gun?)`.
  - `sehir` boşsa adanın kayıtlı şehri (`Weather.Name`, koordinatlar kayıtlı), doluysa Open-Meteo ad aramasıyla bulunur.
  - `gun`: "bugün", "yarın", "YYYY-MM-DD" (7 gün içinde) ya da "hafta".
- **İstek:** Open-Meteo forecast: `current=temperature_2m,weather_code` ve
  `daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max`, `timezone=auto`, `forecast_days=7`.
- **Sonuç modele (örnek):**
  - "tamam: Ankara, şimdi 8°, parçalı bulutlu; bugün en yüksek 12°, en düşük 3°, yağış olasılığı %20"
  - "tamam: İzmir, yarın (27 Kasım Cuma): hafif yağmur, en yüksek 19°, en düşük 11°, yağış olasılığı %70"
  - "hafta" için 7 satır.
- **Hata:** Şehir bulunamazsa "hata: 'X' adlı bir yer bulunamadı". İnternet yoksa "hata: hava durumuna ulaşılamadı".
- **Talimat ve işlev açıklamaları:** Hava soruları `hava_durumu` ile yanıtlanır. `guncel_bilgi` açıklamasından ve talimattaki
  listeden "hava" çıkar. Döviz, haber gibi öbür güncel sorular bugünkü gibi.
- Hava kodlarının Türkçe adları için adanın mevcut `WeatherText.Describe`'ı kullanılır.

## Kullanıcının verdiği kararlar

- Hem kısa süreler hem ertesi gün.
- Süre dolunca Ada kendi sesiyle söyler (A).
- Önerilen davranışların hepsi ("tamam, böyle olsun"): mikrofonsuz uyarı ve "Hey Ada, 5 dakika ertele"; oturumdayken aynı
  oturumda, not bitince; tam ekranda da söyler; bir saatten kısa olan adada geri sayar; diske yazılır, uykuda dolanlar uyanınca
  söylenir; tekrarlanan hatırlatma kapsam dışı; hava için anlık, bugün, yarın, 7 gün ve başka şehirler; öbür güncel sorular
  Google'da.

## Benim kararlarım (kullanıcı düzeltebilir)

- Talimata oturum başındaki saat de eklenir.
- Sınırlar: 20 bekleyen, 30 gün ileri, 100 karakter.
- Saklama DPAPI ile, hafıza gibi.
- Saat verilip tarih verilmezse bugün; geçtiyse yarın.
- Ertele yalnız son 10 dakikada dolanı ertelemek için.
- Kilitliyken de söyler.
- CamAda kapalıyken dolanlar açılışta "kaçırılan" olarak söylenir.
- Yazı adada 8 sn; geri sayım biçimi "42 dk" / "9:41".
- Söyleme oturumu kısa talimatlı ve işlevsiz (ucuz ve öngörülebilir).

## Yapana bırakılanlar

- Tonun sesi.
- Denetim aralığı (birkaç saniye).
- Söyleme oturumunun kapanış zamanlaması (yanıt bitince ve en çok ~15 sn).
- Hava sonucunun önbelleği (ör. 10 dk).
- Dosya biçimi ve sürümleme.
- Debug denemeleri (sahte saat ile dolma, söyleme oturumu, Open-Meteo yanıtı).
