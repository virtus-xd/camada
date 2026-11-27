# Ada arayüz dilinde açılır (İngilizcenin 2. adımı, küçültülmüş)

Tarih: 27.11.2026. Kullanıcı onayı: 27.11 ("onaylıyorum, tasarımı yaz başla"). Boyut: küçük değişiklik.
Önceki adım: `2026-11-27-ingilizce-arayuz-design.md` (arayüz İngilizce/Türkçe, `main`'de 8d356bd).

## Amaç ve neden

Kullanıcı canlı denemede gördü: Ada İngilizceyi anlıyor, "İngilizce yanıt ver" deyince İngilizce konuşuyor. Model zaten çok
dilli, yani ayrı bir "İngilizce Ada" gerekmiyor. Eksik olan tek şey varsayılan dil: Ada, Camada'nın arayüz dili neyse o dilde
açılsın. İngilizce arayüz seçen biri Ada'yı çağırınca İngilizce karşılık alsın, Türkçe arayüzde her şey bugünkü gibi kalsın.

## Hedefler

1. Oturum yönergesi arayüz dilini Ada'nın varsayılan dili yapar. Arayüz dili `CultureInfo.CurrentUICulture`'dır (Türkçe ya da
   İngilizce; "Otomatik" Windows'tan çözülmüş hâli). Kullanıcı başka dil isterse Ada o oturumda o dile geçer; bir sonraki
   oturum yine arayüz diliyle açılır.
2. Ada'nın kelimesi kelimesine söylettiğimiz hazır cümleler arayüz dilinde:
   - oturum süresi dolunca veda (`AdaInstruction.MaxDurationGoodbye`),
   - not bitti: "Not aldım." (`AdaInstruction.NoteFinished`, `AdaToolRunner` not sonuçları),
   - not kipi açılınca "Dinliyorum" (`AdaToolRunner`),
   - misafir kipi açıldı / kapandı (`AdaToolRunner.GuestMode`),
   - hatırlatma ve zamanlayıcı duyurusu (`ReminderText.Announcement`): "Süre doldu: …", "Kaçırılan hatırlatma: …", içindeki
     zaman ifadesi ("yarın 09.00") ve "ve" bağlacı dahil.
3. Söyleme oturumunun yönergesindeki (`AdaInstruction.Announcer`) "Türkçe" kalkar: verilen cümleyi olduğu gibi söyler.
4. Yönergedeki bugünün tarihi arayüz dilinde yazılır.
5. "Ada speaks Turkish for now" notu kalkar: Ada panelindeki `TurkishOnlyNote` ve `AdaPanel_TurkishOnlyNote` anahtarı (iki
   resx), README.md ve README.tr.md "Status/Durum", `packaging/store-listing.md`'deki üç yer.

## Hedef dışı

- Yönergenin geri kalanı, işlev adları ve açıklamaları, işlevlerin döndürdüğü sonuçlar (hava cümlesi, güncel bilgi, hatırlatma
  saati, hata metinleri, "Belgeler\Ada Notları" yolu) Türkçe kalır. Bunlar yalnız modele gider; model konuştuğu dile çevirir.
- Uygulama adı eşleştirme (`AppAliases`), ad eşleştirme (`NameMatch`), hassas bilgi süzgeci (`SensitiveText`) değişmez.
- Uyanma sözcüğü, ses tanıtma, misafir kipi davranışı değişmez.
- Kullanıcının konuştuğu dile kendiliğinden geçmek yok. Arka plandaki bir televizyon ya da yabancı dilde bir söz Ada'nın dilini
  değiştirmesin; geçiş yalnız istenince olur. (Kullanıcının tarif ettiği davranış: "söyleyince" geçiyor.)

## Olmaması gereken (anti-hedef)

Türkçe kullanıcının deneyimi değişmemeli. Türkçe arayüzde hazır cümleler bugünküyle birebir aynıdır; yönergede yalnız dil
satırının yazımı değişir. Bugün model istenince zaten başka dile geçiyor, yeni satır bunu açıkça yazar.

## Yaklaşımlar

- **A, seçilen:** Yönergedeki dil satırı, tarih ve hazır cümleler arayüz dilinde; geri kalan her şey Türkçe. Küçük bir değişiklik,
  Türkçede denenmiş davranışa dokunmaz. Kullanıcı "yeterli gibi" dedi.
- **B:** Yönergeyi, işlev açıklamalarını ve sonuçlarını baştan iki dilde yazmak. İki yönergenin bakımı gerekir ve Türkçede
  denenmiş davranışı riske atar; bugünkü gözlem buna gerek olmadığını gösteriyor.
- **C:** Tek bir İngilizce yönerge ve sonuna "şu dilde konuş" satırı. Türkçe yönergeyi baştan değiştirir; B'deki risk aynen
  geçerli.

## Kararlar (benim önerim; kullanıcı ayrıntıya girmedi)

- **Dil satırı** (yönerge Türkçe kaldığı için dil adı Türkçe yazılır): "Varsayılan dilin Türkçe: kullanıcı başka bir dilde
  konuşsa da bu dilde konuş; yalnız açıkça başka bir dilde konuşmanı isterse bu oturumda o dile geç. Kısa, doğal ve hafif
  ciddi bir tonla." İngilizce arayüzde "Türkçe" yerine "İngilizce (English)" yazar. Son incelemede güçlendirildi: ilk yazımda
  ("Türkçe konuş; kullanıcı … isterse") eski "Her zaman" çapası yoktu, Ada duyduğu dile kendiliğinden geçebilirdi.
- **Söyleme oturumu yönergesi:** "Sana verilen cümleyi yazıldığı dilde, çevirmeden, …". İlk yazımdaki "kendi dilinde" ifadesi
  "senin dilinde" diye de okunabiliyordu; o zaman İngilizce hatırlatma Türkçeye çevrilebilirdi.
- **Hazır cümlelerin yeri:** `CoreStrings` (İngilizce temel + `tr`), mevcut `CoreText.Get`/`CoreText.Format` ile. Türkçe değerler
  bugünkü metinle birebir aynıdır. Sistem notu sarmalayıcıları ("(Sistem notu, kullanıcı söylemedi: …)", "tamam: …; yalnız '…'
  de") Türkçe kalır; yalnız tırnak içindeki, Ada'nın söyleyeceği cümle yerelleşir.
- **İngilizce cümleler:**

  | Yer | Türkçe (bugünkü) | İngilizce |
  |---|---|---|
  | Oturum sonu | Uzun sürdü, yeniden çağırabilirsin. | That took a while; you can call me again. |
  | Not bitti | Not aldım. | Note taken. |
  | Not kipi | Dinliyorum | I'm listening. |
  | Misafir açık | Misafir kipi açık, bana adımla seslen. | Guest mode is on; say my name first. |
  | Misafir kapalı | Misafir kipi kapandı. | Guest mode is off. |
  | Süre doldu | Süre doldu: {0}. | Time's up: {0}. |
  | Kaçırılan (1) | Kaçırılan hatırlatma: {0}, {1}. | Missed reminder: {0}, {1}. |
  | Kaçırılan (çok) | Kaçırılan hatırlatmalar: {0}. | Missed reminders: {0}. |
  | Bağlaç | ve | and |
  | Zaman | 14.41 / yarın 09.00 / 28 Kasım Cumartesi 09.00 | 2:41 PM / tomorrow 9:00 AM / Saturday, November 28, 9:00 AM |

- **Duyurudaki saat:** Türkçede bugünkü "HH.mm". İngilizcede en-US "h:mm tt" kullanılır, çünkü konuşulan İngilizcede doğal olan
  bu. Bölge ayarı bu metni etkilemez; ekrandaki saat kuralı ayrıdır.
- **`ReminderText.When`/`Relative`:** işlev sonucunda (modele giden) Türkçe kalır; yalnız duyuru arayüz dilini kullanır. Türkçe
  çıktı bugünküyle aynıdır.
- **`ui` parametresi:** Plan 11 düzeninde, çağıran yer `CultureInfo.CurrentUICulture` verir (App, `AdaAssistant`,
  `AdaToolRunner`, `AdaProbes`). Çekirdek kendisi okumaz.
- **Hatırlatmanın kendi metni** kaydedildiği dilde kalır. Türkçe kurulmuş bir hatırlatma İngilizce arayüzde "Time's up:
  makarna." diye söylenir; bu kabul edildi.

## Riskler

- Model Türkçe bir işlev sonucunu (hava cümlesi, güncel bilgi) bazen çevirmeden okuyabilir. Canlı denemede bakılır; olursa o
  sonuçlar ayrı bir iş olarak yerelleşir.
- `guncel_bilgi` işlevinin `soru` parametresi "Türkçe" diyor. İngilizce oturumda model soruyu Türkçeye çevirip sorar, gelen
  yanıtı İngilizceye çevirir. Bu yüzden değiştirilmez; denemede yanıt kalitesine bakılır.

## Doğrulama

- **Birim testleri:**
  - `AdaInstruction`: İngilizce ve Türkçe dil satırı, tarih, iki dilde hazır cümleler.
  - `ReminderText.Announcement`: iki dilde, tek, çok ve kaçırılan hatırlatmalar.
  - Türkçe çıktıların bugünküyle birebir aynı kaldığı mevcut testler korunur.
- **Canlı deneme** (kullanıcı):
  1. İngilizce arayüzde Ada'yı çağır: İlk yanıt İngilizce olmalı.
  2. "Speak Turkish" de: Ada Türkçeye geçmeli. Oturumu kapatıp yeniden çağır: Ada yine İngilizce açılmalı.
  3. 1 dakikalık zamanlayıcı kur: "Time's up: …" duyulmalı.
  4. Not al, sonra bitir: "I'm listening" ve "Note taken" duyulmalı.
  5. Misafir kipini aç ve kapat.
  6. Ada boştayken (oturum yokken) İngilizce bir zamanlayıcının dolmasını bekle: duyuru çevrilmeden İngilizce söylenmeli.
  7. Dil geçişi istemeden tek bir Türkçe cümle söyle (İngilizce arayüzde; Türkçe arayüzde de tersi): Ada kendi dilinde kalmalı.
  8. "Remember that …" de ve Ada'nın yapamadığı bir şey iste: yanıtlar Türkçe kalıp cümleleriyle değil, İngilizce gelmeli.
  9. Türkçe arayüzde aynı adımları dene: her şey bugünkü gibi olmalı.

## Kurucuya kalanlar

Resx anahtar adları, test adları ve yardımcı yöntemlerin imzası kurucuya kalır. Bu iş 1.1.0'ın içine girer; sürüm henüz Store'a
gönderilmedi.
