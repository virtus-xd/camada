# Plağı gizle — kısa tasarım

Tarih: 2026-11-24 · Durum: kullanıcı onaylı · Bağlı tasarım: `2026-11-23-cam-ada-design.md`

## Amaç
Kullanıcı sol alttaki masaüstü plağını istediği zaman gizleyip yeniden gösterebilmek istiyor; yer adanın sağ tık menüsü. Seçim kalıcı olmalı.

## Hedefler
- Adanın sağ tık menüsünde, "Görev çubuğunu gizle" ile aynı türden işaretli öğe: **"Plağı gizle"**.
- İşaretlenince plak hemen kaybolur; işaret kalkınca hemen geri gelir (uygulamayı yeniden açmadan).
- Seçim ayarlara (`Record.Enabled`) yazılır; yeniden açılışta korunur.
- Gizliyken plak hiç iş yapmaz (işlemci yok); üzerine gelince açılan şarkı kartı da plakla birlikte gider.

## Hedef dışı
- Adadaki mini plak ve müzik bölümü — etkilenmez.
- Plağın kendi üzerinde sağ tık menüsü, kısayol tuşu, plağın konum/boyut ayarları.

## Yaklaşımlar
1. **Kapat / yeniden kur (seçilen):** gizlerken plak penceresi kapatılır (medya olayları, örtülme yoklaması, ekran olayları bırakılır); gösterirken açılıştaki yolla yeniden kurulur ve Spotify ekleri yeniden bağlanır. Neden: "plak kapalı açılış" ile aynı hâl, gizliyken sıfır iş.
2. **Görünmez yap (Hide/Show):** daha az değişiklik; ama gizliyken pencere şarkı olaylarını dinlemeye devam eder ve açılışta kapalıyken pencere hiç yoktur — iki farklı "gizli" hâl. Elendi.

## Kararlar
- Menü yazısı "Plağı gizle" (kullanıcının sözcüğü; "Görev çubuğunu gizle" ile tutarlı: işaret = gizli). Varsayılan: işaretsiz (plak görünür).
- Menü açılırken işaret gerçek durumdan (`Record.Enabled`) okunur — diğer işaretli öğeler gibi.

## Yapıcıya bırakılanlar
- Komutun `IslandCommands`'a nasıl ekleneceği; plak kurulum/kapatma kodunun App içinde tek yere toplanması.
- Saf karar mantığı (ayar → yeni ayar) için Core testi; pencere tarafı canlı denemeyle doğrulanır.
