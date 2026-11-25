# Ada ile not alma — kısa tasarım

Tarih: 26.11.2026 · Kapsam: Ada'ya (Plan 5) küçük ekleme

## Amaç

"Hey Ada, yeni not defteri aç ve not al" denince Ada yeni bir Not Defteri açar, "Dinliyorum" der ve bekler. Kullanıcının
söylediklerini düzgün bir metne çevirip açılan nota yazar. Kullanıcının notları kısa notlar.

## Hedefler

- Komutla yeni Not Defteri açılır, Ada "Dinliyorum" deyip bekler.
- "Şunu not al: …" tek nefeste söylenirse beklemeden yazılır.
- Metin düzgün olur: noktalama ve büyük harf eklenir, "ııı, şey" gibi dolgular atılır. Anlam değişmez; özet ve ekleme yok.
- **Bitiş (kullanıcının seçimi, 3. yol):**
  - Her duraklamada o ana kadar söylenen nota yazılır.
  - Ada birkaç saniye daha not kipinde kalır; o sürede söylenenler alt satıra eklenir.
  - 4 sn sessizlikte ya da kullanıcı "bitti" deyince Ada "Not aldım" der ve normale döner.

- **Not dosyası (26.11 değişikliği, kullanıcı onayladı):** Ada her not için `Belgeler\Ada Notları\Not 26.11.2026 01.07.txt`
  gibi kendi dosyasını oluşturup Not Defteri'nde açar ve not bitince kaydeder. Neden: Not Defteri açılınca önceki oturumun
  sekmelerini geri yükler ve hangi sekmenin önde olacağı belli değil. Yalnız pencere başlığında bu dosyanın adı varken yazılırsa
  Ada başka bir nota hiç yazamaz.

## Hedef dışı

- Var olan notu okumak ya da düzenlemek.
- Uzun dikte.
- Başka düzenleyiciler.

## Olmaması gerekenler

- Notun Not Defteri dışında bir pencereye yazılması.
- Notta geçen bir sözün ("sesi kıs", "bilgisayarı kapat") komut olarak yürütülmesi.
- Ada'nın not sırasında söylenenlere yanıt vermesi.

## Kısıtlar

- Gemini Live tek oturum.
- Oturum 8 sn sessizlikte kapanır. Not 4 sn'de biter, ardından gelen "Not aldım" yeni etkinlik sayılır, oturum ondan sonraki 8 sn
  sessizlikte kapanır.
- Bu bilgisayardaki Not Defteri Windows 11'in sekmeli yeni Not Defteri (Microsoft.WindowsNotepad 11.2609). Açılınca kullanıcının
  önceki sekmelerini geri yükler (26.11 denemesinde 11 sekme geri geldi, önde kullanıcının notu vardı).
- Yönetici olarak çalışan pencereye tuş gönderilemez; mevcut `Native.IsElevated` ile denetlenir.

## Yaklaşımlar

| Yol | Ne kazandırır | Bedeli |
|---|---|---|
| **A. Notu Live modeli alır, not kipini uygulama denetler (seçilen)** | Tek model ve tek yol. Türkçe düzeltmeyi konuşmayı anlayan model yapar. Ada'ya yazılı not gönderme yolu vedada canlı doğrulandı. En az kod. | Düzeltmenin "özetleme" kuralına uyması modele bağlı. Parçayı Google'ın duraklama algısı böler. |
| B. Not sırasında Google'a ses gitmez; yerel kayıt, yerel duraklama, ayrı yazıya çevirme isteği | Ada'nın yanıt vermesi ya da komut yürütmesi baştan imkânsız. | Yerel duraklama kalabalıkta zayıf, parça başı 1–2 sn gecikme, bakımı gereken ikinci yol. |
| C. Live'ın ham yazısı + ayrı düzeltme isteği | — | Live yanıt üretmeyi sürdürür (ücret, gevezelik); iki model. |

Kullanıcı nasıl yapılacağını bıraktı; A'yı seçtim.

## Tasarım

**Yeni işlevler (AdaTools):**

| İşlev | Ne yapar |
|---|---|
| `not_al(metin?)` | Ada'nın not dosyasını oluşturur, `notepad.exe "<dosya>"` ile açar, başlığında dosya adı olan Not Defteri penceresi öne gelene kadar en çok ~5 sn bekler, not kipine geçer. Metin verildiyse hemen yazar ve kaydeder. Sonuç: "tamam: not defteri açık; 'Dinliyorum' de ve bekle". |
| `not_yaz(metin)` | Kullanıcının duraklamaya kadar söylediğinin düzeltilmiş hâli. İlk parça değilse önce alt satıra geçer. |
| `not_bitir()` | Kullanıcı "bitti" deyince çağrılır. |

**Not kipi (AdaAssistant ve işlev yürütücüsü):**
- Not sürerken `not_yaz` ve `not_bitir` dışındaki her işlev reddedilir: "hata: not alınıyor; söylenenler not olarak yazılır".
- Kullanıcı not kipinde ilk kez konuştuğu andan not bitene kadar Ada'nın sesi çalınmaz (gelen ses atılır). Böylece model
  yanıt üretmeye kalkışsa da duyulmaz.
- Kullanıcıdan 4 sn ses gelmezse ve bekleyen yazma yoksa not biter. Not her bitişte (sessizlik, "bitti", oturumun kapanması)
  Ctrl+S ile kaydedilir; bu tuş da yalnız başlık denetiminden sonra gider.
- Bitince Ada'nın sesi açılır, `not_bitir` çağrılmadıysa modele yazılı sistem notu gider ("not bitti, yalnız 'Not aldım.' de").
  Bu yol 10 dk vedasında canlı doğrulandı.
- Talimata eklenecek: "Not kipinde konuşma, yanıt verme; yalnız not_yaz çağır. Metni düzgün Türkçeye çevir; anlamı
  değiştirme, özetleme, ekleme yapma."

**Yazma:**
- `SendInput` ile Unicode karakterler gönderilir; satır sonu için Enter. Pano kullanılmaz, CamAda'nın pano geçmişi kirlenmez.
  Mevcut `Native.Paste` yardımcılarının üstüne kurulur.
- Yazmadan önce ön pencerenin Not Defteri süreci olduğu, yönetici olmadığı ve başlığında Ada'nın not dosyasının adı geçtiği
  denetlenir.
- Değilse metin hiçbir pencereye yazılmaz. Notun o ana kadarki tamamı panoya konur ve işlev "hata: Not Defteri önde değildi,
  not panoya kopyalandı" döner; Ada bunu söyler.

## Builder'a bırakılanlar

- Not Defteri penceresinin öne geldiğini anlamanın yolu (süreç adı, pencere sınıfı).
- 4 sn sayacının oturum denetimine (250 ms) nasıl bağlanacağı.
- İşlev açıklamalarının tam metni.

**Doğrulama:** Sahte odada ses dosyasıyla uçtan uca deneme (gerçek mikrofon açılmaz). Not Defteri'ne gerçekten yazıldığı ve
notta geçen komutların yürütülmediği görülür.
