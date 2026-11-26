# Camada — Partner Center gönderim metinleri

Partner Center'daki alanlara kopyalanacak metinler. İlk gönderim gizli (özel hedef kitle); Türkçe. İngilizce liste 6. işte.

## Fiyat ve görünürlük

- Fiyat: Ücretsiz
- Görünürlük: Özel hedef kitle (yalnız geliştiricinin Microsoft hesabı); "Store'da bulunabilir" kapalı
- Kategori: Kişiselleştirme (yoksa: Yardımcı programlar ve araçlar)
- Gizlilik politikası adresi: https://camada-azure.vercel.app/gizlilik (geçici; Vercel projesi "camada", `site/gizlilik.html`)
- Destek iletişimi: erdemoz2003@gmail.com

## Kısa açıklama

Görev çubuğunun yerine geçen cam ada: uygulamalar, müzik, hızlı ayarlar, pano ve sesli asistan Ada tek yerde.

## Açıklama

Camada, Windows görev çubuğunun yerine ekranın üstünde duran şık bir cam ada koyar. Ada normalde küçüktür; saati, çalan
müziği ve bildirimleri gösterir. Üzerine gelince genişler: sabitlediğiniz uygulamalar, açık pencereler, sistem tepsisi,
Wi‑Fi, Bluetooth, ses, parlaklık, takvim, pano geçmişi ve ekran görüntüleri tek dokunuş uzağınızdadır.

İsteğe bağlı sesli asistan Ada, "Hey Ada" dediğinizde uyanır: uygulama açar, ses ve parlaklığı ayarlar, web'de arar,
not alır, hatırlatma kurar ve hava durumunu söyler. Ada kendi Google Gemini API anahtarınızla çalışır ve varsayılan
olarak kapalıdır.

Windows görev çubuğunu gizlemek tamamen sizin seçiminizdir ve tek tıkla geri alınır. Başlat menüsündeki
"Görev çubuğunu geri getir" girişi her zaman görev çubuğunu geri getirir; uygulamayı kaldırdığınızda görev çubuğu
kendiliğinden geri gelir.

## Sertifikasyon notları (Notes for certification)

Camada is a desktop shell that replaces the Windows taskbar with a floating "island" at the top of the screen.
- It hides the Windows taskbar only when the user chooses "Görev çubuğunu gizle" (Hide taskbar) in the island's
  right-click menu; new installs keep the taskbar visible.
- A second Start menu entry, "Görev çubuğunu geri getir" (Restore taskbar), closes the app and restores the taskbar.
- Uninstalling or updating the app while the taskbar is hidden restores it (tested).
- The optional voice assistant is off by default and requires the user's own Google Gemini API key; no test account is
  needed.
- UI language: Turkish.

## runFullTrust gerekçesi (restricted capability justification)

Camada is a desktop shell (taskbar replacement) built with WPF. It needs full trust to host the system tray (notification
area), enumerate and switch top-level windows, register as an app bar, hide/restore the Windows taskbar, control audio
devices and screen brightness, and register global hotkeys. These are Win32 APIs that are not available to
AppContainer apps.
