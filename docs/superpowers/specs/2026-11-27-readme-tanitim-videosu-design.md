# README tanıtım videosu — kısa tasarım

Tarih: 27.11.2026 · Boyut: küçük iş (uygulama kodu değişmiyor)

## Amaç

GitHub sayfasına gelen biri README'nin başında ~35 saniyede Camada'nın ne olduğunu, gerçek görüntüsüyle görsün: görev çubuğunun yerini alan cam ada, müzik, tepsi, hızlı ayarlar, pano, "Hey Ada" ve tek tıkla geri dönen görev çubuğu. Sonunda depoyu yıldızlayıp denemek istesin.

## Kararlar (kullanıcıyla)

- **Yol C:** gerçek ekran kaydı + üstünde animasyonlu İngilizce yazılar. Kaydı kullanıcı aldı: `%USERPROFILE%\Videos\deneme2.mp4` (1920×1080, 60 fps, 86 s, Windows varsayılan duvar kâğıdı). Kayıt olduğu gibi kullanılır (pano bölümünde yalnız ekran görüntüleri var, yeniden çekilmeyecek).
- **Araç:** HyperFrames (HTML → video). Yalnız FFmpeg'le yaklaşmalar sabit kesim, yazılar düz kalırdı; README ~900 px genişlikte gösterildiği için her sahnede ilgili bölgeye yaklaşmak şart.
- **Sessiz.** Müzik ve efekt yok (lisans derdi yok; README'de oynatıcı tıklanınca açılır, yazılar anlatır).
- **Yazılar İngilizce** (README varsayılanı İngilizce); uygulamanın kendi arayüzü Türkçe görünür.

## Kısıtlar

- Hızlı ayarlardaki Wi‑Fi ağ adı (kayıtta 0:34–0:38) görünmez: o kutu kaynağa işlenmeden önce pikselleştirilir. Ağ adı bu belgeye de yazılmaz.
- Store görsellerindeki üçüncü taraf duvar kâğıdı kullanılmaz; yalnız bu kayıt ve Camada'nın kendi logosu (`packaging/Assets`) kullanılır.
- Gerçek arayüz dışında arayüz çizilmez (sahte panel yok).
- Teslim: H.264 MP4, 1920×1080, 30 fps, ses izi yok, **en çok 10 MB** (GitHub ücretsiz hesap ek dosya sınırı). İlk kare anlamlı olmalı (GitHub önizleme olarak ilk kareyi gösterir): başlık ilk karede okunur.

## Akış (kaynak zamanları yaklaşık; kurgu ~35 s)

| # | Kurgu | Ekrandaki yazı | Kaynak | Kamera |
|---|---|---|---|---|
| 1 | 0–3 s | **Camada** · A glass island for your Windows 11 taskbar | 0:02–0:05 (görev çubuğu + küçük ada) | tam kare, yazı sol üstteki koyu alanda |
| 2 | 3–7.5 s | Replaces the taskbar | 0:06–0:11 (menü → "Görev çubuğunu gizle" → görev çubuğu kaybolur) | menüye ~1.8× yaklaş, sonra tam kareye çekil (görev çubuğunun gidişi görünsün) |
| 3 | 7.5–12 s | Music and time at a glance | 0:14–0:20 (müzik kartı) | kartın üstüne ~1.8× |
| 4 | 12–14.5 s | Your system tray, too | 0:31–0:34 ("Arka planda çalışanlar") | panelin üstüne |
| 5 | 14.5–19 s | Quick settings | 0:34–0:41 (hızlı ayarlar → ses kaydırıcısı) | panelin üstüne; Wi‑Fi adı pikselli |
| 6 | 19–22.5 s | Clipboard history | 0:43–0:49 (pano, "Kopyalandı") | panelin üstüne |
| 7 | 22.5–28.5 s | Say "Hey Ada" · “Set a 5-minute timer.” | 0:55–1:04 + 1:14–1:16 ("Dinliyorum…" → komut → "zamanlayıcınızı kurdum" → adada sayaç) | adanın ortasına; aradaki bekleme kesilir |
| 8 | 28.5–31.5 s | One click brings the taskbar back | 1:20–1:24 | menüye yaklaş, görev çubuğu gelirken tam kareye çekil |
| 9 | 31.5–35 s | Camada logosu + "Free & open source" + github.com/virtus-xd/camada | kaydın son karesi zemin | logo ortada, iniş hareketiyle; 2 s bekler |

## Görünüm

- Ortam: Camada'nın kendi camı. Yazılar, adanın kendi malzemesinden küçük bir cam plakanın üstünde (zemin `#CC14182A`, 1 px `#66FFFFFF` kenar, köşe yarıçapı adayla aynı); böylece mavi duvar kâğıdının parlak yerlerinde de okunur.
- Yazı tipi: Manrope (depoda, OFL) — başlık ExtraBold, açıklama SemiBold; okunacak yazı 1080'de en az 36 px, başlık ~72 px.
- Vurgu rengi: uygulamanın kendi turuncusu (hızlı ayar kutucuklarından ölçülecek), karede tek yerde.
- Yaklaşmalarda arayüz yazısı 1080'de en az ~26 px olacak kadar büyür (~1.8×); geçişler 0.4–0.6 s.
- Yasak: koyu boşlukta yüzen küçük öğeler, parlama/neon, ortalanmış her şey, aynı anda solarak gelen her şey, köşeye yapışık logo, başlığın karartılmış ekran görüntüsü üstünde durması.

## Teslim

- HyperFrames projesi depo dışında: `%USERPROFILE%\Videos\camada-tanitim\` (kaydın kopyası `assets/` altında). Çıktı: `camada-readme.mp4`.
- Kullanıcı MP4'ü GitHub web düzenleyicisinde README'ye sürükleyip bırakır (ya da bağlantıyı bana verir); bağlantı iki README'de (EN + TR) tanıtım paragraflarının altına, `<!-- Screenshot ... -->` yorumunun yerine konur.
- Render öncesi önizleme kullanıcıya açılır; kullanıcı onaylayınca son dosya üretilir.

## Geliştiriciye kalanlar

- Kesin kaynak giriş/çıkış kareleri, yaklaşma kutularının koordinatları, Wi‑Fi kutusunun koordinatı (kare kare bakılarak).
- Vurgu turuncusunun hex değeri (kareden ölçülerek).
- 10 MB'a sığmazsa önce bit hızı, sonra 1600×900.
