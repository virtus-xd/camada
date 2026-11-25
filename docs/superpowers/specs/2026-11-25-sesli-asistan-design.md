# Sesli asistan "Ada" — tasarım

Tarih: 2026-11-25 · Durum: kullanıcı onaylı (25.11.2026; hafıza eki dahil) · Bağlı tasarım: `2026-11-23-cam-ada-design.md` (§7.1, §7.5, §8)

## Amaç ve neden

Kullanıcı CamAda'ya, uyandırma sözcüğüyle açılan ve genel bir bilgisayar asistanı gibi davranan bir sesli asistan istiyor. Asistan CamAda'nın işlevlerini de bilecek. Gündelik istekleri şunlar:

- "YouTube'u aç", "Google'da … araştır";
- uygulamayı adıyla açmak;
- ses, parlaklık, çıkış ve giriş cihazı gibi Windows işlevleri;
- olabiliyorsa Spotify ve WhatsApp;
- bunların dışındaki soruları (güncel olanlar dahil) yanıtlamak.

**Kullanıcının koyduğu ilk şart performans:** asistan bilgisayarı yormayacak. Bu yüzden yerel dil modeli yok. İş bulutta yapılıyor, Windows işlevleri elle yazılıyor. Proje ileride açık (public) olacağı için kurulumda güveni zedeleyen adım olmayacak (bkz. bildirimlerin iptali). Her kullanıcı kendi API anahtarını girecek.

Bulgular (25.11.2026). Hepsi geçici denemelerle ölçüldü; deneme kodu atıldı, dersler aşağıda.

- **Gemini Live denemesi** (C#, NAudio, ham WebSocket):
  - Türkçe konuşmayı doğru anlıyor; işlevleri doğru değerle seçiyor (YouTube, ses %30, parlaklık %100, Spotify'ı aç, oynat, Not Defteri).
  - `gemini-3.8-live` ile sustuktan sonra ilk ses ~1,1–1,8 sn; güncel bilgi aramalı yanıtta ~3,2–3,6 sn. Eski `gemini-2.5-flash-native-audio` ~2–3 sn.
  - Ses "Kore" (hafif ciddi kadın sesi); kullanıcı beğendi.
- **Dürüstlük:** Talimatsız model yapamadığını yapmış gibi söyledi ("Chrome kapatıldı", "ekranda 1.850 TL yazıyor"). Talimat ve gerçek işlev sonuçlarıyla "yapamıyorum" demeye başladı.
- **Güncel bilgi:** Live modelinin kendi Google araması kullanılmadı. Model, verilen "Google'da ara" işlevini seçti ve eski bilgisiyle yanlış yanıt verdi (dolar 29,8 TL). Ayrı bir `guncel_bilgi` işlevi, `gemini-3.5-flash-lite` + Google araması (REST) ile 1,3–2,5 sn'de doğru yanıt verdi: dolar 50,68 TL, gram altın ~5.700 TL, Galatasaray–Aston Villa 1-0, Ankara hava durumu. İşlev tanımı "yalnız zamanla değişen bilgiler" demeli; yoksa model tarih sorularında da arıyor.
- **Ücretsiz katman yetmiyor:**
  - Google araması hiçbir modelde yok (429).
  - 3.8 Live kapalı; yalnız 2.5 native audio açık.
  - 2.5 metin modelleri yeni kullanıcılara kapatılmış.
  - Kullanıcı ödemeyi açtı (ön ödeme). Ücretli katmanda ayda 5.000 Google araması ücretsiz.
- **Yankı:** Hoparlörde model kendi sesini duyup kendi sözünü kesti. Windows'un yerleşik yankı engelleyicisi (Voice Capture DSP, `CLSID_CWMAudioAEC`, tek kanal AEC, 16 kHz mono) bunu çözdü:
  - Ada konuşurken mikrofona sızan artık RMS ≤ 0,006; kullanıcının araya girişi 0,07–0,16.
  - Google'ın konuşma algısı (varsayılan hassasiyet) söz kesmeyi anında yaptı.
  - SteelSeries Sonar sanal mikrofonu DSP ile ses vermedi; dizüstünün "Mikrofon Dizisi (Realtek)" mikrofonu çalıştı.
- **Uyandırma sözcüğü:** sherpa-onnx anahtar sözcük algılayıcısı (İngilizce gigaspeech 3,3M, int8, eğitimsiz, "HEY ADA" → `▁HE Y ▁A D A`):
  - Kullanıcının "Hey Ada" ve "Hey Eyda" söyleyişlerini yakaladı.
  - 6 dakikada tek yanlış tetiklenme: "Hey adam gel buraya".
  - İki algılayıcı birlikte tek çekirdeğin %5–8'ini kullandı; tek algılayıcı ~%3–4.
- **Maliyet:** 3.8 Live konuşmanın sesini bağlamda tutuyor ve her yanıtta yeniden faturalıyor. 22 yanıtlık oturumda yanıt başı maliyet 0,3 sentten 1,4 sente çıktı. Kısa oturumda yanıt başı ~0,3–0,8 sent.

## Hedefler

1. **Uyanma:** "Hey Ada" (ya da adadaki mikrofon düğmesi, ya da kısayol) bir konuşma oturumu açar. Ada'yla Türkçe, doğal sesle konuşulur.
2. **Söz kesme:** Hoparlörde de çalışır. Ada konuşurken araya girilince Ada hemen susar ve yeni söyleneni dinler.
3. **Sabit işlevler** gerçekten yapılır ve sonucu doğru söylenir (liste aşağıda). Yapılamayan iş için "yapamıyorum" denir.
4. **Güncel sorular** (döviz, altın, hava, maç, haber, fiyat) Google aramasıyla doğru yanıtlanır. Kalıcı bilgiler (tarih, genel kültür) modelin kendi bilgisiyle yanıtlanır.
5. **Adada görünüm:** oturum boyunca "dinliyorum / konuşuyor" durumu, söylenenlerin yazısı ve mikrofonun açık olduğunu gösteren bir işaret.
6. **Boşta yük:** yalnız uyandırma algılayıcısı çalışır (≤ tek çekirdeğin %5'i, ekran kartı yok, en fazla ~60 MB ek bellek). Oturum dışında hiçbir ses internete gitmez.
7. **Anahtar:** Gemini API anahtarı ilk kullanımda adadan girilir ve şifreli saklanır.
8. **Maliyet görünürlüğü:** Asistan panelinde bu ayın tahmini harcaması görünür (Google'ın bildirdiği token sayılarından).
9. **Hafıza:** Ada kullanıcı hakkında önemli bilgileri ve tercihleri (ör. "kulaklık" = Buds2, şehir, düzeltmeler) kısa maddeler olarak hatırlar ve sonraki oturumlarda kullanır. Kaydettiğini her zaman sesli söyler; kullanıcı "unut" diyebilir ya da panelden silebilir.

## Hedef dışı

- Ekranı görmek ya da okumak, fareyi ya da klavyeyi sürmek, tarayıcı sekmesi yönetmek.
- Dosya silmek, taşımak, ad değiştirmek; bir uygulamayı zorla kapatmak.
- WhatsApp'ta mesajı kendiliğinden göndermek ya da mesaj okumak (yalnız hazır yazılmış sohbet açılır).
- Konuşma dökümü ya da özeti saklamak ("dün ne konuşmuştuk"). Her uyanış yeni bir oturumdur; oturumlar arasında yalnız hafıza maddeleri taşınır.
- Türkçe dışındaki diller (model başka dilde sorulursa yanıt verebilir ama hedef değil).
- Yerel dil modeli ya da yerel konuşma tanıma (performans şartı).
- Bildirim okumak (Plan 4.4 iptal; aynı paket kimliği sorunu).

## Olmaması gerekenler (çalışsa bile başarısızlık sayılır)

- Ada'nın yapmadığı bir işi yaptım demesi ya da bilgi uydurması.
- Yanlış anlaşılan bir komutla geri alınamaz bir şey olması (kapatma, yeniden başlatma onaysız yapılırsa).
- Oturum bitmesine rağmen mikrofonun internete akmaya devam etmesi.
- Boştayken fark edilir işlemci, ekran kartı ya da pil tüketimi.
- Ada'nın hoparlörde kendi sesiyle kendi sözünü kesmesi.
- Kurulumun sertifika eklemesi, geliştirici modu istemesi ya da yönetici izni gerektirmesi.

## Yaklaşımlar

**Konuşma yolu:**

| Yol | Nasıl | Neden seçilmedi / seçildi |
|---|---|---|
| Yerel model (Ollama + Whisper + Piper) | Her şey bilgisayarda | 6 GB ekran kartında 5–6 GB tutar, oyunla çakışır. Kullanıcının performans şartına aykırı. |
| A. Zincir: Gemini Flash-Lite (ses girişi) + ElevenLabs sesi | Komut kaydı gönderilir, yanıt metni seslendirilir | Söz kesmeyi kendimiz yazmamız gerekirdi, gecikme daha uzun. Kullanıcı ElevenLabs seslerini beğendi ama Live'ı denedikten sonra Live'ı seçti. |
| B. Ayrı yazıya çevirme + Claude Haiku + ElevenLabs | Üç ayrı servis | Üç anahtar; web araması 1.000'de 10 $. |
| **C. Gemini Live (seçilen)** | Tek canlı bağlantı: model dinler, işlev çağırır, kendi sesiyle konuşur, söz kesmeyi kendisi algılar | En hızlı ve en doğal. Kullanıcı denedi ve seçti. Bedeli: ses Google'ın sesleri, servis "önizleme". |

**Uyandırma sözcüğü:**

| Yol | Neden |
|---|---|
| openWakeWord, kendi modelimizi eğitmek | Onlarca GB veri ve saatlerce eğitim; hazır modellerin lisansı ticari olmayan ve paylaşımlı (CC BY-NC-SA). |
| Porcupine | Her kullanıcının Picovoice anahtarı alması gerekir; Türkçe yok. |
| **sherpa-onnx anahtar sözcük algılayıcısı (seçilen)** | Eğitim yok, sözcük metinle verilir, Apache 2.0, C# paketi var. Canlıda doğrulandı. |

**Yankı:**

| Yol | Neden |
|---|---|
| Ada konuşurken mikrofonu kapatmak | Yankı biter ama söz kesme olmaz. Yalnız yedek olarak kalır. |
| Yalnız kulaklıkla kullanmak | Kullanıcı hoparlörle kullanıyor. |
| **Windows Voice Capture DSP (seçilen)** | Windows'ta hazır, ek kurulum yok. Canlıda doğrulandı. |

## Kullanıcının verdiği kararlar

- **Performans ilk şart.** Yerel dil modeli yok; bulut + elle yazılmış işlevler.
- **Gemini Live**, ses **Kore**. Söz kesme şart.
- **Uyandırma sözcüğü "Hey Ada".** "Hey adam" gibi nadir yanlış tetiklenmeler kabul edilebilir.
- **Ödeme açık:** Google AI Studio'da ön ödemeli ücretli katman. Güncel bilgi için şart.
- **Konuşma akışı:** Ada yanıtladıktan sonra ~8 sn sessizlik olursa ya da "tamam, teşekkürler" denirse oturum kapanır.
- **İstenen işlevler:** site açma, Google'da arama, uygulama açma, ses, parlaklık, çıkış/giriş cihazı, Spotify, WhatsApp (yalnız hazır yazılmış sohbet açma; kişiler ayarda). Kapatma ve yeniden başlatma "emin misin?" ister; dosya silme yok.
- **Uyandırma dışı tetikleyiciler:** adada mikrofon düğmesi ve bir kısayol.
- **Anahtar girişi:** adadan küçük bir panelle, şifreli saklanır.
- **Hafıza:** Ada neyi hatırlayacağına kendisi de karar verir. Kullanıcı önemli görünen bir bilgi söylediğinde yakalaması isteniyor; kaydettiğini sesli söyler. Token kullanımını azaltmaz, küçük bir artış getirir (~500 tokenlık hafıza yanıt başı ~0,04 sent, ayda ~0,3–0,4 $); amaç kişiselleştirme.

## Davranış

### Uyanma ve oturum

1. **Boşta:** Uyandırma algılayıcısı seçili mikrofonu 16 kHz'te dinler. Ses hiçbir yere gitmez.
2. **Tetiklenme** ("Hey Ada", mikrofon düğmesi ya da kısayol):
   - Ada adada "Dinliyorum" hâline geçer ve kısa, yumuşak bir bip sesi çalar.
   - Algılayıcının mikrofonu bırakılır, yankı engelleyici mikrofonu açar.
   - Gemini Live bağlantısı kurulur (~0,5 sn).
   - Tetiklenmeden sonra söylenenler kaybolmaz. Bağlantı hazır olana kadar ses arabellekte tutulur, sonra sırayla gönderilir. Böylece "Hey Ada, sesi kıs" tek nefeste söylenebilir.
3. **Konuşma:**
   - Mikrofon sürekli açık (yankı engelleyicili).
   - Sustuğunu Google algılar; Ada yanıt verir, gerekirse işlev çağırır.
   - Araya girilince Google "kesildi" der, çalan ses anında durur ve kalan yanıt atılır.
4. **Oturumun kapanması:**
   - Ada'nın yanıtı bittikten sonra 8 sn konuşma olmazsa.
   - Kullanıcı vedalaşınca ("tamam, teşekkürler", "görüşürüz"): model `konusmayi_bitir` işlevini çağırır, Ada kısa bir veda eder.
   - Yanlış tetiklenmede, uyandıktan sonra 6 sn içinde hiç konuşma olmazsa sessizce.
   - Mikrofon düğmesine ya da kısayola yeniden basılınca hemen.
   - Oturum 10 dk'yı aşarsa (Google'ın oturum sınırından önce) Ada "uzun sürdü, yeniden çağırabilirsin" diyip kapanır.
   - Kapanışta yankı engelleyici kapanır, bağlantı kapanır, algılayıcı yeniden dinlemeye başlar. Mikrofonun internete gitmesi biter.
5. **Her uyanış yeni oturumdur.** Uzun sohbetlerde maliyeti sınırlamak için Google'ın bağlam sıkıştırması (kayan pencere) açık olur.

### Tam ekran

Tam ekran bir uygulama varken (oyun, video) uyandırma algılayıcısı duraklar. Oyun ve video sesi yanlış tetiklemesin, Ada oyunun üstüne konuşmasın diye. Kısayol çalışmaya devam eder ve tam ekranda da bir oturum açabilir; bu durumda yalnız ses vardır, ada görünmez. Ayardan değiştirilebilir (`ListenInFullScreen`).

### Ada konuşurken diğer sesler

Oturum boyunca öbür uygulamaların sesi kısılır, oturum bitince geri gelir (müzik çalıyorsa Ada duyulsun diye). Windows'un iletişim kısması bunu kendiliğinden yaparsa o kullanılır; yapmazsa uygulama ses oturumları üzerinden yapılır (yapıcının ölçümü).

### Adada

```
Dinliyorum:     (  ●  Dinliyorum…                                   14:32 )
Sen konuşurken: (  ●  "sesi yüzde otuza getir"                            )
Ada konuşurken: (  ✦  Ses düzeyini yüzde otuza getirdim.                   )
İş yapılınca:   (  🔊 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  %30             )
Hata:           (  ⚠  Ada'ya ulaşılamıyor — internet yok                   )
```

- Oturum boyunca ada, ekran görüntüsü önizlemesi gibi genişler ve son söylenen cümleyi gösterir.
  - Kullanıcının sözü, Google'ın verdiği canlı yazıdan alınır.
  - Ada'nın sözü, yanıtın yazılı dökümünden alınır.
- Ses ve parlaklık işlerinde mevcut kısa gösterge de çıkar.
- Mikrofon açıkken solda kırmızımsı bir nokta durur (gizlilik işareti). Ada yalnız dinlerken ya da düşünürken hafifçe nabız atar.
- Adanın sağ tık menüsünde **"Sesli asistan"** açma/kapama ve **"Ada ayarları…"** (anahtar ve aylık harcama) bulunur.

### İşlevler (v1)

Her işlev gerçek sonucu döndürür: başarılıysa ne yapıldığı, değilse neden olmadığı. Model yalnız bu sonuca göre konuşur.

| İşlev | Ne yapar | CamAda'daki karşılığı |
|---|---|---|
| `ses_ayarla(yuzde)`, `ses_degistir(fark)`, `sessize_al(sessiz)` | Ana ses | `AudioService.SetVolume` / `SetMuted` |
| `cikis_cihazi_sec(ad)` | Çıkış cihazı ("kulaklık", "hoparlör", "Buds") | `AudioService.Outputs` + `SetDefaultOutput`, adla eşleme |
| `giris_cihazi_sec(ad)` | Varsayılan mikrofon | **Yeni:** kayıt cihazı listesi + varsayılan yapma (`PolicyConfig`) |
| `mikrofon(acik)` | Mikrofonu kapat/aç | `AudioService.Mic` / `ToggleMic`. Kapatılırsa oturum da veda edip biter. |
| `parlaklik_ayarla(yuzde)`, `parlaklik_degistir(fark)` | Ekran parlaklığı | `BrightnessService.Set` (dahili ekran; tek harici varsa o) |
| `wifi(acik)`, `bluetooth(acik)` | Telsizler | `RadioService.SetAsync` |
| `medya_kontrol(komut)` | oynat / duraklat / sonraki / önceki | `NowPlayingService`. Oynat/duraklat şimdiki duruma göre. |
| `spotify_cal(sorgu)` | Şarkı, sanatçı ya da liste çal | **Yeni:** Spotify Web API arama + çalma. Yeni izin (`user-modify-playback-state`) için bir kez yeniden bağlanmak gerekir. Spotify kapalıysa önce açılır. |
| `uygulama_ac(ad)` | Uygulamayı adıyla açar | `AppIndex` listesi, Türkçe harfe duyarsız eşleme; birden fazla aday varsa sorar |
| `uygulama_kapat(ad)` | Uygulamanın pencerelerini kapatır | **Yeni:** pencerelere kapatma isteği (WM_CLOSE). Uygulama "kaydet?" diye sorabilir; asla zorla öldürmez. |
| `web_sitesi_ac(adres)` | Siteyi varsayılan tarayıcıda açar | — |
| `tarayicida_google_ac(sorgu)` | Yalnız açıkça "tarayıcıda/Google'da aç" denince | — |
| `guncel_bilgi(soru)` | Zamanla değişen bilgi; Google'da aranır, kaynak adıyla döner | **Yeni:** `gemini-3.5-flash-lite` + `google_search` (REST) |
| `whatsapp_mesaj(kisi, mesaj)` | Ayardaki kişiyle sohbeti mesaj hazır yazılmış açar | `whatsapp://send?phone=…&text=…`. Gönderme kullanıcıda; kişi yoksa "rehberde yok". |
| `ekran_goruntusu()`, `kilitle()`, `uyku()` | Bilinen düğmeler | `IQuickLauncher` |
| `bilgisayari_kapat(yeniden_baslat)` | Kapatma / yeniden başlatma | İki adımlı koruma (aşağıda) |
| `konusmayi_bitir()` | Vedalaşınca oturumu kapatır | Oturum yöneticisi |
| `hatirla(konu, bilgi)`, `unut(konu)` | Hafızaya madde ekler/günceller, siler | **Yeni:** hafıza deposu (Core'da kurallar, şifreli dosya) |

**Kapatma koruması:** İlk çağrı hiçbir şey yapmaz; işlev "onay gerekiyor; kullanıcıya sor" döndürür. Model sorar. Kullanıcı 30 sn içinde onaylarsa ikinci çağrı yapılır. Böylece modelin davranışından bağımsız olarak onaysız kapatma olmaz.

**Talimat (system instruction), denemede doğrulanan ilkeler:**
- Her zaman Türkçe; kısa, doğal, hafif ciddi bir tonla.
- İşlem yapıldıktan sonra tek kısa cümleyle söyler.
- Yalnız işlevin "tamam" ya da sonuç döndürdüğü işi yapılmış sayar. Uygun işlev yoksa ya da hata dönerse "yapamıyorum" der.
- Ekranı göremez, sayfaları okuyamaz.
- Kalıcı bilgiyi kendi bilgisiyle, zamanla değişeni `guncel_bilgi` ile yanıtlar; o da başarısızsa tahmin yürütmez.
- Kapatmadan önce sorar.
- Kendisine söylenmeyen konuşmaları yanıtlamaz.
- Bugünün tarihi her oturumda verilir.
- Hafıza listesi verilir. Önemli kişisel bilgi, tercih ya da düzeltmeyi kendiliğinden kaydeder ve kaydettiğini söyler; hassas bilgiyi kaydetmez.

### Hafıza

- **Ne tutulur:** Kullanıcı hakkında kalıcı ve işe yarar bilgiler ve tercihler. Örnekler: adı, yaşadığı şehir, "kulaklık" denince hangi cihaz, "müzik" denince Spotify, sık yazdığı kişiler, "ben 'Dosyalar' deyince Dosya Gezgini'ni kastediyorum" gibi düzeltmeler. Konuşma dökümü tutulmaz.
- **Nasıl:** İki işlev.
  - `hatirla(konu, bilgi)`: `konu` kısa bir anahtardır ("kulaklık", "şehir"). Aynı konu yeniden kaydedilirse eski bilgi güncellenir; Ankara → İzmir gibi.
  - `unut(konu)`.
- **Ne zaman kaydeder:** Ada kendisi karar verir. Kullanıcı kendisi hakkında önemli görünen bir şey söylediğinde, bir tercihini belirttiğinde ya da Ada'yı düzelttiğinde kaydeder. Kullanıcı "bunu hatırla" derse her zaman kaydeder.
- **Kaydettiğini her zaman sesli söyler:** "Bunu aklımda tutuyorum: kulaklık deyince Buds2." Habersiz kayıt yok. "Bunu unut" / "benim hakkımda ne biliyorsun?" soruları çalışır.
- **Hassas bilgi:** Şifre, kart numarası, IBAN ve kimlik numarası asla kaydedilmez. Talimat bunu söyler; ayrıca Core'daki bir süzgeç bu tür içeriği reddeder ve işlev "kaydedilmedi: hassas bilgi" döndürür.
- **Sınır:** En fazla 50 madde, madde başına en fazla 200 karakter. Dolunca en eski güncellenen madde çıkar ve Ada bunu söyler.
- **Kullanım:** Her oturumun başında talimata "Kullanıcı hakkında bildiklerin" başlığıyla kısa bir liste olarak eklenir. Model gerektiğinde kullanır (ör. "kulaklığa geç" → Buds2, "hava nasıl" → kayıtlı şehir).
- **Görme ve düzenleme:** Ada ayarları panelinde liste görünür; madde madde silinebilir ya da "Hafızayı temizle" ile tümü silinir.
- **Saklama:** `%LOCALAPPDATA%\CamAda\ada-hafiza.bin`, Windows kullanıcı şifrelemesiyle (DPAPI); anahtar ve Spotify belirteciyle aynı yöntem. Bozuk ya da okunamayan dosya boş hafıza sayılır ve üzerine yazılmadan önce yedeklenir.
- **Gizlilik notu:** Hafıza her oturumda Google'a talimatın parçası olarak gider (ücretli katmanda ürün geliştirmede kullanılmıyor).

### Anahtar ve ayarlar paneli

- Asistan kurulmamışken mikrofon düğmesine basmak (ya da menüden "Ada ayarları…") küçük bir panel açar:
  - kısa açıklama ve Google AI Studio bağlantısı;
  - gizli bir anahtar kutusu ve **Kaydet**;
  - anahtar doğrulanınca: **"Sesli asistan açık"** anahtarı, seçili mikrofon, bu ayın tahmini harcaması.
- **Saklama:** Anahtar Windows'un kullanıcı şifrelemesiyle (DPAPI) `%LOCALAPPDATA%\CamAda\gemini-key.bin` dosyasına yazılır; Spotify belirtecinin yöntemiyle aynı.
- **Denemeden kalan düz metin anahtar:** `%LOCALAPPDATA%\CamAda\gemini-anahtar.txt` varsa bir kez içeri alınır ve dosya silinir.
- **Doğrulama:** Kaydedince bir Live kurulum denetimi yapılır (ses göndermeden). Geçersiz anahtar, ödenmemiş hesap ve kota ayrı mesajlarla söylenir.

### Hata durumları

| Durum | Kullanıcı ne görür/duyar |
|---|---|
| İnternet yok / bağlantı kurulamadı | Adada "Ada'ya ulaşılamıyor — internet yok", kısa hata sesi; boşa döner |
| Anahtar geçersiz (400/403) | "Gemini anahtarı geçersiz" ve ayarlar paneli |
| Bakiye bitti (402) | "Gemini bakiyesi bitti — AI Studio'dan yükle" |
| Kota ya da harcama sınırı (429) | "Bu ayın Gemini sınırına ulaşıldı" |
| Oturum ortasında bağlantı koptu | Ada'nın sesi durur, adada "Bağlantı koptu"; boşa döner |
| `guncel_bilgi` başarısız | İşlev hata döndürür; Ada "güncel bilgiye şu an ulaşamıyorum" der |
| Yankı engelleyici açılmadı | Yedek: Ada konuşurken mikrofon susar (yarı çift yönlü); söz kesme yok, bir kez bilgilendirilir |
| Mikrofon yok | Asistan duraklar; "Mikrofon bulunamadı" |

## Bizim kararlarımız

- **Model:** `models/gemini-3.8-live`; `guncel_bilgi` için `gemini-3.5-flash-lite`. İkisi de ayardan değişir. 3.8 Live düşünme ayarlarını kabul etmiyor; düşünme varsayılan kalır, düşünme metinleri gösterilmez.
- **Google SDK'sı yok:** Live protokolü ham WebSocket ile, arama REST ile yazılır (denemede çalıştı, bağımlılık eklemez). Anahtar URL yerine `x-goog-api-key` başlığıyla gider; WebSocket bağlantısında Google başka yol sunmuyorsa URL'de kalır ama hiçbir günlüğe yazılmaz.
- **Yankı engelleyici:** Voice Capture DSP kaynak modunda; referans, Ada'nın çaldığı çıkış cihazı. .NET'te DSP'ye tampon vermek için eski COM köprüsü çalışmadı ("Typelib export" hatası). Kaynak üretimli COM (`[GeneratedComInterface]`, `[GeneratedComClass]`, `StrategyBasedComWrappers`) ve elle bellekte `DMO_OUTPUT_DATA_BUFFER` kullanılır.
- **Uyandırma:** sherpa-onnx (C# paketi), gigaspeech 3,3M int8 modeli uygulamayla birlikte gelir. Anahtar sözcük satırı derlemede hazırlanır; çalışırken `sentencepiece` gerekmez. Eşik varsayılanı 0,20; aralığı ayardan.
- **Mikrofon seçimi:** ayar `Assistant.Microphone` (ad parçası); boşsa Windows'un varsayılan iletişim mikrofonu. Bu kullanıcıda "Realtek"e ayarlanır, çünkü Sonar sanal mikrofonu DSP ile ses vermedi.
- **Kısayol:** varsayılan **Win+Alt+A**, ayardan değişir. Başka uygulama almışsa adada bir kez "kullanılamıyor".
- **Süreler:** sessizlik 8 sn, yanlış tetiklenme 6 sn, oturum en fazla 10 dk (ayardan).
- **Konuşmalar kaydedilmez.** Yalnız aylık token sayacı tutulur: ay, giriş/çıkış tokenları, tahmini $.
- **Fiyat tablosu Core'da ve tarihli** (kip başına giriş/çıkış $/1M token; ör. 3.8 Live sesi giriş 3 $, çıkış 12 $). Google bazı fiyatları 1 Ocak 2027'de değiştiriyor; tablo tarihe göre seçilir. Tahmin yalnız bilgi amaçlıdır; asıl tutar AI Studio'dadır.
- **Ayarlar:** `AppSettings.Assistant` kaydı:
  - `Enabled` (varsayılan kapalı; anahtar girilince açılır);
  - `Model`, `LookupModel`, `Voice`, `Microphone`, `WakeThreshold`;
  - `SilenceSeconds`, `NoSpeechSeconds`, `MaxSessionMinutes`;
  - `Hotkey`, `ListenInFullScreen`, `DuckOthers`;
  - `WhatsAppContacts` (ad → telefon).
  - Aralık dışı değerler varsayılana döner (mevcut `SettingsStore` kuralı).

## Ana tasarımda değişen

- §7.1 / §7.5: adaya "Ada" oturum durumları eklenir (dinliyor, konuşuyor, hata).
- §8 (teknik yön): sesli asistan için Gemini Live, sherpa-onnx ve Windows Voice Capture DSP eklenir.
- §3: "yerel dil modeli" ve "bildirim okumak" kapsam dışı olarak yazılır.

## Kısıtlar

- **Performans bütçesi:**
  - Boşta uyandırma algılayıcısı ≤ tek çekirdeğin %5'i (ölçülen ~%3–4); ek bellek ≤ ~60 MB.
  - Ekran kartı kullanılmaz.
  - Oturumda yankı engelleyici + WebSocket + çalma birkaç yüzde.
  - Boştayken başka zamanlayıcı yok.
- **Gizlilik:** Mikrofon yalnız oturumda Google'a gider, işaret adada görünür. Uyandırma sesi bilgisayarda kalır. Konuşmalar diske yazılmaz. Hafıza maddeleri bilgisayarda şifreli durur, her oturumda talimatla Google'a gider.
- **Maliyet:** Kısa oturum, bağlam sıkıştırma ve 10 dk sınırı. Tahmini aylık harcama panelde görünür. Kullanıcının AI Studio harcama sınırı (ör. 10 $) asıl güvencedir.
- **Güvenlik:** Hiçbir işlev dosya silmez, zorla kapatmaz, mesaj göndermez; kapatma iki adımlı. Anahtar asla günlüğe yazılmaz.
- **Bağımlılıklar:**
  - sherpa-onnx C# paketi (Apache 2.0, kendi ONNX çalışma zamanıyla);
  - gigaspeech KWS modeli (Apache 2.0), lisans notu uygulamayla gelir;
  - NAudio (mevcut).
- **Test:**
  - Core'da birim testli saf mantık:
    - işlev argümanlarını doğrulama ve normalleştirme (yüzde sınırı, göreli değişim, adres tamamlama);
    - uygulama ve cihaz adı eşleme (Türkçe büyük/küçük harf);
    - WhatsApp bağlantısı ve telefon biçimi;
    - kapatma korumasının zaman penceresi;
    - oturum kapanış kuralları (sahte saatle);
    - Live iletilerinin ayrıştırılması (JSON örnekleriyle);
    - token → maliyet hesabı;
  - hafıza deposu (ekleme, aynı konuyu güncelleme, silme, 50 madde sınırı, hassas bilgi süzgeci, talimata yazılan liste);
    - ayar temizleme.
  - Ses, WebSocket, yankı engelleyici ve uyandırma canlı deneme listesiyle denenir.
  - Denemede gerçek işlem yapmayan işlev olmayacak. Denemede boş kalan bir işlev varsa kendini "deneme: yapılmadı" diye bildirir; ilk denemedeki "yaptım diyor ama yapmıyor" karışıklığı bundandı.
- **Canlı deneme kuralları** (önceki planlardan):
  - Kullanıcının sesini ve uygulamalarını değiştiren her deneme önce durumu kaydeder, sonra geri yükler.
  - Kapatma ve yeniden başlatma yalnız sahte başlatıcıyla denenir.
  - Wi‑Fi'yi kapatan deneme aynı komutta geri açar.

## Yapıcıya bırakılanlar

- Ada'nın adadaki görünüm ölçüleri, glifler, bip sesi; nabız animasyonu (30 kare/sn sınırı, mevcut `AnimationTicker`).
- Uygulama ve cihaz adı eşleme ayrıntısı (ör. sözcük başı eşleşme, "Dosya Gezgini" ↔ explorer).
- Diğer sesleri kısma yöntemi (Windows iletişim kısması ya da uygulama oturumları) ve kısma oranı.
- Uyandırma eşiğinin son ayarı (canlı ölçümle) ve "Hey Ada" dışında ek söyleyiş satırı gerekip gerekmediği.
- Live kurulum alanlarının güncel adları (`realtimeInputConfig`, `contextWindowCompression`, `outputAudioTranscription` …): Google önizlemede değiştirebilir, yapıcı resmi belgeyle doğrular.
- sherpa-onnx C# paketinin tam adı ve sürümü; model dosyalarının uygulamada nerede durduğu.
- Hata metinlerinin son hâli.
