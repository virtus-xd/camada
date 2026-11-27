# Camada

**Türkçe** · [English](README.en.md)

[![CI](https://github.com/virtus-xd/camada/actions/workflows/ci.yml/badge.svg)](https://github.com/virtus-xd/camada/actions/workflows/ci.yml) · Lisans: [Apache 2.0](LICENSE)

Camada, Windows görev çubuğunun yerine ekranın üstünde duran şık bir cam ada koyar. Ada normalde küçüktür; saati ve çalan müziği gösterir. Üzerine gelince genişler: sabitlediğiniz uygulamalar, açık pencereler, sistem tepsisi, Wi‑Fi, Bluetooth, ses, parlaklık, takvim, pano geçmişi ve ekran görüntüleri tek dokunuş uzağınızdadır.

İsteğe bağlı sesli asistan Ada, "Hey Ada" dediğinizde uyanır: uygulama açar, ses ve parlaklığı ayarlar, web'de arar, not alır, hatırlatma kurar ve hava durumunu söyler. Ada kendi Google Gemini API anahtarınızla çalışır ve varsayılan olarak kapalıdır.

<!-- Ekran görüntüsü: hakkı olan bir duvar kâğıdıyla eklenecek -->

## Özellikler

- Görev çubuğunun yerine ekranın üstünde duran cam ada
- Çalan müzik ve saat bir bakışta
- Sabitlenmiş uygulamalar, açık pencereler ve sistem tepsisi
- Wi‑Fi, Bluetooth, ses ve parlaklık için hızlı ayarlar
- Pano geçmişi (Win+Alt+V) ve ekran görüntüleri
- "Hey Ada" ile isteğe bağlı sesli asistan
- Görev çubuğu tek tıkla geri gelir

## Durum

Erken sürüm. Arayüz şimdilik yalnız Türkçe; İngilizce arayüz planlanıyor. Camada yakında Microsoft Store'da da olacak.

## Görev çubuğu güvenlik ağı

Kaynaktan derlenen sürüm **ilk açılışta** Windows görev çubuğunu gizler ve oturum açılışında kendiliğinden başlamak için kendini kaydeder. İkisini de adaya sağ tıklayınca açılan menüden kapatabilirsiniz: **"Görev çubuğunu gizle"** ve **"Windows açılınca başlat"**. Uygulamayı kapatmak için aynı menüde **"Çıkış"** vardır. Görev çubuğu her zaman geri getirilebilir:

- `CamAda.exe --restore-taskbar` adayı kapatır ve Windows görev çubuğunu geri getirir. Ayarı değiştirmez: Camada bir sonraki açılışta görev çubuğunu yine gizler. Kalıcı olarak kapatmak için menüdeki "Görev çubuğunu gizle" işaretini kaldırın.
- Bunun için masaüstüne bir kısayol oluşturabilirsiniz: `powershell -ExecutionPolicy Bypass -File .\tools\create-restore-shortcut.ps1 -ExePath <CamAda.exe yolu>` ("Görev çubuğunu geri getir").
- Microsoft Store sürümünde Başlat menüsünde ayrıca **"Görev çubuğunu geri getir"** girişi bulunur.
- Camada beklenmedik şekilde kapanırsa ayrı çalışan bir bekçi görev çubuğunu kendiliğinden geri getirir.
- Beklenmeyen hatalar `%LOCALAPPDATA%\CamAda\crash.log` dosyasına yazılır; hata bildirirken bu dosyayı paylaşabilirsiniz.

## Gereksinimler

- Windows 11 22H2 ya da sonrası, x64
- Kaynaktan derlemek için [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Ada için kendi Google Gemini API anahtarınız (isteğe bağlı)
- Spotify için kendi Spotify geliştirici uygulamanızın Client ID'si (isteğe bağlı)

## Kaynaktan derleme

```powershell
git clone https://github.com/virtus-xd/camada.git
cd camada
dotnet build CamAda.slnx
dotnet test tests/CamAda.Core.Tests
```

Çalıştırmadan **önce** kurtarma kısayolunu oluşturun, sonra uygulamayı başlatın. İlk açılışta görev çubuğu gizlenir ve Camada oturum açılışında başlamak için kendini kaydeder (bkz. [Görev çubuğu güvenlik ağı](#görev-çubuğu-güvenlik-ağı)).

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\create-restore-shortcut.ps1 -ExePath .\src\CamAda\bin\Debug\net10.0-windows10.0.22621.0\CamAda.exe
dotnet run --project src/CamAda
```

Kalıcı kurulum için `.\tools\install.ps1`: Release sürümünü derler, `%LOCALAPPDATA%\Programs\CamAda` klasörüne kurar, kurtarma kısayolunu oluşturur ve Camada'yı başlatır. Camada bundan sonra oturum açılışında kendiliğinden başlar. PowerShell betik çalıştırmayı engelliyorsa: `powershell -ExecutionPolicy Bypass -File .\tools\install.ps1` (yalnız bu komut için geçerlidir).

## Kaldırma

1. Adaya sağ tıklayın, "Windows açılınca başlat" ve "Görev çubuğunu gizle" işaretlerini kaldırın, sonra "Çıkış"ı seçin.
2. Oturum açılış kaydı kaldıysa silin (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` altındaki `CamAda` değeri):
   `Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name CamAda -ErrorAction SilentlyContinue`
3. Kurulum dosyalarını silin: `%LOCALAPPDATA%\Programs\CamAda` klasörü ve masaüstündeki "Görev çubuğunu geri getir" kısayolu.
4. Ayarları ve Ada verilerini de silmek isterseniz: `%APPDATA%\CamAda` ve `%LOCALAPPDATA%\CamAda`. Ada'nın notları (Belgeler\Ada Notları) ve ekran görüntüleri (Resimler) yerinde kalır.

Microsoft Store sürümü Windows Ayarlar → Uygulamalar'dan kaldırılır; görev çubuğu kendiliğinden geri gelir.

## Proje yapısı

| Klasör | İçerik |
| --- | --- |
| `src/CamAda.Core` | Windows'a bağlı olmayan, test edilen mantık (`net10.0`) |
| `src/CamAda` | WPF arayüzü ve Windows entegrasyonu: ada, paneller, tepsi, ses, Ada |
| `tests/CamAda.Core.Tests` | `CamAda.Core` için xUnit testleri |
| `tools/` | Kurulum, kurtarma kısayolu ve yardımcı betikler |
| `docs/superpowers/specs/` | Tasarım kararları ve gerekçeleri (Türkçe) |

## Gizlilik

Camada hesap açtırmaz, reklam göstermez, kullanım istatistiği toplamaz. İnternete yalnız sizin açtığınız özellikler çıkar: Ada (Google Gemini, kendi anahtarınızla), hava durumu (Open-Meteo) ve Spotify. "Hey Ada" uyandırma sözcüğü yalnız bilgisayarınızda dinlenir. Gemini anahtarı, Ada'nın hafızası ve Spotify belirteci bilgisayarınızda Windows DPAPI ile şifreli saklanır. Ayrıntılar: [gizlilik politikası](https://camada-azure.vercel.app/gizlilik).

## Katkı, güvenlik ve destek

- Katkı vermek için: [CONTRIBUTING.md](CONTRIBUTING.md)
- Güvenlik açığı bildirmek için: [SECURITY.md](SECURITY.md)
- Davranış kuralları: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- Projeyi desteklemek isterseniz: [GitHub Sponsors](https://github.com/sponsors/virtus-xd)

## Lisans

Camada [Apache License 2.0](LICENSE) ile lisanslıdır; üçüncü taraf bileşenler [NOTICE](NOTICE) dosyasında listelenir. "Camada" adı ve logosu lisansa dahil değildir.
