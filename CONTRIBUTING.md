# Katkı rehberi

Camada'ya katkı vermek istediğiniz için teşekkürler. Bu belge kodu nasıl derleyip deneyeceğinizi ve bir değişikliği nasıl göndereceğinizi anlatır. (English summary below.)

## Başlamadan önce

- Bir hata ya da fikir için önce [issue](https://github.com/virtus-xd/camada/issues) açın ya da var olanın altına yazın.
- Büyük bir değişiklik (yeni panel, yeni Ada işlevi, mimari değişiklik) düşünüyorsanız kod yazmadan önce issue'da konuşalım; böylece emeğiniz boşa gitmez.

## Gereksinimler

- Windows 11 22H2 ya da sonrası, x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Derleme ve test

```powershell
dotnet build CamAda.slnx
dotnet test tests/CamAda.Core.Tests
```

## Görev çubuğu güvenlik ağı

Kaynaktan derlenen Camada **ilk açılışta** Windows görev çubuğunu gizler ve oturum açılışında başlamak için kendini kaydeder; ikisini de ada sağ tık menüsündeki "Görev çubuğunu gizle" ve "Windows açılınca başlat" ile kapatabilirsiniz. Uygulamayı çalıştırmadan **önce** kurtarma kısayolunu oluşturun:

```powershell
dotnet build CamAda.slnx
powershell -ExecutionPolicy Bypass -File .\tools\create-restore-shortcut.ps1 -ExePath .\src\CamAda\bin\Debug\net10.0-windows10.0.22621.0\CamAda.exe
```

Görev çubuğu gizli kalırsa masaüstündeki "Görev çubuğunu geri getir" kısayolunu açın ya da `CamAda.exe --restore-taskbar` çalıştırın. Camada beklenmedik şekilde kapanırsa bekçi görev çubuğunu kendiliğinden geri getirir.

Görev çubuğunu gizleyen ya da geri getiren koda, ya da bekçiye (`src/CamAda/Safety`) dokunan bir değişiklik bu yolları bozmamalı. Böyle bir PR'da neyi nasıl denediğinizi yazın.

## Kodun yapısı

- `src/CamAda.Core`: Windows'a bağlı olmayan mantık (`net10.0`). Burada yazılan her şey xUnit ile test edilir. Yeni mantık için **önce test** yazın, testin kırmızı olduğunu görün, sonra kodu yazın.
- `src/CamAda`: WPF arayüzü ve Windows entegrasyonu (görev çubuğu, tepsi, ses, Wi‑Fi, Ada).
- `tests/CamAda.Core.Tests`: `CamAda.Core` testleri.
- `docs/superpowers/specs/`: tasarım kararları ve gerekçeleri (Türkçe). Bir şeyin neden öyle olduğunu merak ederseniz önce buraya bakın.

## Proje kuralları

- **Güven:** Camada'nın hiçbir özelliği sertifika üretmeyi, güven deposuna sertifika eklemeyi, geliştirici modunu, yönetici iznini ya da resmi olmayan kurulum yollarını gerektirmez; bunları gerektiren değişiklikler kabul edilmez. Belgelenmiş bir yolu olan işte belgelenmemiş Windows arayüzü kullanılmaz. Başka yolu olmayan yerde ayrı bir dosyada tutulur ve gerekçesi yazılır; bugünkü örnekler varsayılan ses cihazını değiştiren `IPolicyConfig` (`src/CamAda/Audio/PolicyConfig.cs`) ve ManagedShell'in sistem tepsisini barındırmasıdır.
- **Gizli bilgi:** API anahtarı, belirteç (token), parola ya da kişisel klasör yolu commit etmeyin. Gemini anahtarı ve Spotify Client ID kullanıcı tarafından uygulamada girilir.
- **Dil:** Arayüz metinleri Türkçe (İngilizce arayüz planlanıyor). Kod yorumları Türkçe ya da İngilizce olabilir.
- **Commit mesajları:** kısa, Türkçe ya da İngilizce; önek kullanın: `feat:`, `fix:`, `docs:`, `build:`, `test:`.

## Değişiklik isteği (PR) göndermek

1. Depoyu çatallayın (fork) ve yeni bir dal açın.
2. Değişikliğinizi testleriyle birlikte yapın; `dotnet test tests/CamAda.Core.Tests` geçsin.
3. PR açın ve şablondaki soruları yanıtlayın. Otomatik kontrol (CI) Windows'ta derleyip testleri çalıştırır; yeşil olmadan birleştirme yapılmaz.

## Lisans

Camada [Apache License 2.0](LICENSE) ile lisanslıdır. Gönderdiğiniz katkılar, lisansın 5. maddesi gereği aynı lisansla kabul edilir; ayrıca bir sözleşme imzalamanız gerekmez.

---

## In English

Thanks for your interest in Camada!

- **Before you start:** open or comment on an [issue](https://github.com/virtus-xd/camada/issues); discuss large changes first.
- **Requirements:** Windows 11 22H2+ (x64), .NET 10 SDK.
- **Build and test:** `dotnet build CamAda.slnx` and `dotnet test tests/CamAda.Core.Tests`.
- **Taskbar safety net:** on its first run a build from source hides the Windows taskbar and registers itself to start at sign-in; turn both off with "Görev çubuğunu gizle" and "Windows açılınca başlat" in the island's right-click menu. Before running from source, create the restore shortcut with `powershell -ExecutionPolicy Bypass -File .\tools\create-restore-shortcut.ps1 -ExePath <path to CamAda.exe>`. If the taskbar stays hidden, run `CamAda.exe --restore-taskbar`. Changes touching taskbar hiding or the watchdog (`src/CamAda/Safety`) must keep these paths working.
- **Structure:** platform-independent logic lives in `src/CamAda.Core` and is covered by xUnit tests (write the test first); WPF and Windows integration live in `src/CamAda`. Design decisions are documented in Turkish in `docs/superpowers/specs/`.
- **Rules:** no feature may require certificates, trust-store changes, developer mode, admin rights or unofficial install routes. Undocumented Windows interfaces are used only where no documented alternative exists, kept in their own file with the reason written down (today: `IPolicyConfig` for the default audio device, and ManagedShell's tray hosting). Never commit API keys, tokens or personal paths. The UI is Turkish for now; comments may be Turkish or English. Use `feat:`/`fix:`/`docs:`/`build:`/`test:` commit prefixes.
- **License:** contributions are accepted under the [Apache License 2.0](LICENSE) (Section 5); no separate agreement is needed.
