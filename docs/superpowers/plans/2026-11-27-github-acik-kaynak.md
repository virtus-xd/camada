# GitHub'da Açık Kaynak Yayımlama — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Depoyu temizlenmiş geçmişiyle, Apache 2.0 lisansı, topluluk dosyaları, iki dilli README ve Windows'ta derleyip test eden iş akışıyla `virtus-xd/camada` adında önce gizli, kullanıcı onayından sonra herkese açık bir GitHub deposu olarak yayımlamak.

**Architecture:** Önce yedek alınır, geçmiş `git filter-repo` ile bir kez yeniden yazılır (kimlik → noreply, metin dosyalarında kişisel ifadeler → nötr karşılıkları) ve iki tarayıcıyla doğrulanır. Sonra `github-acik-kaynak` dalına lisans, topluluk dosyaları, README ve CI eklenir, dal `main`'e ileri sarılır, gizli depoya yalnız `main` ile `plan-10-store-paketi` yüklenir. Herkese açma ayrı ve kullanıcının açık onayına bağlı son görevdir.

**Tech Stack:** git 2.53, git-filter-repo 2.47.0 (Python 3.12), gh CLI (virtus-xd, `repo` yetkisi), GitHub Actions (`actions/checkout@v7`, `actions/setup-dotnet@v6`), .NET 10 SDK (yerelde 10.0.404), xUnit.

**Spec:** `docs/superpowers/specs/2026-11-27-github-acik-kaynak-design.md`

## Global Constraints

- Lisans Apache License 2.0; telif satırı `Copyright 2026 VirtusTech`.
- Depo `virtus-xd/camada`; önce **gizli**. Herkese açma (Görev 6) yalnız kullanıcı bu oturumda açıkça "aç" dedikten sonra.
- Yeni commit kimliği `virtus-xd <190945580+virtus-xd@users.noreply.github.com>` (depo yerel ayarı, Görev 1'de); her commit mesajı `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` ile biter.
- Temizlik verileri yalnız `.superpowers/github-temizlik/` altında (git yok sayar; asla commit edilmez, asla yüklenmez): `degistir.json` (eski→yeni çiftleri, sıralı), `mailmap.txt`, `yasak.txt` (kalmaması gereken ifadeler).
- `yasak.txt`'teki hiçbir ifade yeni dosyalara, commit mesajlarına ya da bu plana girmez. Tek istisna: `site/gizlilik.html` ve `packaging/store-listing.md` içindeki iletişim adresi (kullanıcının onayladığı herkese açık adres). Yeni dosyalar iletişim için adres yazmaz, `https://camada-azure.vercel.app/gizlilik` sayfasına bağlantı verir.
- Store ekran görüntüleri (üçüncü taraf duvar kâğıdı) depoya girmez; README şimdilik görselsiz.
- Uygulama kodu değişmez. Tek istisna: CI koşucusunda (en-US, UTC) çıkan kültür ya da saat dilimi bağımlı test hataları; onlar testi kültür ve saatten bağımsız yaparak düzeltilir, atlanmaz.
- GitHub'a yalnız `main` ve `plan-10-store-paketi` dalları gider; etiket, `refs/once/*` ya da başka ref gitmez.
- Belgeler Türkçe; README'nin tam İngilizce karşılığı (`README.en.md`), SECURITY/CONTRIBUTING/CODE_OF_CONDUCT'ta kısa İngilizce bölüm var.
- Proje kuralı (güven): özellikler sertifika üretme, güven deposu değişikliği, geliştirici modu ya da yönetici izni gerektirmez; resmi olmayan Windows yolları kullanılmaz. CONTRIBUTING'de yazılı olur.
- Yedek klasörü: `$USERPROFILE/camada-yedek-2026-11-27/` (proje dışında, TEMP dışında).

## Review Focus

1. **Yeniden yazma alakasız bir metin dosyasını bozabilir** (eski bir commit'te sayı ya da ad parçası eşleşmesi): uç dallarda eski ve yeni ağaç farkı yalnız beklenen dosyalarda ve yalnız `degistir.json` çiftlerinden oluşmalı → Görev 1 Adım 10.
2. **İstenmeyen ref'in yüklenmesi** (`refs/once/*`, etiket, `github-acik-kaynak`): uzakta tam iki dal olmalı → Görev 5 Adım 5.
3. **CI koşucusu yerel makineden farklı** (en-US kültür, UTC saat dilimi, temiz NuGet önbelleği): ilk CI çalışması yeşil olmalı; kırmızıysa yalnız test kültür/saat bağımsız yapılır → Görev 5 Adım 7.
4. **README'deki komutlar temiz bir kopyada çalışmayabilir** (yok sayılan dosyaya bağımlılık, yanlış yol): GitHub'dan taze klon alınıp README'deki derleme/test komutları harfiyen çalıştırılmalı → Görev 5 Adım 8.
5. **Kişisel kimliğin GitHub'da görünmesi** (yerelde temiz ama yüklenen commit'lerde eski e-posta): GitHub API'sinden gelen tüm commit e-postaları yalnız noreply olmalı → Görev 5 Adım 6.

---

### Task 1: Yedek ve geçmiş temizliği

**Files:**
- Create (depo dışı ya da yok sayılan): `$USERPROFILE/camada-yedek-2026-11-27/once.bundle`, `refs-once.txt`, `test-sayilari.txt`, `commit-map`; `.superpowers/github-temizlik/tara.sh`, `blob.py`, `message.py`
- Hazır (planlayan yazdı): `.superpowers/github-temizlik/degistir.json`, `mailmap.txt`, `yasak.txt`
- Depoda yeni dosya yok; tüm dalların geçmişi yeniden yazılır.

**Interfaces:**
- Produces: temiz geçmiş; `bash .superpowers/github-temizlik/tara.sh` (çıkış 0 = temiz, 1 = ihlal; ihlalleri `KİMLİK:` / `MESAJ:` / `İÇERİK:` başlıklarıyla basar) — Görev 5 de kullanır; depo yerel `user.email` = noreply adresi.

Tüm komutlar Git Bash'te, depo kökünde.

- [ ] **Step 1: Başlangıç durumunu doğrula**

Run: `git status --porcelain; git stash list; git branch --format='%(refname:short)'`
Expected: `git status` yalnız `?? out/` gösterir (Store paket çıktısı; `out/` kuralı yalnız Store dalının `.gitignore`'unda, `main`'de yok — dokunma, asla commit etme, commit'lerde hep açık yol ver); stash boş; dallar tam olarak `github-acik-kaynak`, `main`, `plan-10-store-paketi`.

- [ ] **Step 2: Test sayılarını kaydet**

Her dalda (`main`, `plan-10-store-paketi`) `git switch <dal> && dotnet test tests/CamAda.Core.Tests` çalıştır, "Başarılı: N" sayısını `test-sayilari.txt`'ye yaz; sonunda `git switch github-acik-kaynak`.
Expected: main 806 (27.11'de ölçüldü), plan-10 için ölçülen sayı; ikisinde de "Başarısız: 0".

- [ ] **Step 3: Yedek al**

```bash
Y="$USERPROFILE/camada-yedek-2026-11-27"; mkdir -p "$Y"
git bundle create "$Y/once.bundle" --all
git bundle verify "$Y/once.bundle"
git for-each-ref --format='%(objectname) %(refname)' > "$Y/refs-once.txt"
for b in main plan-10-store-paketi github-acik-kaynak; do echo "$b $(git rev-list --count $b)"; done >> "$Y/refs-once.txt"
git log --all --format=%B | grep -c 'Co-Authored-By: Claude' >> "$Y/refs-once.txt"
```
Expected: `verify` çıktısında "is okay"; dosyada 3 dal satırı ve sayıları.

- [ ] **Step 4: Tarayıcıyı yaz `.superpowers/github-temizlik/tara.sh`**

Davranış: `yasak.txt`'i sabit metin listesi (`grep -F -f`) olarak kullanır ve üç yerde arar: (1) `git log --all --format='%an <%ae>%n%cn <%ce>'` → `KİMLİK:`; (2) `git log --all --format='%h %B'` → `MESAJ:`; (3) `git rev-list --all | xargs git grep -n -F -f <yasak> -- . ':!site/gizlilik.html' ':!packaging/store-listing.md'` → `İÇERİK:` (ilk 50 satır). Herhangi biri doluysa başlığıyla basar, sonda `ihlal=0|1` yazar ve o kodla çıkar.

- [ ] **Step 5: Tarayıcının ihlal bulduğunu gör (kırmızı)**

Run: `bash .superpowers/github-temizlik/tara.sh; echo "kod=$?"`
Expected: `KİMLİK:` ve `İÇERİK:` bölümleri dolu, `kod=1`.

- [ ] **Step 6: filter-repo'yu kur ve yerel kimliği ayarla**

```bash
python -m pip install --user git-filter-repo==2.47.0
git filter-repo --version
git config user.name virtus-xd
git config user.email 190945580+virtus-xd@users.noreply.github.com
```
Expected: sürüm satırı basılır. `git filter-repo` bulunamazsa `python -c "import sysconfig;print(sysconfig.get_path('scripts','nt_user'))"` klasörünü bu kabuğun PATH'ine ekleyip yeniden dene.

- [ ] **Step 7: Geri çağırma gövdelerini yaz**

`.superpowers/github-temizlik/blob.py` (yalnız NUL içermeyen, yani metin bloblarında; modeller, yazı tipleri, görseller dokunulmaz):
```python
import json, os
pairs = json.load(open(os.environ["CAMADA_DEGISTIR"], encoding="utf-8"))
if b"\0" not in blob.data[:8000]:
    for old, new in pairs:
        blob.data = blob.data.replace(old.encode("utf-8"), new.encode("utf-8"))
```
`.superpowers/github-temizlik/message.py`:
```python
import json, os
pairs = json.load(open(os.environ["CAMADA_DEGISTIR"], encoding="utf-8"))
for old, new in pairs:
    message = message.replace(old.encode("utf-8"), new.encode("utf-8"))
return message
```

- [ ] **Step 8: Geçmişi yeniden yaz**

```bash
T="$PWD/.superpowers/github-temizlik"
CAMADA_DEGISTIR="$T/degistir.json" git filter-repo --force \
  --mailmap "$T/mailmap.txt" \
  --blob-callback "$(cat "$T/blob.py")" \
  --message-callback "$(cat "$T/message.py")"
cp .git/filter-repo/commit-map .git/filter-repo/ref-map "$USERPROFILE/camada-yedek-2026-11-27/"
```
Expected: hatasız biter; `git status --porcelain` yalnız `?? out/`; `.superpowers/` ve `out/` yerinde.

- [ ] **Step 9: Tarayıcı ve sır taraması temiz (yeşil)**

Run: `bash .superpowers/github-temizlik/tara.sh; echo "kod=$?"`
Expected: `ihlal=0`, `kod=0`.
Run: `git log --all -p | grep -E 'AIza[0-9A-Za-z_-]{30,}|gh[pousr]_[A-Za-z0-9]{30,}|sk-[A-Za-z0-9]{20,}|xox[abp]-' | head`
Expected: boş.
Run: `for b in main plan-10-store-paketi github-acik-kaynak; do echo "$b $(git rev-list --count $b)"; done; git log --all --format=%B | grep -c 'Co-Authored-By: Claude'`
Expected: Adım 3'teki sayılarla birebir aynı.

- [ ] **Step 10: Eski ve yeni uç ağaçlarını karşılaştır**

```bash
git fetch -q "$USERPROFILE/camada-yedek-2026-11-27/once.bundle" 'refs/heads/*:refs/once/*'
for b in main plan-10-store-paketi github-acik-kaynak; do git diff --stat refs/once/$b $b; done
git diff refs/once/plan-10-store-paketi plan-10-store-paketi | grep -E '^[-+][^-+]'
```
Expected: değişen dosyalar yalnız `docs/superpowers/**`, `tests/CamAda.Core.Tests/**`, `src/CamAda/Assistant/AdaProbes*.cs`; her `-`/`+` satır çifti yalnız `degistir.json`'daki bir çiftle farklı. Beklenmeyen dosya ya da fark varsa dur, yedekten geri dön (`git fetch` ile `refs/once/*`'tan dalları geri kur) ve kullanıcıya bildir.
Sonra eski ref'leri sil:
```bash
git for-each-ref --format='%(refname)' refs/once | xargs -n1 git update-ref -d
git reflog expire --expire=now --all && git gc -q --prune=now
bash .superpowers/github-temizlik/tara.sh
```
Expected: `ihlal=0`.

- [ ] **Step 11: Derleme ve testler değişmedi**

Her dalda `git switch <dal> && dotnet build CamAda.slnx && dotnet test tests/CamAda.Core.Tests`; sonunda `git switch github-acik-kaynak`.
Expected: derleme "0 Hata"; test sayıları `test-sayilari.txt` ile aynı, "Başarısız: 0".

- [ ] **Step 12: Kayıt**

Commit yok (geçmiş yeniden yazıldı). Yerel notlarda (`.superpowers/sdd/...`, bellek) geçen 27.11 öncesi commit kimliklerinin eşlemesi `$USERPROFILE/camada-yedek-2026-11-27/commit-map`'te.

---

### Task 2: Lisans ve topluluk dosyaları

**Files:**
- Create: `LICENSE`, `NOTICE`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `.github/FUNDING.yml`, `.github/PULL_REQUEST_TEMPLATE.md`, `.github/ISSUE_TEMPLATE/hata.yml`, `.github/ISSUE_TEMPLATE/oneri.yml`, `.github/ISSUE_TEMPLATE/config.yml`

**Interfaces:**
- Consumes: Görev 1'in yerel commit kimliği.
- Produces: README'nin bağlantı verdiği dosya adları (yukarıdaki yollar, birebir).

- [ ] **Step 1: Denetimleri yaz ve kırmızı gör**

Scratchpad'e `denetle-topluluk.sh`: (a) `src/*/*.csproj` içindeki her `PackageReference Include` adı `NOTICE`'ta geçer (büyük/küçük harf duyarsız), (b) `LICENSE` ilk dolu satırı `Apache License` ve `Version 2.0, January 2004` içerir, (c) `.github/` altındaki her `.yml` Python ile (`yaml.safe_load`; yoksa `python -m pip install --user pyyaml`) hatasız okunur, `ISSUE_TEMPLATE/hata.yml` ve `oneri.yml`'de `name`, `description`, `body` anahtarları var, (d) yeni dosyalarda `grep -F -f .superpowers/github-temizlik/yasak.txt` boş. Eksik olanı `EKSİK: …` diye basar, varsa 1 ile çıkar.
Run: `bash <scratchpad>/denetle-topluluk.sh; echo "kod=$?"`
Expected: `EKSİK:` satırları, `kod=1`.

- [ ] **Step 2: `LICENSE` ve `NOTICE`**

`gh api /licenses/apache-2.0 --jq .body > LICENSE` (metin değiştirilmez).
`NOTICE`:
```
Camada
Copyright 2026 VirtusTech

Bu ürün Apache License 2.0 ile lisanslanmıştır (bkz. LICENSE).
"Camada" adı ve logosu bu lisansa dahil değildir (Apache License 2.0, Madde 6).
This product is licensed under the Apache License 2.0. The "Camada" name and logo are not licensed (Section 6).

Üçüncü taraf bileşenler / Third-party components:
- ManagedShell — Apache-2.0 — https://github.com/cairoshell/ManagedShell
- NAudio.Wasapi — MIT — https://github.com/naudio/NAudio
- org.k2fsa.sherpa.onnx (sherpa-onnx) — Apache-2.0 — https://github.com/k2-fsa/sherpa-onnx
- System.Management — MIT — https://github.com/dotnet/runtime
- Anahtar sözcük modeli sherpa-onnx-kws-zipformer-gigaspeech-3.3M — Apache-2.0 — src/CamAda/Assets/WakeWord/LICENSE.txt
- CAM++ konuşmacı modeli (3D-Speaker) — Apache-2.0 — src/CamAda/Assets/Speaker/LICENSE-models.txt
- Silero VAD — MIT — src/CamAda/Assets/Speaker/LICENSE-models.txt
- Manrope yazı tipi — SIL Open Font License 1.1 — src/CamAda/Assets/Fonts/OFL.txt
```

- [ ] **Step 3: `CONTRIBUTING.md`** (Türkçe; sonda kısa "In English" bölümü)

İçermesi gerekenler: önce issue aç ya da var olanı yorumla, büyük değişiklikte önce konuş · gereksinimler: Windows 11 22H2+ x64, .NET 10 SDK · komutlar `dotnet build CamAda.slnx`, `dotnet test tests/CamAda.Core.Tests` · mimari kuralı: Windows'a bağlı olmayan mantık `src/CamAda.Core` (net10.0, xUnit ile test edilir, yeni mantığa önce test), Windows/WPF kodu `src/CamAda` · güvenlik ağı: çalıştırmadan önce `.\tools\create-restore-shortcut.ps1 -ExePath <CamAda.exe yolu>`, görev çubuğu gizli kalırsa `CamAda.exe --restore-taskbar`; görev çubuğunu gizleyen/geri getiren koda ya da bekçiye (`src/CamAda/Safety`) dokunan PR bu yolları bozmamalı · Global Constraints'teki güven kuralı · API anahtarı, belirteç, kişisel yol commit edilmez; anahtarlar uygulamada girilir · commit önekleri `feat:` `fix:` `docs:` `build:` `test:`, Türkçe ya da İngilizce · arayüz metinleri Türkçe (İngilizce arayüz planlı), yorumlar Türkçe ya da İngilizce · katkılar Apache 2.0 Madde 5 gereği aynı lisansla kabul edilir, ayrı sözleşme yok · tasarım kararları `docs/superpowers/specs/`.

- [ ] **Step 4: `CODE_OF_CONDUCT.md` ve `SECURITY.md`** (ikisi de Türkçe + kısa İngilizce)

CODE_OF_CONDUCT: Contributor Covenant 2.1'i benimser (https://www.contributor-covenant.org/version/2/1/code_of_conduct/), kısa Türkçe özet; bildirim için proje sahibine `https://camada-azure.vercel.app/gizlilik` sayfasındaki iletişim adresi.
SECURITY: desteklenen sürüm en son yayın ve `main`; bildirim yalnız GitHub özel bildirimiyle (`https://github.com/virtus-xd/camada/security/advisories/new`), herkese açık issue açılmaz; kapsam örnekleri: görev çubuğunun geri getirilemediği durumlar, Gemini anahtarı / Ada hafızası / Spotify belirtecinin (Windows DPAPI ile şifreli) sızması, mikrofon sesinin Ada kapalıyken ya da izinsiz gönderilmesi; ilk yanıt hedefi 7 gün.

- [ ] **Step 5: `.github` dosyaları**

`FUNDING.yml`: tek satır `github: [virtus-xd]`.
`ISSUE_TEMPLATE/hata.yml` (issue formu): `name: Hata bildirimi / Bug report`, `labels: [bug]`; en üstte markdown notu "Görev çubuğu gizli kaldıysa: `CamAda.exe --restore-taskbar` ya da Başlat menüsündeki 'Görev çubuğunu geri getir'"; alanlar: ne oldu (textarea, zorunlu), ne bekliyordunuz (textarea), adımlar (textarea, zorunlu), Windows sürümü (input, zorunlu, örnek `11 24H2 (26100.xxxx)`), Camada sürümü ya da commit (input), `%LOCALAPPDATA%\CamAda\crash.log` içeriği (textarea, `render: text`, "kişisel bilgi olmadığını kontrol edin").
`ISSUE_TEMPLATE/oneri.yml`: `name: Öneri / Feature request`, `labels: [enhancement]`; alanlar: ihtiyaç ya da sorun (zorunlu), önerilen çözüm, düşünülen alternatifler.
`ISSUE_TEMPLATE/config.yml`: `blank_issues_enabled: true`; `contact_links`: "Güvenlik açığı / Security vulnerability" → `https://github.com/virtus-xd/camada/security/advisories/new`.
`PULL_REQUEST_TEMPLATE.md`: "Ne değişti, neden?" + onay kutuları: `dotnet test` geçti · yeni mantık için test eklendi · görev çubuğu güvenlik ağı etkilenmiyor ya da denendi · anahtar, belirteç, kişisel veri yok.

- [ ] **Step 6: Denetim yeşil**

Run: `bash <scratchpad>/denetle-topluluk.sh; echo "kod=$?"`
Expected: `EKSİK:` yok, `kod=0`.

- [ ] **Step 7: Commit**

```bash
git add LICENSE NOTICE CONTRIBUTING.md CODE_OF_CONDUCT.md SECURITY.md .github
git commit -m "docs: Apache 2.0 lisansı ve topluluk dosyaları"
```

---

### Task 3: README (Türkçe ve İngilizce)

**Files:**
- Create: `README.md` (Türkçe), `README.en.md` (İngilizce, aynı bölümler)

**Interfaces:**
- Consumes: Görev 2'nin dosya adları; Görev 4'ün iş akışı dosyası `.github/workflows/ci.yml` (rozet adresi).

- [ ] **Step 1: Bağlantı denetimini yaz ve kırmızı gör**

Scratchpad'e `denetle-readme.sh`: iki README'de her göreli markdown bağlantısının (`](yol)`, `http` ile başlamayan, `#` çapası atılmış) hedefi depoda var; her iki dosya şu başlıkları içerir (TR / EN): `Görev çubuğu güvenlik ağı` / `Taskbar safety net`, `Kaynaktan derleme` / `Building from source`, `Lisans` / `License`; `--restore-taskbar` ve `crash.log` geçer; `yasak.txt` ifadeleri geçmez. Eksikte `EKSİK: …`, kod 1.
Run: `bash <scratchpad>/denetle-readme.sh; echo "kod=$?"`
Expected: `kod=1` (dosyalar yok).

- [ ] **Step 2: `README.md` yaz**

Sırayla: `# Camada` · `**Türkçe** · [English](README.en.md)` · CI rozeti `[![CI](https://github.com/virtus-xd/camada/actions/workflows/ci.yml/badge.svg)](https://github.com/virtus-xd/camada/actions/workflows/ci.yml)` ve `[Apache 2.0](LICENSE)` · Store metnindeki (`git show plan-10-store-paketi:packaging/store-listing.md`; bu dosya `main`'de yok) açıklamanın ilk iki paragrafı (görev çubuğunun yerine üstte duran cam ada; isteğe bağlı "Hey Ada", kendi Gemini anahtarıyla, varsayılan kapalı) · `<!-- Ekran görüntüsü: hakkı olan bir duvar kâğıdıyla eklenecek -->` · **Özellikler** (Store listesindeki 7 madde) · **Durum**: erken sürüm, arayüz yalnız Türkçe (İngilizce planlı), Microsoft Store'da yakında · **Görev çubuğu güvenlik ağı**: Camada Windows görev çubuğunu gizleyebilir, ada sağ tık menüsündeki "Görev çubuğunu gizle" ile açılıp kapanır; geri getirmek için `CamAda.exe --restore-taskbar` ya da `.\tools\create-restore-shortcut.ps1 -ExePath <CamAda.exe yolu>` ile oluşturulan masaüstü kısayolu (Store sürümünde Başlat menüsünde "Görev çubuğunu geri getir"); Camada beklenmedik şekilde kapanırsa bekçi görev çubuğunu geri getirir; hatalar `%LOCALAPPDATA%\CamAda\crash.log` · **Gereksinimler**: Windows 11 22H2+ x64; derlemek için .NET 10 SDK; Ada için kendi Google Gemini API anahtarınız (isteğe bağlı); Spotify için kendi Spotify geliştirici uygulamanızın Client ID'si (isteğe bağlı) · **Kaynaktan derleme**: `git clone https://github.com/virtus-xd/camada.git`, `cd camada`, `dotnet build CamAda.slnx`, `dotnet test tests/CamAda.Core.Tests`, önce kurtarma kısayolu sonra `dotnet run --project src/CamAda`; kalıcı kurulum `.\tools\install.ps1` (Release derler, `%LOCALAPPDATA%\Programs\CamAda`'ya kurar, kurtarma kısayolunu oluşturur, oturum açılışında başlar) · **Proje yapısı**: `src/CamAda.Core`, `src/CamAda`, `tests/CamAda.Core.Tests`, `tools/`, `docs/superpowers/specs/` (tasarım kararları, Türkçe) birer satır (`site/` ve `packaging/` yalnız Store dalında; `main`'e gelince README'ye eklenir) · **Gizlilik**: Gemini anahtarı, Ada hafızası ve Spotify belirteci bilgisayarda Windows DPAPI ile şifreli; ayrıntı `https://camada-azure.vercel.app/gizlilik` · **Katkı** → `CONTRIBUTING.md`, **Güvenlik** → `SECURITY.md`, **Destek** → `https://github.com/sponsors/virtus-xd` · **Lisans**: Apache 2.0 (`LICENSE`, `NOTICE`); "Camada" adı ve logosu lisansa dahil değildir.

- [ ] **Step 3: `README.en.md` yaz**

Aynı bölümler İngilizce; en üstte `[Türkçe](README.md) · **English**`; başlıklar Step 1'deki İngilizce adlar; arayüzün Türkçe olduğu "Status" bölümünde açıkça yazılır.

- [ ] **Step 4: Denetim yeşil**

Run: `bash <scratchpad>/denetle-readme.sh; echo "kod=$?"`
Expected: `kod=0`. (`.github/workflows/ci.yml` bağlantısı mutlak adres olduğu için Görev 4'ten önce de geçer.)

- [ ] **Step 5: Commit**

```bash
git add README.md README.en.md
git commit -m "docs: Türkçe ve İngilizce README"
```

---

### Task 4: Windows'ta derleyip test eden iş akışı

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Produces: iş akışı adı `CI`, iş adı `build-test` (Görev 6'daki kural bu adı ister).

- [ ] **Step 1: İş akışını yaz**

```yaml
name: CI
on:
  push:
    branches: [main]
  pull_request:
permissions:
  contents: read
jobs:
  build-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'
      - run: dotnet build CamAda.slnx -c Release
      - run: dotnet test tests/CamAda.Core.Tests -c Release --no-build
```

- [ ] **Step 2: Aynı komutları yerelde çalıştır**

Run: `dotnet build CamAda.slnx -c Release && dotnet test tests/CamAda.Core.Tests -c Release --no-build`
Expected: "0 Hata"; `main` için 806 başarılı, "Başarısız: 0".

- [ ] **Step 3: YAML okunur**

Run: `python -c "import yaml,sys;d=yaml.safe_load(open('.github/workflows/ci.yml',encoding='utf-8'));print(list(d['jobs']))"`
Expected: `['build-test']`.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "build: Windows'ta derleme ve test iş akışı"
```

---

### Task 5: `main`'e alma, gizli depo, yükleme ve uzak doğrulama

**Files:** depoda değişiklik yok (CI kırmızıysa yalnız ilgili test dosyası, Adım 7).

**Interfaces:**
- Consumes: Görev 1'in `tara.sh`'i; Görev 4'ün `build-test` işi.

- [ ] **Step 1: Son tarama ve `main`'e ileri sarma**

Run: `bash .superpowers/github-temizlik/tara.sh && git switch main && git merge --ff-only github-acik-kaynak`
Expected: `ihlal=0`; ileri sarma başarılı.

- [ ] **Step 2: Gizli depoyu oluştur**

```bash
gh repo create virtus-xd/camada --private --source . --remote origin --disable-wiki \
  --description "Windows 11 görev çubuğunun yerine geçen cam ada ve sesli asistan Ada · A glass island that replaces the Windows 11 taskbar, with the optional voice assistant Ada"
```
Expected: depo adresi basılır; `git remote -v` `origin` gösterir.

- [ ] **Step 3: Yalnız iki dalı yükle**

Run: `git push -u origin main && git push origin plan-10-store-paketi`

- [ ] **Step 4: Konu etiketleri**

Run: `gh repo edit virtus-xd/camada --add-topic windows-11,taskbar,wpf,dotnet,voice-assistant,desktop-customization,gemini`

- [ ] **Step 5: Uzakta tam iki dal var**

Run: `git ls-remote origin`
Expected: `HEAD`, `refs/heads/main`, `refs/heads/plan-10-store-paketi`; başka ref yok.
Run: `gh api repos/virtus-xd/camada --jq '[.private, .default_branch, .has_wiki] | @tsv'`
Expected: `true	main	false`.

- [ ] **Step 6: GitHub'daki commit e-postaları yalnız noreply**

Run: `for b in main plan-10-store-paketi; do gh api "repos/virtus-xd/camada/commits?sha=$b&per_page=100" --paginate --jq '.[].commit | .author.email, .committer.email'; done | sort -u`
Expected: tek satır `190945580+virtus-xd@users.noreply.github.com`.

- [ ] **Step 7: CI yeşil**

Run: `gh run watch "$(gh run list --branch main --workflow CI --limit 1 --json databaseId --jq '.[0].databaseId')" --exit-status`
Expected: `build-test` başarılı. Kırmızıysa `gh run view --log-failed` ile oku; kültür ya da saat dilimi kaynaklıysa testi kültür/saatten bağımsız yap (açık `CultureInfo`, `FakeTimeProvider`), yerelde ve CI'da geçtiğini gör, `fix: testler CI koşucusunun kültüründen bağımsız` ile commit edip yükle, bu adımı yinele. Başka nedenli kırmızıda dur ve kullanıcıya bildir.

- [ ] **Step 8: Taze klonda README komutları**

Scratchpad'de boş bir klasöre `gh repo clone virtus-xd/camada` ve içinde README'deki komutları harfiyen çalıştır: `dotnet build CamAda.slnx`, `dotnet test tests/CamAda.Core.Tests`.
Expected: "0 Hata", "Başarısız: 0". Klasörü sonra sil.

- [ ] **Step 9: Yerel dalı temizle ve kaydet**

`git branch -d github-acik-kaynak`. Belleğe (`public-proje-niyeti`) gizli deponun adresi, yedek ve commit eşlemesinin yeri, "herkese açma kullanıcı onayı bekliyor" yazılır. Kullanıcıya gizli deponun adresi ve kalan adımları (Sponsors kaydı, ekran görüntüsü, inceleme) bildirilir.

---

### Task 6: Herkese açma (yalnız kullanıcı açıkça "aç" dedikten sonra)

**Files:** yok (depo ayarları).

- [ ] **Step 1: Onayı al**

Kullanıcıdan bu oturumda açık "herkese aç" onayı yoksa dur ve sor. Onaydan hemen önce `bash .superpowers/github-temizlik/tara.sh` → `ihlal=0`.

- [ ] **Step 2: Herkese aç**

Run: `gh repo edit virtus-xd/camada --visibility public --accept-visibility-change-consequences`
Expected: `gh api repos/virtus-xd/camada --jq .private` → `false`.

- [ ] **Step 3: Özel güvenlik bildirimini aç**

Run: `gh api -X PUT repos/virtus-xd/camada/private-vulnerability-reporting`
Expected: `gh api repos/virtus-xd/camada/private-vulnerability-reporting --jq .enabled` → `true`.

- [ ] **Step 4: `main` kuralı**

`gh api -X POST repos/virtus-xd/camada/rulesets --input <scratchpad>/kural.json`; `kural.json`: `name: main`, `target: branch`, `enforcement: active`, `conditions.ref_name.include: ["refs/heads/main"]`, kurallar `deletion`, `non_fast_forward`, `required_status_checks` (`required_status_checks: [{"context": "build-test"}]`, `strict_required_status_checks_policy: false`), `bypass_actors: [{"actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "always"}]` (yönetici yerelden doğrudan yükleyebilir).
Expected: `gh api repos/virtus-xd/camada/rulesets --jq '.[].name'` → `main`.

- [ ] **Step 5: Topluluk profili**

Run: `gh api repos/virtus-xd/camada/community/profile --jq '.health_percentage, (.files | to_entries | map(select(.value == null) | .key))'`
Expected: `100` ve boş liste; değilse eksik dosyayı kullanıcıya söyle.

- [ ] **Step 6: Kaydet**

Belleğe "herkese açık (tarih)" yazılır; kullanıcıya depo adresi ve Sponsor düğmesinin Sponsors kaydı onaylanınca görüneceği bildirilir.
