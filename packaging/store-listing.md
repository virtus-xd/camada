# Camada — Partner Center gönderim metinleri

Partner Center'daki alanlara kopyalanacak metinler. İlk gönderim gizli (özel hedef kitle); Türkçe. İngilizce liste 6. işte.

## Fiyat ve görünürlük

- Fiyat: Ücretsiz
- Görünürlük: Özel hedef kitle (yalnız geliştiricinin Microsoft hesabı); "Store'da bulunabilir" kapalı
- Kategori: Kişiselleştirme (yoksa: Yardımcı programlar ve araçlar)
- Gizlilik politikası adresi: https://camada-azure.vercel.app/gizlilik (geçici; Vercel projesi "camada", `site/gizlilik.html`)
- Destek iletişimi: erdemoz2003@gmail.com

> 27.11 düzeltme: 1.0.0 gönderiminde açıklama ve özelliklerde "bildirimler" geçiyordu (bildirimler iptal edildi). Sonraki
> gönderimde Partner Center'daki metin bu dosyadakiyle güncellenmeli.

## Kısa açıklama

Görev çubuğunun yerine geçen cam ada: uygulamalar, müzik, hızlı ayarlar, pano ve sesli asistan Ada tek yerde.

## Açıklama

Camada, Windows görev çubuğunun yerine ekranın üstünde duran şık bir cam ada koyar. Ada normalde küçüktür; saati ve çalan
müziği gösterir. Üzerine gelince genişler: sabitlediğiniz uygulamalar, açık pencereler, sistem tepsisi,
Wi‑Fi, Bluetooth, ses, parlaklık, takvim, pano geçmişi ve ekran görüntüleri tek dokunuş uzağınızdadır.

İsteğe bağlı sesli asistan Ada, "Hey Ada" dediğinizde uyanır: uygulama açar, ses ve parlaklığı ayarlar, web'de arar,
not alır, hatırlatma kurar ve hava durumunu söyler. Ada kendi Google Gemini API anahtarınızla çalışır ve varsayılan
olarak kapalıdır.

Windows görev çubuğunu gizlemek tamamen sizin seçiminizdir ve tek tıkla geri alınır. Başlat menüsündeki
"Görev çubuğunu geri getir" girişi her zaman görev çubuğunu geri getirir; uygulamayı kaldırdığınızda görev çubuğu
kendiliğinden geri gelir.

## Ürün özellikleri (Product features)

- Görev çubuğunun yerine ekranın üstünde duran cam ada
- Çalan müzik ve saat bir bakışta
- Sabitlenmiş uygulamalar, açık pencereler ve sistem tepsisi
- Wi‑Fi, Bluetooth, ses ve parlaklık için hızlı ayarlar
- Pano geçmişi (Win+Alt+V) ve ekran görüntüleri
- "Hey Ada" ile isteğe bağlı sesli asistan
- Görev çubuğu tek tıkla geri gelir

## Arama terimleri (Search terms, en çok 7)

görev çubuğu, taskbar, kişiselleştirme, dock, sesli asistan, masaüstü, customization

## Diğer alanlar

- Telif hakkı: © 2026 VirtusTech
- Geliştiren: VirtusTech
- Ek sistem gereksinimleri: Windows 11 (64 bit)

## Sertifikasyon notları (Notes for certification)

Camada is a desktop shell that replaces the Windows taskbar with a floating "island" at the top of the screen.
- It hides the Windows taskbar only when the user chooses "Görev çubuğunu gizle" (Hide taskbar) in the island's
  right-click menu; new installs keep the taskbar visible.
- A second Start menu entry, "Görev çubuğunu geri getir" (Restore taskbar), closes the app and restores the taskbar.
- Uninstalling or updating the app while the taskbar is hidden restores it (tested).
- The optional voice assistant is off by default and requires the user's own Google Gemini API key; no test account is
  needed.
- UI language: English and Turkish, following the Windows display language ("Dil / Language" in the island's right-click
  menu switches it). The optional voice assistant Ada speaks Turkish for now.

## runFullTrust gerekçesi (restricted capability justification)

Camada is a desktop shell (taskbar replacement) built with WPF. It needs full trust to host the system tray (notification
area), enumerate and switch top-level windows, register as an app bar, hide/restore the Windows taskbar, control audio
devices and screen brightness, and register global hotkeys. These are Win32 APIs that are not available to
AppContainer apps.

## English listing

Partner Center → Store listings → add **English (United States)**, then paste these. The UI follows the Windows display
language; Ada speaks Turkish for now.

### Short description

A glass island that replaces the taskbar: apps, music, quick settings, clipboard and the voice assistant Ada in one place.

### Description

Camada replaces the Windows taskbar with a sleek glass island at the top of the screen. The island is small by default and
shows the time and the music that is playing. Hover over it and it expands: pinned apps, open windows, the system tray,
Wi‑Fi, Bluetooth, volume, brightness, a calendar, clipboard history and screenshots are one click away.

The optional voice assistant Ada wakes up when you say "Hey Ada": it opens apps, adjusts volume and brightness, searches the
web, takes notes, sets reminders and tells you the weather. Ada runs on your own Google Gemini API key, is off by default and
speaks Turkish for now.

Hiding the Windows taskbar is entirely your choice and can be undone with one click. The "Restore taskbar" entry in the Start
menu always brings the taskbar back, and uninstalling the app restores it automatically.

### Product features

- A glass island at the top of the screen that replaces the taskbar
- Now playing and the clock at a glance
- Pinned apps, open windows and the system tray
- Quick settings for Wi‑Fi, Bluetooth, volume and brightness
- Clipboard history (Win+Alt+V) and screenshots
- Optional voice assistant with "Hey Ada"
- The taskbar comes back with one click

### Search terms (max 7)

taskbar, glass, customization, dock, voice assistant, desktop, Windows 11
