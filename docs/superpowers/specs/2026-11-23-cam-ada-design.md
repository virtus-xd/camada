# Cam Ada ve Plak Çalar: Tasarım Dokümanı

Tarih: 23 Kasım 2026
Durum: Kullanıcı tarafından onaylandı (23 Kasım 2026)
Tasarım tuvali: https://claude.ai/artifact/FViXdf6YsigP5sE3Kg2hV5 (geçerli artboard'lar: "Ada v2", "Cam ada: durumlar", "Plak çalar v2"; "Ekler"den yalnızca Pano geçmişi ile Ses ve mikrofon. "Masaüstü", "Dock" ve "Plak çalar: üç yön" eski keşiflerdir, geçersizdir)

## 1. Amaç ve neden

Kullanıcı kendi bilgisayarındaki Windows 11 arayüzünü kendi zevkine göre baştan kurmak istiyor ve bunu hazır araçlarla (Windhawk, Seelen UI vb.) değil kendi uygulamasıyla yapmak istiyor. Kişisel kullanım için; dağıtım, çoklu kullanıcı ya da mağaza hedefi yok.

Ürün iki parça:

1. **Cam ada**: Ana ekranın üst ortasında duran, buzlu cam görünümlü bir kontrol noktası. Windows görev çubuğunun yerini tamamen alır; görev çubuğu kapatılır, alt kenar boş kalır.
2. **Plak çalar**: Ana ekranın sol kenarına, alt köşenin biraz üstüne yarı gömülü, gerçekçi çizilmiş bir plak. Spotify'da müzik çalarken kendi kendine döner; üzerine gelince yanında müzik kartı açılır.

Başarı tanımı: Kullanıcı Windows görev çubuğunu kapatıp günlük işlerini (Chrome, Dosya Gezgini, Steam, Discord, uygulama arama, Başlat, ses, Wi‑Fi, bildirimler, pano) yalnızca ada üzerinden rahatça yapabiliyor ve ortam ona "benim yaptığım, güzel görünen" bir masaüstü hissi veriyor.

## 2. Hedefler

- Görev çubuğunun tüm günlük işlevlerini adaya taşımak: sabitlenmiş uygulamalar, açık pencereler, arka plan (tepsi) simgeleri, Başlat, arama, saat/takvim, ses, ağ, bildirimler.
- Ada normalde küçük ve sakin; fareyle genişleyip kısayolları gösteren, tıklanınca panel açan bir yapı.
- Spotify ile çalışan, gerçek albüm kapağını plak etiketinde gösteren plak çalar.
- Tasarım tuvalindeki görünüme sadık kalmak: koyu tonlu buzlu cam, lamba turuncusu vurgu (#FFC27A), sakin hareket.

## 3. Kapsam dışı (v1)

- Dock / yeni bir alt görev çubuğu (kullanıcı iptal etti; alt kenar boş kalacak).
- "Ekler" artboard'undaki seçilmeyen paneller: Steam oyunları ve oyun modu paneli, Discord sesli kanal, zamanlayıcı, şarkı sözleri, sistem durumu (CPU/GPU) paneli.
- Ayarlar arayüzü. v1'de ayarlar bir JSON dosyasından okunur.
- İkincil monitörlerde ada ya da üst şerit.
- Açık tema. Sadece koyu görünüm.

## 4. Anti-hedefler (çalışsa bile başarısızlık sayılacak durumlar)

- Uygulama çöktüğünde ya da kapandığında kullanıcının görev çubuğu olmadan kalması. Her durumda Windows görev çubuğu geri gelmeli.
- Adanın fareyi Chrome sekmelerine götürürken sürekli yanlışlıkla açılması.
- Tam ekran oyunların üstünde adanın görünmesi ya da oyunda takılma yaratması.
- Adanın ekranı kaplayan pencerelerin başlık/sekme alanını örtmesi.
- Boştayken fark edilir CPU kullanımı (pil, fan, oyun performansı).
- Hap şeklinin köşelerinde dikdörtgen bulanıklık "taşması" gibi ucuz görünen cam efekti.

## 5. Kısıtlar

- İşletim sistemi: Windows 11 Pro, Insider build 26300. Explorer'a kod enjekte eden yöntemler kırılgan, kullanılmayacak.
- Birden fazla monitör var. Ada ve üst şerit yalnızca ana ekranda. Pencere listesi tüm ekranlardaki pencereleri kapsar.
- Spotify Premium var. Spotify Web API'nin Şubat 2026 geliştirici modu kuralları: uygulama sahibinin Premium olması şart, en fazla 5 kullanıcı. Kullanıcı kendi Spotify geliştirici uygulamasını (Client ID) bir kez oluşturacak.
- Spotify'ın geliştirici tasarım kuralları, Web API'den alınan albüm kapaklarının kırpılmasını, döndürülmesini ve canlandırılmasını yasaklıyor. Plak etiketindeki dönen kapak bu yüzden Web API'den değil, Spotify masaüstü uygulamasının Windows'a verdiği medya oturumu küçük resminden alınır. Web API yalnızca beğenme ve sıradaki şarkılar için kullanılır. Uygulama kişisel kullanım içindir, dağıtılmaz.
- Diğer uygulamaların bildirimlerini okumak (Windows bildirim dinleyicisi) paket kimliği ister: uygulama seyrek (sparse) MSIX paketi ve kendinden imzalı sertifika ile kaydedilecek, kullanıcı bir kez izin verecek.
- Kullanıcı kodu okumayacak; kurulum, çalıştırma ve acil durumda görev çubuğunu geri getirme adımları kod bilgisi gerektirmemeli.

## 6. Yaklaşımlar

| Yaklaşım | Artısı | Eksisi |
|---|---|---|
| **C# + WPF + ManagedShell (seçilen)** | Tepsi simgeleri, pencere listesi ve ekran kenarı çubuğu (AppBar) hazır ve RetroBar'da günlük kullanımda kanıtlı. Medya, ses, pano için oturmuş .NET kütüphaneleri. Düşük bellek. | Cam ve animasyonlar XAML'de elle kurulmalı, CSS kadar kolay değil. |
| Tauri 2 (Rust + web UI) | Mockup'lar HTML/CSS, arayüz neredeyse birebir taşınır. | Tepsi barındırma ve pencere listesi Rust'ta sıfırdan yazılmalı; en kritik parça en riskli hâle gelir. WebView2 bellek maliyeti. |
| Karma: C# çekirdek + WebView2 arayüz | ManagedShell + HTML arayüz. | İki katman ve köprü bakımı; saydam WebView2'nin WPF içinde bilinen sınırları. |
| Windhawk modu (elendi) | Görev çubuğunu yerinde şekillendirir. | Explorer'a enjeksiyon, Insider sürümde kırılgan, serbest yüzen ada çizemez. |
| Electron (elendi) | Web arayüzü. | Tauri'den ağır, ek kazancı yok. |

Seçim gerekçesi: Görev çubuğu tamamen kapatıldığı için pencere listesi ve tepsi simgelerinin güvenilirliği her şeyden önemli. Kullanıcı kodu okumayacağı için dil tercihi belirleyici değil. Kullanıcı nasıl yapılacağını bize bıraktı; seçim bizim kararımız.

## 7. Davranış tasarımı (kullanıcının onayladığı)

### 7.1 Ada: küçük hâl

- Ana ekranın üst ortasında, üstten 8 px boşlukla, 400×40 px hap.
- İçerik: dönen mini plak (albüm kapağıyla) + şarkı adı ve sanatçı + çalarken oynayan ekolayzır çubukları; mikrofon kapalıysa kırmızımsı mikrofon işareti; bildirim sayısı; saat.
- Müzik çalmıyorsa: tarih, hava durumu ve saat ("Cam ada: durumlar" artboard'undaki "Boşta").

### 7.2 Ada: fareyle genişleyen hâl

- Fare üzerine gelince ~0,12 sn gecikmeyle yaklaşık 770×46 px'e esneyerek genişler, fare çekilince ~0,35 sn sonra küçülür. Bir panel açıkken büyük kalır.
- Klavye odağı adaya gelince de genişler.
- Soldan sağa: Başlat, Ara | Chrome, Dosya Gezgini, Steam, Discord (sabitlenmiş) + sabitlenmemiş açık uygulamalar | müzik (mini plak + şarkı adı) | arka plan uygulamaları (^), pano, Wi‑Fi, ses (+ mikrofon kapalıysa işaret), bildirimler (sayı), saat.
- Uygulama simgeleri uygulamaların kendi simgeleridir (mockup'taki renkli kareler temsilîdir).
- Açık uygulamanın altında nokta; birden fazla penceresi varsa birden fazla nokta. Dikkat isteyen (yanıp sönen) uygulamada rozet/vurgu.

### 7.3 Uygulama simgesine tıklama

- Açık değilse başlatır (başlarken kısa bir "zıplama").
- Tek penceresi varsa: öndeyse küçültür, değilse öne getirir.
- Birden fazla penceresi varsa: adanın altında pencere önizlemeleri paneli (canlı küçük resimler + başlık); seçilen pencere öne gelir. "Yeni pencere" düğmesi.

### 7.4 Paneller (adanın altında açılan cam paneller)

Hepsi adanın altında, ortalı açılır; dışarı tıklayınca kapanır. Tek seferde bir panel açık.

- **Başlat ve ara** (Başlat ya da Ara): arama kutusu; boşken sabitlenmiş uygulamalar, son açılan dosyalar, Tüm uygulamalar, Kilitle/Uyku/Yeniden başlat/Kapat. Yazınca uygulamalar, ayarlar, dosyalar ve en altta "Web'de ara" sonuçları; Enter ilk sonucu açar, ok tuşlarıyla gezilir.
- **Pencereler**: 7.3'te anlatıldığı gibi.
- **Arka planda çalışanlar** (^): tepsi simgeleri ızgarası. Sol tık uygulamanın kendi davranışı, sağ tık uygulamanın kendi menüsü.
- **Pano**: son kopyalananlar (metin, bağlantı, görüntü, kod). Tıklayınca yeniden panoya alınır ve "Kopyalandı" görünür; sabitleme; "Sabitlenmeyenleri sil". Ctrl+Shift+V her yerden açar. Şifre yöneticilerinin geçmişe kaydedilmemesini işaretlediği kopyalar saklanmaz. Ayrıca son ekran görüntüleri burada bir şeritte görünür (bkz. 11, bizim kararımız).
- **Hızlı ayarlar** (Wi‑Fi simgesi): Wi‑Fi, Bluetooth, Odak, Gece ışığı, Koyu tema, Uçak modu kutucukları; Parlaklık kaydırıcısı; Ekran görüntüsü, Kilitle, Uyku, Görev Yöneticisi düğmeleri.
- **Ses ve mikrofon** (hoparlör simgesi): ana ses; çıkış cihazı seçimi (ör. Kulaklık / Hoparlör / Monitör, sistemdeki gerçek cihazlar); uygulama bazında ses (o an ses çıkaran uygulamalar); mikrofonu kapat/aç (tüm uygulamalar için varsayılan kayıt cihazı). Ctrl+Shift+M her yerden mikrofonu kapatıp açar.
- **Müzik** (müzik bölümü): kapak, şarkı, sanatçı, ilerleme, önceki/oynat-duraklat/sonraki; Premium olduğu için beğen ve sıradaki şarkılar.
- **Bildirimler**: diğer uygulamaların bildirimleri listesi, tek tek kapatma, Tümünü temizle.
- **Takvim** (saat): yalnızca ay görünümü (Pazartesi başlangıçlı, bugün vurgulu), ay ileri/geri. v1'de etkinlik yok ve hesap bağlanmaz (kullanıcı kararı); mockup'taki "Bugün" etkinlik listesi v1'de yer almaz.

### 7.5 Kendiliğinden kısa genişlemeler ("Cam ada: durumlar" artboard'u)

- Bildirim geldi: ada genişleyip bildirimi gösterir, birkaç saniye sonra sayaca döner.
- Ekran görüntüsü alındı (Win+Shift+S ya da Print Screen): küçük önizleme, Kopyala / Düzenle.
- Ses ya da parlaklık değişti: ada içinde yatay gösterge (Windows'un kendi ses penceresinin yerine).
- Dosya rafı fikri v1 kapsamı dışında.

### 7.6 Akıllı gizlenme ve tam ekran (kullanıcı kararı, 23.11.2026)

Önceki "56 px üst şerit" kararı kullanıcı canlı görünce değiştirildi: üstte alan **ayrılmaz**, pencereler ekranın en üstünden başlar.

- Adanın yerini (ana ekranın üst ortası) bir pencere kaplıyorsa (ekranı kaplayan bir pencere ya da başlık çubuğu oraya denk gelen herhangi bir normal pencere), ada ince bir çizgiye iner: 96×5 DIP, üstten 3 DIP, ortalı.
- İmleç ekranın en üst kenarına, ada penceresinin genişliği içinde gelince (0,12 sn gecikmeyle) ada geniş hâliyle açılır. İmleç adadan ve üst kenardan ayrılınca (0,35 sn) yeniden çizgiye iner.
- Çizgi tıklamaları engellemez; en üst kenardaki tıklamalar alttaki pencereye (ör. Chrome sekmeleri) gider.
- Ada bir pencerenin üstünde açıldığında arkasında duvar kağıdı olmadığı için ton koyulaşır (düz cam).
- Tam ekran uygulama ya da oyun açıkken ada, çizgi dahil, tamamen gizlenir ve kısa genişlemeler bastırılır.

### 7.7 Görev çubuğunu kapatma ve güvenlik ağı

- Uygulama açılınca Windows görev çubuğu (tüm monitörlerde) gizlenir; uygulama normal kapanınca geri gelir.
- Uygulama çökerse ya da öldürülürse görev çubuğu otomatik geri gelmeli (ayrı bir bekçi süreci ya da eşdeğeri).
- Kod bilgisi gerektirmeyen bir acil kurtarma yolu olmalı (ör. masaüstünde "Görev çubuğunu geri getir" kısayolu).
- Windows tuşu Windows'un kendi Başlat menüsünü açmaya devam eder (kullanıcı itiraz etmedi). Görev çubuğu gizliyken bu menünün nerede açıldığı ilk denemede kontrol edilecek.

### 7.8 Plak çalar ("Plak çalar v2" artboard'u)

Kullanıcının tarifi: sol alt tarafta, köşenin biraz üstüne gömülü, epey gerçekçi bir plak; müzik çalarken kendi hâlinde döner; üzerine gelince müzik kartı açılır, oradan şarkı değiştirilir; imleç çekilince kart kapanır, plak aynı yerde dönmeye devam eder.

- **Konum**: Ana ekranın sol kenarı. Plağın yaklaşık %45'i ekranın dışında kalır, alt kenarı ekranın altından ~70 px yukarıda. Varsayılan çap ~320 px. Üçü de ayarlardan değiştirilebilir.
- **Pikap kolu yok** (plağın yarısı ekran dışında olduğu için).
- **Tıklama**: Plağa tıklamak müziği duraklatır ya da başlatır (kart açık olsun olmasın). Klavyede odaklıyken Enter/Boşluk aynı işi yapar.
- **Ortadaki kapak**: Plağın etiketi çalan şarkının albüm kapağıdır, daire şeklinde kırpılır ve plakla birlikte döner. Üzerine yazı eklenmez. Varsayılan etiket çapı plak çapının ~1/3'ü (320 px plakta ~108 px), ayarlardan büyütülebilir. Kapak, şarkı bilgisiyle birlikte Windows medya oturumundan alınır, Spotify Web API'den değil (bkz. 5. bölüm, Spotify kapak kuralları). Şarkı değişince yeni kapak yumuşak bir geçişle gelir.
- **Pencere katmanı**: Plak pencerelerin arkasında, masaüstü katmanında durur; yalnızca masaüstü görünürken görünür. Pencereler açıkken müzik adadan yönetilir.
- **Dönüş**: Müzik çalarken gerçek plak hızında, 33⅓ devir/dakika (turu 1,8 sn). Duraklatınca birden durmaz, yavaşlayarak durur; tekrar başlayınca hızlanarak döner. Hiçbir şey çalmıyorken durgun.
- **Gerçekçilik**: Mockup'taki katmanlar gerçek uygulamada da korunur:
  - dönen disk: ince oluk dokusu, şarkılar arası daha düz bantlar, giriş ve çıkış bölgeleri, hafif kabarık kenar, çok hafif toz/gren;
  - dönen etiket: gerçek albüm kapağı daire şeklinde, kenarında parlak ince halka, ortada mil deliği;
  - **sabit** ışık yansıması: plak dönerken yerinden kıpırdamayan, oluklara özgü kama şeklindeki parlama ve hafif gökkuşağı yanardönerliği;
  - etiket üzerinde sabit yumuşak parlama;
  - masaüstüne düşen gölge ve kapağın baskın renginden alınan hafif hâle.
- **Müzik kartı**: Plağın üzerine gelince ~0,1 sn içinde plağın sağında, dikey olarak ortalı açılır; fare plaktan ve karttan çıkınca ~0,3 sn sonra kapanır. Plaktan karta geçerken kapanmaz. İçerik: şarkı adı, sanatçı ve albüm, beğen (Premium), ilerleme ve süreler, önceki / oynat-duraklat / sonraki, "Sıradaki" şarkı (Premium).
- **Klavye**: Plak odaklanabilir; odaklanınca kart açılır.
- **Boş durum**: Spotify kapalıyken ya da hiçbir şey çalmıyorken plak durgun; kartta "Şu an bir şey çalmıyor" ve Spotify'ı açma düğmesi.
- Ada ile aynı durumu paylaşır: birinde duraklatınca diğeri de durur.

### 7.9 Görsel dil

- Cam: koyu ton (yaklaşık rgba(20,24,42,0.46) ada, 0.66 paneller), ince açık kenar, üst kenarda hafif parıltı, arkadaki masaüstünün gerçek bulanıklığı.
- Vurgu: #FFC27A (lamba turuncusu), koyu metin #2A1A08 ile. Diğer renkler: #E0607E (kapak gülü), #5E4C86, metin #F4F6FB.
- Yazı tipi: Manrope (OFL lisanslı, uygulamayla birlikte gelir).
- Hareket: sadece bir şey değiştiğinde (açılma, genişleme, plak dönüşü). Windows'un "animasyonları azalt" ayarı açıksa animasyonlar kapanır.
- Arayüz dili Türkçe, tarih/saat Türkçe biçimde.

## 8. Teknik yön (kararlaştırılan)

- C# ve WPF, .NET'in ManagedShell'in desteklediği en güncel LTS sürümü.
- ManagedShell: pencere listesi (tasks), tepsi simgeleri (tray), AppBar.
- Medya: Windows sistem medya oturumu (Windows.Media.Control). Spotify Web API (PKCE ile giriş) yalnızca beğen ve sıradaki şarkılar için.
- Ses: Windows Core Audio (uygulama oturumları, varsayılan cihaz, mikrofon sessize alma).
- Bildirimler: Windows bildirim dinleyicisi, seyrek MSIX paket kimliği ile.
- Pano: Windows pano dinleyicisi.
- Ekran görüntüleri: Resimler\Ekran Görüntüleri klasörünü izleme + panoya gelen görüntüleri yakalama.
- Ayarlar: JSON dosyası (sabitlenmiş uygulamalar, kısayol tuşları, hava durumu konumu, Spotify Client ID).
- Oturum açılışında otomatik başlama.

## 9. Riskler ve ilk deneme (spike)

1. **Hap şeklinde gerçek cam bulanıklığı**: Windows'un Acrylic/Mica efektleri dikdörtgen pencere için tasarlanmış. Kenarları yumuşak, köşelerde taşmayan bulanıklık ve genişleme animasyonu sırasında doğru davranış ilk denemede kanıtlanmalı. Olmazsa yedek: daha koyu, bulanıksız yarı saydam cam.
2. **Görev çubuğu gizliyken Windows tuşu ve Başlat menüsü**: Menünün nerede açıldığı, bildirim baloncuklarının nerede çıktığı.
   - Plan 1 / Görev 4 ölçümü (23.11.2026): Görev çubuğu ManagedShell ile gizliyken Win tuşu gönderildikten 2 sn sonra görünür (cloak'suz) bir Başlat penceresi tespit edilemedi. Başlat'ın hiç açılmıyor olması mümkün; kullanıcı Görev 7 kontrol noktasında gözüyle doğrulayacak. Plan 2'deki kendi Başlat panelimiz bu yüzden önem kazanıyor.
   - Explorer yeniden başlayınca ManagedShell AppBar kaydını yenilemiyordu; `StripAppBar` "TaskbarCreated" mesajında kaydı yeniliyor (ölçümle doğrulandı).
3. **Tam ekran algılama**: oyunlarda adanın gizlenmesi.
4. **Spotify API**: Şubat 2026 değişikliklerinden sonra "beğen" ve "sıra" uç noktalarının hâlâ çalıştığı doğrulanmalı.
5. **Bildirim dinleyicisi**: seyrek paket ile izin akışının Insider sürümde çalıştığı.
6. **Plağın masaüstü katmanı** (Plan 3 / Görev 5 ölçümü, 24.11.2026): Pencere `WM_WINDOWPOSCHANGING`'de hep `HWND_BOTTOM`'a zorlanır; Windows masaüstünü (Progman) en altta tuttuğu için plak onun hemen üstünde kalır, öne alınmaya zorlansa bile pencerelerin altında kalır. Yalnız bu, Win+D'de yetmedi (masaüstü öne gelince plak kayboldu); pencerenin sahibi masaüstü yapılınca (`GWLP_HWNDPARENT = GetShellWindow()`) Win+D'de plak görünür ve tıklanabilir kaldı. Araç penceresi (`WS_EX_TOOLWINDOW`): Alt+Tab'da yok.

**Cam görünümü kararı (Plan 1 / Görev 7, 23.11.2026):** Varsayılan **duvar kağıdı camı**. Gerekçe: bulanık ve doygun duvar kağıdı kesiti, kusursuz yumuşak kenarlar, genişleme sırasında kayma yok, boştayken işlemci ~%0. Akrilik elendi: WPF'in katmanlı penceresinde Windows bulanıklığı bölgeye kırpılmıyor, bütün pencereyi gri bir dikdörtgen olarak kaplıyor. Duvar kağıdı yoksa (düz renk masaüstü) ada kendiliğinden düz cama geçer. Paneller ve plak kartı da aynı yöntemi kullanır; bir panel pencerelerin üstüne açıldığında arkasında duvar kağıdı olmayabileceği için panellerde ton daha koyu (düz cam) tutulur.

İlk iş, 1 ve 2'yi kanıtlayan küçük bir deneme: ana ekranda AppBar ile ayrılmış üst şerit, içinde hap şeklinde cam ada, fareyle genişleyip küçülme ve görev çubuğunu gizleyip geri getirme.

## 10. Oluşturucuya bırakılanlar

- Proje yapısı, katmanlar, kütüphane seçimleri (8. bölümün içinde kalarak).
- Arama dizini (Başlat menüsü kısayolları, ayar sayfaları listesi, son dosyalar) ve sıralama mantığı.
- Hava durumu kaynağı (anahtarsız bir servis tercih edilir) ve konumun ayarlardan alınması.
- Bekçi süreci ve acil kurtarma yolunun tam biçimi.
- Pencere önizlemelerinin canlı mı anlık görüntü mü olacağı (canlı tercih edilir).
- Plağın gerçekçi çizimi için teknik: önceden üretilmiş yüksek çözünürlüklü disk dokusu mu, çalışma anında çizim mi; sabit ışık yansıması için yarı saydam katman mı, gölgelendirici (shader) efekti mi. Sonuç "Plak çalar v2" mockup'ından daha az gerçekçi olmamalı.
- Çok sayıda açık uygulama olduğunda genişlemiş adanın taşma davranışı (ör. belli bir genişlikten sonra "+3" düğmesi).
- Uygulamaların görev çubuğu rozetlerinin (ör. Discord okunmamış sayısı) ManagedShell üzerinden gösterilmesi.
- "Tüm uygulamalar" listesinin görünümü (Başlat panelinin içinde kayan bir liste yeterli).
- Kapat ve Yeniden başlat için tek adımlık onay.
- Performans hedefleri: boştayken ~%0 işlemci; makul bellek.
- Testler: saf mantık (arama, ayar okuma, pano geçmişi, takvim ızgarası) için birim testleri; kabuk entegrasyonu için elle kontrol listesi.

## 11. Kendi verdiğimiz kararlar (kullanıcının gözden geçirmesi için)

- Teknoloji: C# + WPF + ManagedShell.
- Plak çalarda pikap kolunun kaldırılması, 33⅓ devir, yavaşlayarak durma, kartın plağın sağında açılması, varsayılan ölçüler (320 px, %45 gömülü, alttan 70 px, kapak ~108 px).
- Plağın pencerelerin arkasında durması (kullanıcı bu soruyu cevapsız bıraktı; önerimiz uygulandı).
- Dönen kapağın Spotify Web API yerine Windows medya oturumundan alınması.
- Ekran görüntüleri galerisi: Ada v2'de ayrı bir sekmesi kalmadığı için son ekran görüntüleri Pano panelinde bir şerit olarak gösterilecek; yeni ekran görüntüsünde kısa önizleme açılacak.
- Kısayollar: Ctrl+Shift+M (mikrofon), Ctrl+Shift+V (pano). Win+V'ye dokunulmuyor.
- Açılma/kapanma gecikmeleri: 0,12 sn / 0,35 sn.
- ~~Üst şerit ~56 px~~ → kullanıcı "akıllı gizlenme"yi seçti (§7.6). Bir pencerenin adanın yerini kaplayıp kaplamadığı, z-sırasında o noktayı içeren ilk normal pencereye bakılarak 250 ms'de bir kontrol edilir; araç pencereleri, tıklama geçiren katmanlar, başlıksız ve gizli (cloaked) pencereler sayılmaz.
- Ayarlar v1'de JSON dosyasından.

## 12. Açık sorular (kullanıcıya)

Yok.
