# Ada'da misafir kipi (kalabalıkta susma) — tasarım

Tarih: 26.11.2026 · Kapsam: Ada'ya (Plan 5) ekleme

## Amaç ve neden

Kullanıcı Ada'ya bir şey söylerken odaya biri girip konuşuyor. Ada onun sözlerini de duyup komutu ona göre alıyor. Kullanıcı o
kişiye yanıt verince Ada bunun kendisine söylendiğini sanıyor. Kullanıcının yaşadığı iki durum:

1. **Komutun ortasında başkasının sözü:** başkasının sesi komuta karışıyor.
2. **Kullanıcının başkasına yanıtı:** kullanıcının kendi sesi, ama Ada'ya söylenmemiş.

Bugün oturum yalnız sessizlikte kapanıyor (Ada'nın yanıtından sonra 8 sn). Google'ın duyduğu her söz, kimden gelirse gelsin, sayacı
sıfırlıyor. Odada konuşma sürdükçe oturum 10 dk sınırına kadar açık kalıyor, ses Google'a gidiyor, her yanıtta tüm geçmiş yeniden
faturalanıyor. "Bana mı söylendi?" kararını yalnız model veriyor. Talimattaki "sana söylenmeyen konuşmaları yanıtlama" satırı 25.11
denemesinde yetmedi: Ada odadaki konuşmaya yanıt verdi. Google sesleri ayırt etmiyor.

## Hedefler

- Ada kullanıcının sesini tanısın. Başkasının sesi Google'a hiç gitmesin: komuta karışmasın, oturumu açık tutmasın, Ada'nın
  sözünü kesmesin.
- Ada başka bir ses duyunca **misafir kipine** geçsin. Bu kipte yalnız "Ada" ile başlayan sözler Google'a gitsin. Böylece
  kullanıcının misafire verdiği yanıt da gitmez (2. durum).
- Kullanıcı kipin açıldığını anlasın: kısa bir ton ve adada kalıcı bir simge.
- Kullanıcı kipi sesle kendisi açıp kapatabilsin ("misafir modunu aç", "kalabalık bir ortamdayım").
- Yalnızken konuşma bugünkü gibi doğal kalsın: her seferinde "Ada" demek gerekmesin.

## Hedef dışı

- Aynı anda konuşan iki sesi ayırmak.
- Oturum yokken (yalnız "Hey Ada" dinlenirken) misafiri fark etmek. Bilgisayarı yormamak için sesler yalnız oturumda
  karşılaştırılır.
- Birden çok kullanıcı tanıtmak. Yalnız bir ses izi olur.
- "Hey Ada" uyandırmasını sese göre süzmek. Misafir "Hey Ada" derse oturum yine açılır.

## Olmaması gerekenler

- Kullanıcı yalnızken Ada'nın onu duymaması. Bu en kötü sonuçtur. Yanlış reddedilmeye karşı güvenlik payı aşağıda.
- Kip açıkken sık ton çalınması. Ton kip her açıldığında bir kez çalar, misafirin her sözünde çalmaz.
- Ada'nın kip açılınca kendiliğinden konuşması. O an odada biri konuşuyor, araya girmek kaba olur ve para tutar. (Elle açınca
  kısaca söyler, çünkü kullanıcı ona sordu.)
- Ses kaydının saklanması ya da internete gitmesi. Tanıtma ve karşılaştırma tamamen bilgisayarda yapılır.

## Kısıtlar

- Öncelik performans: yerel büyük model yok. Konuşmacı modeli küçük (yaklaşık 30 MB) ve yalnız oturumda ya da tanıtırken çalışır.
- Kitaplık zaten projede: sherpa-onnx 2.0.3. `SpeakerEmbeddingExtractor`, `SpeakerEmbeddingManager` (`Verify`/`Search`) ve
  `VoiceActivityDetector` (Silero, TEN) içeriyor (DLL'de doğrulandı). Uyandırma modeli gibi model dosyaları `src/CamAda/Assets`
  altında depoya girer. Proje açık kaynak olacağı için modelin lisansı yeniden dağıtıma izin vermeli (3D-Speaker ve WeSpeaker
  Apache-2.0).
- Oturum mikrofonu yankı engelleyicili, 16 kHz mono, 100 ms'lik parçalar (`EchoCancelledMic`). Yankı engelleyici yoksa yarı çift
  yönlü yol var (`HalfDuplex`: Ada konuşurken mikrofon sıfır gider).
- "Hey Ada" ile açılan oturuma algılamadan 0,8 sn öncesinden başlayan ön kayıt gider (`DrainFromWake`).
- Mikrofonu Google'a açan otomatik deneme yok (25.11 olayından sonra kural). Ölçüm ve denemeler yerelde kalır ya da sahte odayla
  (`CAMADA_ADA_FAKE_ROOM`) yapılır.
- Bu dizüstünde mikrofon "Mikrofon Dizisi (Realtek)". Konuşmacı tanımanın bu mikrofonda ve yankı engelleyiciden sonra ne kadar iyi
  ayırdığı bilinmiyor: ilk iş ölçüm.

## Yaklaşımlar

| Yol | Çözdüğü | Bedeli |
|---|---|---|
| **A. Ses tanıma + misafir varken adıyla seslenme (seçilen)** | İki durumu da. Misafirin sesi internete çıkmaz. Yalnızken konuşma doğal kalır. | En büyük iş. Yeni model. Kısa sözlerde ~0,5 sn gecikme olabilir. Doğruluk ölçülmeden bilinmiyor. |
| B. İlk komuttan sonra hep adıyla seslenme | 2. durumu ve oturumun açık kalmasını. | 1. durumu çözmez: kullanıcı konuşurken araya giren söz yine karışır (kullanıcının asıl anlattığı bu). Her devam sorusunda "Ada" demek gerekir. |
| Talimatı sıkılaştırmak | — | 25.11'de yetmedi, sayılmadı. |

Kullanıcı A'yı seçti ("A'yı deneyelim, sesimi öğrensin"). B'nin adıyla seslenme kuralı A'nın misafir kipinde kullanılır.

## Tasarım

### 1. Sesini tanıtma

- Ada ayarlarında (AdaPanel), "Mikrofon" bölümünün altında yeni **"Sesim"** bölümü: "Sesimi tanıt" düğmesi. Tanıtılmışsa
  "Yeniden tanıt" ve "Sil", bir de kısa açıklama: "Ada başka bir ses duyunca yalnız 'Ada' diye başlayan sözlerini dinler."
- "Sesimi tanıt"a basınca ekranda 4–5 kısa Türkçe cümle sırayla gösterilir. Kullanıcı okur, toplam ~30 sn. Her cümlenin altında
  ilerleme, sonunda "Tamam" ya da sorun varsa ("Ses çok kısık, yeniden dene").
- Kayıt oturumla **aynı mikrofon ve aynı yankı engelleyiciyle** yapılır (Ada oturumda nasıl duyuyorsa öyle öğrensin). Tanıtma
  sürerken uyandırma dinleyicisi durur, bitince sürer. Oturum açıksa düğme kapalı.
- Saklanan: yalnız sesten çıkarılan sayısal ses izi (bir ya da birkaç gömme). `%LOCALAPPDATA%\CamAda\` altında, Windows şifrelemesiyle
  (DPAPI, hafızayla aynı yol). Ses kaydı saklanmaz.
- Ses izi yoksa kendiliğinden misafir algılama kapalı, Ada bugünkü gibi çalışır. Elle misafir kipi yine çalışır (aşağıda).

### 2. Oturumda ses süzgeci

Mikrofonla Google arasına bir süzgeç girer (bugünkü yarı çift yönlü susturmanın yanına). Google'a giden her 100 ms ya olduğu gibi
ya da sıfır (sessizlik) gider. Akış hiç kesilmez, Google'ın konuşma algısı bozulmaz.

- **Konuşma parçaları:** Yerel konuşma algılayıcı (VAD) sesi parçalara ayırır. Her parçanın başı, karar verilene kadar bekletilir
  (hedef ≤1 sn). Karar "kullanıcı" ise bekleyen kısım hemen, gerçek zamandan hızlı gönderilir, sonrası beklemeden akar. Böylece uzun
  sözlerde gecikme olmaz. Parça kararı beklemeden biterse ("evet") karar parçanın sonunda verilir (~0,5 sn gecikme).
- **Uzun parçada yeniden denetim:** Parça sürerken belli aralıklarla yeniden karşılaştırılır. Kullanıcıdan sonra hiç durmadan
  misafir konuşursa, misafir fark edildiği andan sonrası sıfır gider. İlk ~1 sn kaçabilir; ölçümde görülecek.
- **Başkasının sesi:** O parça sıfır gider, Google hiç duymaz. Bu yüzden oturum sayacını sıfırlamaz, Ada'nın sözünü kesmez,
  komuta karışmaz. Misafir kipi açık değilse açılır (ton + simge).
- **"Hey Ada"nın ardından gelen ilk söz:** Ön kayıt ve oturum başı da süzgeçten geçer (1. durum tam da ilk komutta oluyor).
- **Güvenlik payı (karar):** "Ada" ile başlayan söz, kimin sesi olursa olsun geçer. Ada kullanıcıyı yanlışlıkla tanımazsa
  (uzaktan, kısık, hastayken) kip açılır ama "Ada, …" demek yine işe yarar. Misafir "Ada, …" derse zaten Ada'ya sesleniyordur.
  Kabul edilen sonuç: misafir "Ada, evet" diyerek bir kapatma onayını geçirebilir (odada, fiziksel olarak orada).
- **Yarı çift yönlü yolda** (yankı engelleyici yok) süzgeç aynı çalışır. Ada konuşurken mikrofon zaten sıfır gider.

### 3. Misafir kipi

- **Açılış:** Ada'nın kendisi açarsa (oturumda başka ses) kısa, yumuşak bir ton çalar. Uyanma bipinden (iki yükselen ton) ve hata
  sesinden (tek alçak ton) farklı. Kullanıcı açarsa Ada kısaca söyler.
- **Kipte ne geçer:** Ses izine uyan ve "Ada" ile başlayan sözler. "Ada" sözcüğünü oturum mikrofonunda yerel anahtar sözcük
  algılayıcı yakalar (uyandırma modeli, "ADA" anahtarı). Sözcükten ~0,8 sn öncesinden itibaren gönderilir, uyandırmadaki ön kayıt
  gibi. Söz yalnız başında "Ada" varsa geçer. Ortadaki "Ada" ("sesi kıs Ada") yetmez, öncesi zaten gitmemiştir.
- **İstisnalar (adsız geçer, ses izi yine denetlenir):**
  - "Hey Ada" ile açılan oturumun ilk sözü ("Hey Ada" zaten addır).
  - Ada soru sorduktan hemen sonraki yanıt: kapatma onayı bekleniyorsa ya da Ada'nın son cümlesi soruyla bitiyorsa (yazılı dökümde
    "?"), Ada'nın yanıtı bittikten sonraki sessizlik süresi içinde gelen ilk söz.
  - Not kipi boyunca kullanıcının sözleri (ses izi uyanlar).
- **Yerel söz kesme:** Kip açıkken Ada konuşurken yerel söz kesme yalnız "Ada" sözcüğüyle olur. Bugünkü ses düzeyiyle kesme
  misafirin sesinde de tetiklenirdi. Kip kapalıyken bugünkü gibi.
- **Bitiş:** Kendiliğinden açılan kip, başka ses en son duyulduktan **2 dk** sonra biter. Oturumlar arasında taşınır: 2 dk içinde
  yeni "Hey Ada" denirse kip baştan açık gelir, simge hemen görünür.
- **Elle açma/kapama (kullanıcı isteği):** Yeni işlev `misafir_kipi(acik)`. Model "misafir modunu aç", "kalabalık bir ortamdayım",
  "yanımda biri var" gibi sözlerde `acik=true`, "misafir modunu kapat", "yalnızım" gibi sözlerde `false` ile çağırır.
  - Elle açılan kip, kullanıcı kapatana ya da CamAda yeniden başlayana kadar açık kalır (karar: kendisi açtıysa ne zaman
    biteceğini de kendisi bilir). 2 dk kuralı yalnız kendiliğinden açılana uygulanır.
  - Elle kapatınca kip hemen biter. Yeni bir başka ses duyulursa kendiliğinden yeniden açılabilir.
  - Ses izi olmadan da çalışır: kip yalnız "Ada" ile başlayan sözleri geçirir.
  - Sonuç modele: "tamam: misafir kipi açık; kullanıcıya kısaca 'Misafir kipi açık, bana adımla seslen.' de" (kapatınca benzeri).

### 4. Adada gösterim

Bugün oturum sırasında hap büyür, solda nabız atan nokta (dinlerken) ya da ✦ (konuşurken), ortada tek satır. Kip açıkken noktanın
soluna küçük "iki kişi" simgesi (Segoe Fluent) gelir, oturum boyunca kalır. Dinlerken satırda "Dinliyorum…" yerine "“Ada” deyin,
dinliyorum" yazar. Kullanıcının sözü ve Ada'nın yanıtı bugünkü gibi gösterilir:

```
Normal:          ( ●  Dinliyorum…                 )
Kip açıkken:     ( 👥 ●  "Ada" deyin, dinliyorum  )
"Ada, sesi kıs": ( 👥 ●  “Ada, sesi kıs”          )
Ada yanıtlarken: ( 👥 ✦  Sesi kıstım.             )
```

Tam ekranda ada gizli olduğundan orada tek işaret tondur. Erişilebilirlik adı da kipi söyler.

### 5. Talimat

"Sana söylenmeyen konuşmaları yanıtlama" satırı kalır. Eklenecek: misafir kipi işlevinin ne zaman çağrılacağı. Talimat kipin durumunu
bilmek zorunda değil, süzgeç uygulamada.

### 6. İlk iş: ölçüm (sürdürme koşulu)

Plan, başka bir şey yapılmadan önce yerel bir ölçümle başlar (Debug denemesi, ör. `--ada-konusmaci-sina`):

- Kullanıcı sesini tanıtır, sonra kendisi ve odadaki başka biri birkaç cümle okur. Her parçanın benzerlik puanı ve karar süresi
  yazdırılır. Google'a hiçbir şey gitmez.
- Aynı denemede oturum mikrofonunda "ADA" anahtarının yanlış tetiklenmesi de ölçülür ("kadar", "adam", "Adana" gibi sözcükler).
- **Sürdürme koşulu (karar):** Seçilen eşikte kullanıcının parçalarının en az %90'ı geçmeli, başkasının parçalarının en az %90'ı
  durmalı, karar ≤1 sn içinde verilmeli. Tutmazsa durulur ve kullanıcıya dönülür (ör. B yoluna geçmek).
- Model (3D-Speaker CAM++ / WeSpeaker ResNet34 vb.) ve VAD (Silero / TEN) bu ölçümde seçilir.

## Kullanıcının verdiği kararlar

- A yolu: ses tanıma + misafir varken adıyla seslenme.
- Gösterim: ton + adada simge. Ada kendiliğinden konuşmaz.
- Kendiliğinden açılan kip 2 dk sürer (önerilen 5 dk yerine).
- Elle açma/kapama sesle.

## Benim kararlarım (kullanıcı düzeltebilir)

- Tanıtma ayarlarda, 4–5 cümle, oturumla aynı mikrofon ve yankı engelleyici. Yalnız ses izi saklanır, DPAPI ile.
- Güvenlik payı: "Ada" ile başlayan söz kimden gelirse gelsin geçer.
- "Hey Ada" sonrası ilk söz, sorudan sonraki yanıt ve not kipi adsız geçer.
- Kipte yerel söz kesme yalnız "Ada" ile.
- Elle açılan kip kapatılana ya da CamAda yeniden başlayana kadar sürer. Elle açınca Ada kısaca söyler.
- Ölçüm sürdürme koşulu: %90 / %90 / ≤1 sn.

## Yapana bırakılanlar

- Model ve VAD seçimi, eşik, karar penceresi, yeniden denetim aralığı (ölçümle).
- Kipi kendiliğinden açan en kısa başka ses (tek kısa yanlış ret kullanıcıyı 2 dk "Ada" demeye zorlamasın; ölçümle).
- Süzgeçten sonra giden ses her yerde "kullanıcı konuştu" sayılır (not kipindeki yerel ses düzeyi zaten gönderilen sese bakıyor).
  Sıfır giden parça hiçbir sayacı sıfırlamaz.
- Ses izinin biçimi (tek ortalama ya da cümle başına gömme) ve dosya adı.
- Tanıtma cümlelerinin metni, tonun sesi.
- Yeni ayarlar (`AssistantSettings`): eşik, 2 dk, tanıtma açık/kapalı gibi değerlerin adları ve aralıkları.
- Debug denemeleri ve sahte odayla oturum denemesi (`CAMADA_ADA_FAKE_ROOM` iki farklı sesle).
