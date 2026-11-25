# Ada ile Anında Ekran Görüntüsü Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ada'nın `ekran_goruntusu` işlevi seçme aracını açmak yerine tüm ekranın görüntüsünü hemen alır, Ekran Görüntüleri klasörüne kaydeder ve panoya kopyalar.

**Architecture:** Dosya adı ve işlev açıklaması Core'da (birim testli). Uygulamada küçük bir GDI yardımcısı (`ScreenCapture`) sanal ekranı BitBlt ile WPF `BitmapSource`'a çeker ve PNG kaydeder; `AdaToolRunner.Screenshot` bunu çağırır ve görüntüyü panoya koyar.

**Tech Stack:** .NET 10, WPF (`Imaging.CreateBitmapSourceFromHBitmap`, `PngBitmapEncoder`, `Clipboard.SetImage`), user32/gdi32 P/Invoke, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-26-ada-ekran-goruntusu-design.md`

## Global Constraints

- Tuş gönderilmez.
- Dosya adı `Ekran görüntüsü yyyy-MM-dd HHmmss.png`; aynı adda dosya varsa ` (2)`, ` (3)`… (`.png`'den önce).
- Klasör `ScreenshotLibrary.KnownFolder()`; yoksa oluşturulur.
- Ada'nın sonucu:
  - başarıda `"tamam: ekran görüntüsü alındı ve panoya kopyalandı"`;
  - pano meşgulse `"tamam: ekran görüntüsü alındı (panoya kopyalanamadı)"`;
  - kaydedilemezse `"hata: ekran görüntüsü kaydedilemedi"`.
- Adadaki hızlı ayarlardaki ekran görüntüsü düğmesi (`QuickLauncher.Screenshot` → `ms-screenclip:`) değişmez.
- Debug denemesi ekranın içeriğini günlüğe yazmaz. Ekran görüntüsünü geçici klasöre kaydeder, yalnız boyutu ve boş olmadığını yazar,
  dosyayı siler. Kullanıcının panosuna dokunmaz.
- Testler: `dotnet test tests/CamAda.Core.Tests`; `dotnet build src/CamAda -c Release` 0 uyarı.

## Review Focus

1. Negatif koordinatlı ya da farklı ölçekli ikinci monitör: sanal ekranın tamamı kaymadan çekilmeli. Görev 2 denemesi sanal ekran
   dikdörtgenini ve görüntü boyutunu yazar; ikisi aynı olmalı.
2. Ekran Görüntüleri klasörü yok ya da taşınmış (OneDrive): bulunan yola kaydedilmeli, yoksa oluşturulmalı. Görev 2 denemesi var
   olmayan bir geçici alt klasöre kaydeder.
3. Aynı saniyede iki görüntü: ikincisi ` (2)` almalı. Görev 2 denemesi aynı saniyede iki kez kaydeder.
4. Pano meşgul: görüntü yine kaydedilmeli, Ada panonun kopyalanamadığını söylemeli. Yürütücüde `ExternalException` yakalanır;
   yapı gereği.
5. Çekim başarısız (ör. güvenli masaüstü, GDI hatası): çökme yok, `"hata: ekran görüntüsü kaydedilemedi"`. `CaptureAllScreens`
   GDI başarısızlığında null döner; yürütücü hata verir.

---

### Task 1: Dosya adı ve işlev açıklaması (Core)

**Files:**
- Create: `src/CamAda.Core/Clipboard/ScreenshotName.cs`
- Modify: `src/CamAda.Core/Assistant/AdaTools.cs` (`EkranGoruntusu` açıklaması)
- Test: `tests/CamAda.Core.Tests/Clipboard/ScreenshotNameTests.cs`
- Test: `tests/CamAda.Core.Tests/Assistant/AdaToolsTests.cs`

**Interfaces:**
- Produces: `public static class ScreenshotName` with `string For(DateTime now)` → `"Ekran görüntüsü 2026-11-26 021530.png"`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Name_has_date_and_seconds() =>
    Assert.Equal("Ekran görüntüsü 2026-11-26 021530.png", ScreenshotName.For(new DateTime(2026, 11, 26, 2, 15, 30)));
```

`AdaToolsTests` içinde: `ekran_goruntusu` açıklaması `"hemen"` ve `"panoya"` içerir, `"aracını açar"` içermez.

- [ ] **Step 2: Run to verify RED**

Run: `dotnet test tests/CamAda.Core.Tests --filter "FullyQualifiedName~ScreenshotName|FullyQualifiedName~AdaTools"`
Expected: derleme hatası (`ScreenshotName` yok).

- [ ] **Step 3: Implement**

- `ScreenshotName.For`: `InvariantCulture` ile biçimlenir.
- Açıklama: `"Tüm ekranın görüntüsünü hemen alır, Ekran Görüntüleri klasörüne kaydeder ve panoya kopyalar."`

- [ ] **Step 4: Run to verify GREEN**

Run: `dotnet test tests/CamAda.Core.Tests`
Expected: hepsi Başarılı.

- [ ] **Step 5: Commit**

`git commit -m "feat: ekran görüntüsü adı ve Ada açıklaması (Core)"`

### Task 2: Ekranı çekme ve Ada'ya bağlama

**Files:**
- Create: `src/CamAda/Clips/ScreenCapture.cs`
- Modify: `src/CamAda/Assistant/AdaToolRunner.cs` (`Screenshot()`)
- Create: `src/CamAda/Assistant/AdaProbes.Screen.cs` (Debug, `--ada-ekran-sina`)
- Modify: `src/CamAda/Assistant/AdaProbes.cs` (kayıt)

**Interfaces:**
- Consumes: `ScreenshotName.For`, `ScreenshotLibrary.KnownFolder()`.
- Produces: `internal static class ScreenCapture`:
  - `BitmapSource? CaptureAllScreens()`: sanal ekran `GetSystemMetrics(76..79)`; `BitBlt` ile `SRCCOPY | CAPTUREBLT`; GDI
    başarısızsa null; dondurulmuş (`Freeze`).
  - `string Save(BitmapSource image, string folder, DateTime now)`: klasörü oluşturur, benzersiz adla PNG yazar, yolu döner;
    hatada `IOException` / `UnauthorizedAccessException` atar.
  - `(int X, int Y, int Width, int Height) VirtualScreen()`.

- [ ] **Step 1: Write the probe (the failing check)**

`--ada-ekran-sina`:
- Sanal ekranı ve çekilen görüntünün boyutunu yazar.
- Görüntünün boş olmadığını yazar: piksellerin hepsi aynı değilse "boş değil".
- Var olmayan bir geçici alt klasöre aynı saniyede iki kez kaydeder; adları ve dosya boyutlarını yazar.
- Klasörü siler. Panoya dokunmaz.

- [ ] **Step 2: Run to verify RED**

Run: scratchpad `probe.ps1 -ProbeArgs "--ada-ekran-sina"`
Expected: derleme hatası (`ScreenCapture` yok).

- [ ] **Step 3: Implement `ScreenCapture` and the runner**

Yürütücü:
- `CaptureAllScreens()` null → `"hata: ekran görüntüsü kaydedilemedi"`.
- `Save(image, ScreenshotLibrary.KnownFolder() ?? <Resimler\Screenshots>, DateTime.Now)` hata → aynı hata metni.
- Ardından `Clipboard.SetImage(image)`; `ExternalException` → `"tamam: ekran görüntüsü alındı (panoya kopyalanamadı)"`; yoksa
  `"tamam: ekran görüntüsü alındı ve panoya kopyalandı"`.

- [ ] **Step 4: Run the probe**

Expected:
- Görüntü boyutu = sanal ekran boyutu, "boş değil".
- İki dosya: `Ekran görüntüsü … .png` ve `… (2).png`, ikisi de > 0 bayt.
- Klasör oluşturuldu ve silindi.

Tekrar: `dotnet test tests/CamAda.Core.Tests` Başarılı; Release 0 uyarı.

- [ ] **Step 5: Commit**

`git commit -m "feat: Ada ile anında tüm ekran görüntüsü"`
