# Ada ile anında ekran görüntüsü — kısa tasarım

Tarih: 26.11.2026 · Kapsam: Ada'nın `ekran_goruntusu` işlevi (küçük değişiklik) · Kullanıcı onayladı.

## Amaç

"Ekran görüntüsü al" deyince Ada seçme aracını açmasın, tüm ekranın görüntüsünü hemen alsın (kullanıcının isteği: "direkt ss
alsın tüm ekranı").

## Hedefler

- Bütün monitörler tek görüntüde (Win+PrtScn gibi), tam çözünürlükte (uygulama PerMonitorV2 DPI farkında).
- Windows'un "Ekran Görüntüleri" klasörüne `Ekran görüntüsü 2026-11-26 021530.png` adıyla kaydedilir (aynı saniyede çakışırsa
  " (2)"…); klasör `ScreenshotLibrary.KnownFolder()` ile bulunur.
- Görüntü panoya da kopyalanır (Win+PrtScn gibi; hemen yapıştırılabilsin).
- Ada: "Ekran görüntüsü alındı, panoya da kopyaladım."

## Hedef dışı

- Seçerek alma (adadaki hızlı ayarlardaki ekran görüntüsü düğmesi seçme aracını açmaya devam eder).
- Ekran kaydı.
- Ada konuşurken adada önizleme (CamAda Ada oturumunda önizlemeyi zaten göstermiyor; görüntü klasörde ve panoda).

## Olmaması gerekenler

- Tuş göndermek (Win+PrtScn gibi). Not Defteri olaylarından sonra Ada tuş göndermiyor.

## Yaklaşımlar

| Yol | Ne kazandırır | Bedeli |
|---|---|---|
| **GDI ile doğrudan çekim (seçilen)**: sanal ekran boyutunda BitBlt (SRCCOPY + CAPTUREBLT), WPF `BitmapSource`, PNG | Tuş yok, Windows ayarına bağlı değil, anında | Küçük bir P/Invoke yardımcısı |
| Win+PrtScn tuşu göndermek | Windows'un kendi davranışı | Tuş göndermek (yasak), Windows ayarına bağlı |
| Ekran alıntısı aracı (`ms-screenclip:`) | Mevcut | Kullanıcı tam bunu istemiyor |

## Tasarım

- `ScreenCapture` (uygulama): `BitmapSource CaptureAllScreens()` (sanal ekran: `GetSystemMetrics` 76–79) ve
  `string Save(BitmapSource, string folder, DateTime now)`.
- Dosya adı Core'da: `ScreenshotName.For(DateTime)` → `"Ekran görüntüsü yyyy-MM-dd HHmmss.png"`.
- `ekran_goruntusu` açıklaması: "Tüm ekranın görüntüsünü hemen alır, Ekran Görüntüleri klasörüne kaydeder ve panoya kopyalar."
- Yürütücü: çekim, kaydetme ve pano. Sonuç: "tamam: ekran görüntüsü alındı ve panoya kopyalandı". Pano meşgulse "tamam: ekran
  görüntüsü alındı (panoya kopyalanamadı)". Kaydedilemezse "hata: ekran görüntüsü kaydedilemedi".

## Builder'a bırakılanlar

GDI kaynaklarının serbest bırakılması, PNG kodlama ayrıntıları.

**Doğrulama:** Core testleri (dosya adı, açıklama). Debug denemesi ekranı geçici klasöre çeker; yalnız boyutu ve görüntünün boş
olmadığını yazar, dosyayı siler (ekranın içeriği günlüğe girmez).
