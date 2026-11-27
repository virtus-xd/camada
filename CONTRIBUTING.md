# Contributing

Thanks for your interest in Camada! This guide explains how to build and try the code and how to send a change. ([Türkçe](#türkçe) below.)

## Before you start

- Open an [issue](https://github.com/virtus-xd/camada/issues) for a bug or an idea, or comment on an existing one.
- If you are planning a large change (a new panel, a new Ada skill, an architectural change), let's discuss it in an issue before you write code, so your effort isn't wasted.

## Requirements

- Windows 11 22H2 or later, x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build and test

```powershell
dotnet build CamAda.slnx
dotnet test tests/CamAda.Core.Tests
```

## Taskbar safety net

Hiding the Windows taskbar is off by default; it is turned on and off with "Görev çubuğunu gizle" (Hide taskbar) in the island's right-click menu. On its first run Camada registers itself to start at sign-in; "Windows açılınca başlat" (Start with Windows) in the same menu turns that off. Create the restore shortcut **before** running the app from source:

```powershell
dotnet build CamAda.slnx
powershell -ExecutionPolicy Bypass -File .\tools\create-restore-shortcut.ps1 -ExePath .\src\CamAda\bin\Debug\net10.0-windows10.0.22621.0\CamAda.exe
```

If the taskbar stays hidden, open the "Görev çubuğunu geri getir" shortcut on your desktop or run `CamAda.exe --restore-taskbar`. If Camada closes unexpectedly, the watchdog restores the taskbar automatically.

A change that touches the code that hides or restores the taskbar, or the watchdog (`src/CamAda/Safety`), must keep these paths working. Describe in such a PR what you tested and how.

## Code layout

- `src/CamAda.Core`: platform-independent logic (`net10.0`). Everything here is covered by xUnit tests. For new logic, **write the test first**, watch it fail, then write the code.
- `src/CamAda`: the WPF UI and Windows integration (taskbar, tray, audio, Wi‑Fi, Ada).
- `tests/CamAda.Core.Tests`: tests for `CamAda.Core`.
- `packaging/` and `tools/package.ps1`: the Microsoft Store (MSIX) package.
- `docs/superpowers/specs/`: design decisions and their reasoning (Turkish). If you wonder why something works the way it does, look here first.

## Project rules

- **Trust:** no Camada feature may require generating certificates, adding certificates to the trust store, developer mode, admin rights or unofficial install routes; changes that need them are not accepted. Undocumented Windows interfaces are not used where a documented way exists. Where there is no other way, they are kept in their own file with the reason written down; today's cases are `IPolicyConfig` for changing the default audio device (`src/CamAda/Audio/PolicyConfig.cs`) and ManagedShell's system tray hosting.
- **Secrets:** never commit API keys, tokens, passwords or personal folder paths. The Gemini key and the Spotify Client ID are entered by the user in the app.
- **Language:** UI text is Turkish (an English UI is planned). Code comments may be in Turkish or English.
- **Commit messages:** short, in Turkish or English, with a prefix: `feat:`, `fix:`, `docs:`, `build:`, `test:`.

## Sending a pull request

1. Fork the repository and create a new branch.
2. Make your change together with its tests; `dotnet test tests/CamAda.Core.Tests` must pass.
3. Open a PR and answer the questions in the template. CI builds on Windows and runs the tests; nothing is merged until it is green.

## License

Camada is licensed under the [Apache License 2.0](LICENSE). Contributions you submit are accepted under the same license (Section 5); no separate agreement is needed.

---

## Türkçe

Camada'ya katkı vermek istediğiniz için teşekkürler!

- **Başlamadan önce:** Bir hata ya da fikir için önce [issue](https://github.com/virtus-xd/camada/issues) açın ya da var olanın altına yazın. Büyük bir değişiklik (yeni panel, yeni Ada işlevi, mimari değişiklik) düşünüyorsanız kod yazmadan önce issue'da konuşalım.
- **Gereksinimler:** Windows 11 22H2 ya da sonrası (x64), .NET 10 SDK.
- **Derleme ve test:** `dotnet build CamAda.slnx` ve `dotnet test tests/CamAda.Core.Tests`.
- **Görev çubuğu güvenlik ağı:** Windows görev çubuğunu gizleme varsayılan olarak kapalıdır; ada sağ tık menüsündeki "Görev çubuğunu gizle" ile açılıp kapanır. Camada ilk açılışta oturum açılışında başlamak için kendini kaydeder; aynı menüdeki "Windows açılınca başlat" bunu kapatır. Kaynaktan çalıştırmadan **önce** kurtarma kısayolunu oluşturun: `powershell -ExecutionPolicy Bypass -File .\tools\create-restore-shortcut.ps1 -ExePath <CamAda.exe yolu>`. Görev çubuğu gizli kalırsa masaüstündeki "Görev çubuğunu geri getir" kısayolunu açın ya da `CamAda.exe --restore-taskbar` çalıştırın. Görev çubuğunu gizleyen/geri getiren koda ya da bekçiye (`src/CamAda/Safety`) dokunan PR bu yolları bozmamalı; neyi nasıl denediğinizi yazın.
- **Kodun yapısı:** Windows'a bağlı olmayan mantık `src/CamAda.Core`'da (`net10.0`) ve xUnit ile test edilir; yeni mantık için önce testi yazın, kırmızı olduğunu görün, sonra kodu yazın. WPF arayüzü ve Windows entegrasyonu `src/CamAda`'da. Store paketi `packaging/` ve `tools/package.ps1`. Tasarım kararları `docs/superpowers/specs/`'te (Türkçe).
- **Güven:** Hiçbir özellik sertifika üretmeyi, güven deposuna sertifika eklemeyi, geliştirici modunu, yönetici iznini ya da resmi olmayan kurulum yollarını gerektirmez; bunları gerektiren değişiklikler kabul edilmez. Belgelenmiş bir yolu olan işte belgelenmemiş Windows arayüzü kullanılmaz. Başka yolu olmayan yerde ayrı bir dosyada tutulur ve gerekçesi yazılır; bugünkü örnekler varsayılan ses cihazını değiştiren `IPolicyConfig` (`src/CamAda/Audio/PolicyConfig.cs`) ve ManagedShell'in sistem tepsisini barındırmasıdır.
- **Gizli bilgi:** API anahtarı, belirteç, parola ya da kişisel klasör yolu commit etmeyin; Gemini anahtarı ve Spotify Client ID uygulamada girilir.
- **Dil ve commit:** Arayüz metinleri Türkçe (İngilizce arayüz planlanıyor), yorumlar Türkçe ya da İngilizce olabilir. Commit mesajları kısa, `feat:`/`fix:`/`docs:`/`build:`/`test:` önekiyle.
- **PR:** Depoyu çatallayın, yeni dal açın, değişikliği testleriyle yapın, PR şablonunu doldurun. CI Windows'ta derleyip testleri çalıştırır; yeşil olmadan birleştirme yapılmaz.
- **Lisans:** Katkılar [Apache License 2.0](LICENSE) Madde 5 gereği aynı lisansla kabul edilir; ayrı bir sözleşme gerekmez.
