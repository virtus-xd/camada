# README Tanıtım Videosu — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kullanıcının ekran kaydından, README'nin başına konacak ~35 s'lik, sessiz, İngilizce yazılı, en çok 10 MB'lık bir MP4 üretmek.

**Architecture:** Depo dışında bir HyperFrames projesi. Kaynak kayıt önce FFmpeg becerisiyle hazırlanır (Wi‑Fi adı pikselleştirilir); sonra kök kompozisyon kaydı tek `<video>` olarak sahne sahne kırpılmış zaman aralıklarıyla oynatır, her sahnede kamera (ölçek + öteleme) ilgili panele yaklaşır, üstte adanın cam malzemesinden bir yazı plakası durur, sonda kelime işaretli bir kapanış kartı gelir.

**Tech Stack:** HyperFrames CLI (`npx hyperframes`), GSAP (HyperFrames içinde), ffmpeg-skill betikleri (`probe`, `look`, `redact`, `check`), Manrope (OFL, depoda).

**Spec:** `docs/superpowers/specs/2026-11-27-readme-tanitim-videosu-design.md`

## Global Constraints

- Proje klasörü `$USERPROFILE/Videos/camada-tanitim/`; depo dışında, TEMP dışında. Kaynak `$USERPROFILE/Videos/deneme2.mp4` asla değiştirilmez.
- Çıktı: H.264 MP4, 1920×1080, 30 fps, ses izi yok, en çok 10 MB, ~35 s. İlk karede başlık okunur.
- Wi‑Fi ağ adı hiçbir karede okunamaz; ağ adı hiçbir dosyaya ya da mesaja yazılmaz.
- Yalnız kayıttaki gerçek arayüz; çizilmiş arayüz yok. Logo yok (kullanıcıda henüz yok): kapanışta Manrope ile "Camada" kelime işareti ve `data-var-src` ile değiştirilebilir, varsayılanda gizli bir logo yuvası.
- Yazılar İngilizce, okunacak metin ≥36 px, başlık ~72 px; yazı plakası `#CC14182A` zemin, 1 px `#66FFFFFF` kenar.
- Yasaklar (spec §Görünüm): koyu boşlukta yüzen öğeler, parlama/neon, ortalanmış her şey, hepsinin birden solarak gelmesi, köşeye yapışık logo, karartılmış ekran görüntüsü üstünde başlık.
- Render yalnız kullanıcı önizlemeyi onaylayınca.

## Review Focus

1. **Wi‑Fi adı bir geçişte görünür kalabilir** (pikselleştirme aralığı panelin açılıp kapanma karelerini kaçırırsa): hızlı ayarlar panelinin göründüğü her kare pikselli olmalı → Görev 1 Adım 4.
2. **Yaklaşınca arayüz yazısı bulanık/okunmaz** (kaynak 1.25 Mbps): yaklaşma ≤2× ve kareler gözle bakılmalı → Görev 3 Adım 4.
3. **10 MB'ı aşma**: render sonrası boyut ölçülür, aşarsa bit hızı düşürülür → Görev 4 Adım 2.
4. **İlk kare boş ya da siyah** (GitHub önizlemesi): 0. karede başlık okunur → Görev 4 Adım 3.
5. **Kesimde kaynağın yanlış anı** (menü açılmadan yazı gelmesi gibi): her sahnenin giriş/çıkış karesi gözle doğrulanır → Görev 2.

---

### Task 1: Proje ve kaynağın hazırlanması

**Files:** Create `camada-tanitim/` (HyperFrames iskeleti), `camada-tanitim/assets/kayit.mp4`, `camada-tanitim/assets/fonts/Manrope-*.ttf`.

- [ ] **Step 1:** `npx hyperframes init "$USERPROFILE/Videos/camada-tanitim" --skip-skills --skill=hyperframes`; `npx hyperframes --version`. Expected: iskelet (index.html) oluşur.
- [ ] **Step 2:** Wi‑Fi kutusunun koordinatını ve panelin göründüğü zaman aralığını kare kare bul (`look.py --at` 0:33.5–0:38.5 arası 0.25 s adımla, kırpıp bak). Expected: kutu (x, y, w, h) ve [başlangıç, bitiş] — kutu bütün aralıkta aynı yerde.
- [ ] **Step 3:** `redact.py deneme2.mp4 --x X --y Y --width W --height H --mode pixelate` ile (zaman aralığı destekleniyorsa yalnız o aralık, değilse bütün kayıt — kutu başka zaman boş masaüstü) `assets/kayit.mp4` üret. Manrope TTF'lerini `src/CamAda/Assets/Fonts`'tan `assets/fonts/`'a kopyala.
- [ ] **Step 4 (doğrulama):** `look.py assets/kayit.mp4 --at <aralığın ilk karesi> --at <orta> --at <son karesi>` → üçünde de ağ adı okunamaz; `look.py --at 16` → müzik kartı bozulmamış.

### Task 2: Kesim listesi ve SPEC.md

**Files:** Create `camada-tanitim/SPEC.md`.

- [ ] **Step 1:** Spec tablosundaki her sahne için kaynakta giriş ve çıkış anını 0.25 s hassasiyetle bul (`look.py --at`): menünün açıldığı, tıklandığı, görev çubuğunun kaybolduğu/geldiği, kartın açıldığı, Ada'nın komut ve yanıt yazılarının göründüğü kareler.
- [ ] **Step 2:** Her sahne için yaklaşma kutusunu (1920×1080 kaynakta x, y, ölçek) panelin sınırlarından hesapla; ölçek ≤2.0, arayüz yazısı ≥26 px.
- [ ] **Step 3:** Vurgu turuncusunu hızlı ayarlar "Koyu tema" kutusundan ölç (PIL ile piksel ortalaması).
- [ ] **Step 4:** `SPEC.md` yaz (spec.md şablonu: GOAL, OUTPUT 1920×1080 30 fps, TIME, LOOK sayılarla, MOTION eğrileri ve kare süreleri, SHOTS her sahne bir satır: kurgu kareleri, kaynak aralığı, kamera, yazı, sonraki sahneye geçiş; BANNED; QA kareleri). Kurgu toplamı 33–37 s.

### Task 3: Kompozisyon

**Files:** Modify `camada-tanitim/index.html`; Create `compositions/*.html` gerektiği kadar (sahne başına bir), `assets/` altında kullanılanlar.

- [ ] **Step 1:** Kökte `data-duration` = SPEC toplamı; her sahne kaynak `assets/kayit.mp4`'ün kendi aralığını oynatır (`muted`, HyperFrames'in medya kesim öznitelikleri `references/format.md`'ye göre), kamera kaydı saran öğede GSAP ile ölçek/öteleme (0.4–0.6 s, SPEC eğrisi).
- [ ] **Step 2:** Yazı plakası (cam malzeme) her sahnede sahnenin boş tarafında, ortalanmamış; yazı satır satır gelir, okunurken kıpırdamaz. Ada sahnesinde komut alıntısı ayrı satır.
- [ ] **Step 3:** Kapanış kartı: kaydın son karesi zemin, "Camada" kelime işareti (Manrope ExtraBold) iniş hareketiyle, altında "Free & open source" ve `github.com/virtus-xd/camada`; logo yuvası `data-var-src`, boşken görünmez; son 2 s sabit.
- [ ] **Step 4 (doğrulama):** `npx hyperframes check --json` → hata yok, uyarılar tartılır; `npx hyperframes snapshot --review` ve `references/review.md` → `REVIEW.md`; SPEC QA karelerinde yazı okunur, Wi‑Fi adı görünmez, yasak listesinden bir şey yok.

### Task 4: Önizleme, onay, render

- [ ] **Step 1:** `npx hyperframes preview` aç, kullanıcıya göster; onay gelene kadar render yok.
- [ ] **Step 2:** Onaydan sonra render → `camada-readme.mp4`; `probe.py` → 1920×1080, 30 fps, ses yok, süre SPEC ile ±0.1 s, boyut ≤10 MB (aşarsa bit hızını düşürüp yeniden).
- [ ] **Step 3:** `look.py camada-readme.mp4 --at 0` → başlık okunur; `look.py --tiles 4x3` → akış sırası doğru.
- [ ] **Step 4:** Kullanıcı dosyayı GitHub web düzenleyicisinde README'ye bırakır ya da bağlantıyı verir; bağlantı `README.md` ve `README.tr.md`'de `<!-- Screenshot ... -->` yorumunun yerine konur, commit, push, CI yeşil.
