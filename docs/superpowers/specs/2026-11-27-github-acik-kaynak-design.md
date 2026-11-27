# Camada'yı GitHub'da açık kaynak yayımlama — kısa tasarım

Tarih: 27.11.2026 · Boyut: küçük iş (uygulama kodu değişmiyor)

## Amaç ve neden

Depo GitHub'da herkese açık olsun: insanlar Camada'yı indirip kullanabilsin, kendi Gemini anahtarıyla Ada'yı bağlayabilsin, geliştirip çatallayabilsin (isterse ticari olarak da). Tek gelir kaynağı ücretli Ada aboneliğidir; onun sunucusu ayrı ve kapalı bir depoda olacak, bu depoda değil. Açık kod, görev çubuğunun yerini alan ve mikrofonu dinleyen bir uygulamada güveni de artırır. Bağış isteyen GitHub Sponsors ile destek olur.

## Hedefler

- Depo geçmişiyle (131+ commit) birlikte yayımlanır; geçmişteki kişisel izler temizlenir.
- Bir yabancı geliştirici depoyu açtığında ne olduğunu, nasıl derleneceğini, nasıl katkı vereceğini ve güvenlik açığını nereye bildireceğini bulur.
- Her değişiklik isteği Windows'ta otomatik derlenir ve testlerden geçer.

## Hedef olmayanlar

- Uygulama kodunda değişiklik, İngilizce arayüz (ayrı iş, betadan sonra).
- Ada abonelik sunucusu (ayrı, kapalı depo).
- Store gönderimi (Plan 10 ayrıca sürüyor).

## Olmaması gerekenler (çalışsa bile başarısızlık sayılır)

- Herkese açılan geçmişte sır (API anahtarı, belirteç) ya da temizlenmesi kararlaştırılan kişisel bilgi kalması.
- Kullanıcının onayı olmadan deponun herkese açılması.
- Hakkı olmayan görselin (Store ekran görüntülerindeki üçüncü taraf duvar kâğıdı) depoya girmesi.

## Kısıtlar

- Tarama (27.11): kodda ve geçmişte sır bulunmadı; Gemini anahtarı ve Spotify Client ID'yi kullanıcı uygulamada giriyor.
- Paketlenen modeller (Apache 2.0) ve Manrope yazı tipi (OFL) lisansları açık kaynakla uyumlu.
- Ücretsiz GitHub planında gizli depoda dal koruması yok; koruma depo herkese açılınca kurulur.
- GitHub Sponsors Türkiye'yi destekliyor; Buy Me a Coffee'nin Türkiye'ye ödeme yaptığı doğrulanamadı.

## Yaklaşımlar ve kararlar

**Lisans** — kullanıcı seçti: **Apache 2.0.** Önce ticari olmayan lisans (PolyForm Noncommercial) konuşuldu. Kullanıcı tek gelirin ücretli Ada olduğunu görüp tamamen açık kaynağa geçti. MIT de aynı serbestliği verir. Apache 2.0'ın ad ve logoyu dışarıda bırakan maddesi ile patent koruması olduğu için o seçildi; bağımlılıklar (ManagedShell, sherpa-onnx) da Apache 2.0. GPL elendi: çatallayanı kodunu açmaya zorlar, kullanıcının "çıkarıyorsa çıkarsın" isteğine ters. Açık lisansta katkı sözleşmesi (CLA) gerekmez, çünkü gelen kod ücretli Ada'da da kullanılabilir.

**Geçmiş** — kullanıcı seçti: **A, geçmişi koru, kişisel izleri temizle.** Elenenler: tek commit'le temiz başlangıç (görünen geliştirme geçmişi kaybolur), olduğu gibi açmak (kişisel izler kalır). Araç `git filter-repo` (Git'in önerdiği; `filter-branch` eski ve yavaş, BFG Java ister). Temizlenecekler:
- Tüm commit'lerin yazar/işleyen e-postası → GitHub'ın gizli (noreply) adresi; yeni commit'ler de bu adresle.
- Belgelerdeki kullanıcı klasörü yolu → nötr ifade.
- Belgelerde örnek olarak geçen iki gerçek şehir adı → başka şehirler.
- Silinecek ifadelerin tam listesi proje dışında (scratchpad) tutulur, depoya girmez.
- Commit'lerdeki "Co-Authored-By: Claude" satırları kalır (gizlemek güveni azaltır).
- Gizlilik sayfasındaki ve Store metnindeki iletişim e-postası kalır (kullanıcının onayladığı herkese açık iletişim adresi).

**Bağış** — kullanıcı seçti: **GitHub Sponsors** (`.github/FUNDING.yml`). Kaydı kullanıcı yapar.

**Benim kararlarım (düzeltilebilir):**
- Depo adı `camada`, hesap `virtus-xd`; önce **gizli** yüklenir, kullanıcı inceleyip onaylayınca herkese açılır.
- Telif satırı "VirtusTech" (Store yayıncı adıyla aynı).
- Yüklenen dallar: `main` ve `plan-10-store-paketi`; bu iş `github-acik-kaynak` dalında yapılıp `main`'e alınır.
- README Türkçe (`README.md`) + İngilizce (`README.en.md`), en üstte dil bağlantısı. README görev çubuğu güvenlik ağını (kurtarma kısayolu, `--restore-taskbar`) açıkça anlatır, çünkü uygulamayı deneyen biri kendi görev çubuğunu gizler.
- Eklenecekler: `LICENSE`, `NOTICE` (üçüncü taraf lisansları), `CONTRIBUTING.md`, `SECURITY.md` (GitHub'ın özel güvenlik bildirimi açılır; e-posta gerekmez), `CODE_OF_CONDUCT.md`, hata/öneri şablonları, PR şablonu, `FUNDING.yml`, Windows'ta derleyip test eden iş akışı.
- `docs/superpowers` belgeleri kalır (tasarım kararlarının kaydı); README'den bağlantı verilir.
- Depo açıklaması ve konu etiketleri gh ile ayarlanır; Wiki kapalı.

## Kullanıcıya kalan adımlar

1. GitHub Sponsors kaydı (banka hesabı) — onay gelene kadar Sponsor düğmesi çalışmayabilir.
2. README için hakkı olan bir duvar kâğıdıyla ekran görüntüsü (gelene kadar README görselsiz).
3. Gizli depoyu inceleyip "herkese aç" onayı; açılınca `main` için dal koruması (iş akışı geçmeden birleştirme yok) kurulur.

## Geliştiriciye kalanlar

- Yeniden yazmadan önce tam yedek (proje dışında `git bundle` + klasör kopyası); yeniden yazma sonrası eski→yeni commit eşlemesi saklanır (yerel notlardaki eski commit kimlikleri bununla bulunur).
- Yeniden yazmadan sonra geçmişin yeniden taranması (e-posta, yol, şehirler, sır kalıpları).
- İş akışında donanım isteyen test çıkarsa nasıl ayrılacağı.
- Belgelerin ayrıntılı içeriği, şablon alanları, `.gitattributes` gerekip gerekmediği.
