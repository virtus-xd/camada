# Plan 4.3: Hızlı Ayarlar ve Pil — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bu plan şunları ekler:
- genişlemiş adaya Wi‑Fi ve pil simgeleri;
- ikisinden de açılan Hızlı ayarlar paneli: Wi‑Fi, Bluetooth, Odak ve Koyu tema kutucukları, parlaklık, dört düğme, pil durumu;
- panelin içinde Wi‑Fi ağı ve Bluetooth ses cihazı listeleri;
- küçük adada düşük pil uyarısı;
- adada parlaklık göstergesi.

**Architecture:**
- **Saf kurallar** `CamAda.Core`'da, birim testli: simge ve metin kuralları, ağ listesinin kurulması, Bluetooth cihaz türü, seyrekleştirme, yankı süzgeci.
- **Servisler:** `src/CamAda/QuickSettings/` altında her Windows yolu için ince bir servis var. Servisler olaylarını `Dispatcher`'a taşır ve hepsi `QuickServices` içinde panele verilir.
- **Panel:** `QuickSettingsPanel` bir ana sayfa ile aynı panelin içindeki Wi‑Fi ve Bluetooth liste sayfalarından oluşur.
- **Bağlantı:** App her şeyi `StartQuickSettingsAsync()` içinde bağlar.

**Tech Stack:**
- .NET 10, C#, WPF.
- WinRT projeksiyonu (`net10.0-windows10.0.22621.0`): `Windows.Devices.Radios`, `.WiFi`, `.Bluetooth`, `.Enumeration`, `Windows.System.Power.PowerManager`, `Windows.Networking.Connectivity`, `Windows.UI.Shell.FocusSessionManager`.
- NAudio.Wasapi 2.2.1, ManagedShell 0.0.374.
- **Yeni paket: `System.Management` 10.0.15** (WMI ile parlaklık).
- Testler: xUnit + Microsoft.Extensions.TimeProvider.Testing.

**Spec:** `docs/superpowers/specs/2026-11-24-hizli-ayarlar-design.md` bu parçanın kararlarını içerir; çelişkide bu geçerlidir. Ayrıca ana tasarım (`2026-11-23-cam-ada-design.md`) §7.1, §7.2, §7.4, §7.5.

## Yol haritası (Plan 4, 3/4)

Plan 4.2 main'de (255a43e). Bu plan `plan-4-3-hizli-ayarlar` dalında yürütülür; tasarım commit'i dc3623a. Sonraki parça 4.4: bildirimler.

## Global Constraints

- **Önceki planlardan devralınanlar (Plan 1–4.2):**
  - Windows 11 build 26300;
  - `CamAda.Core` net10.0 ve WPF'siz;
  - Explorer'a enjeksiyon yok;
  - arayüz Türkçe, arayüzde "Wi‑Fi" U+2011 (bölünmez kısa çizgi) ile yazılır;
  - yazı tipi Manrope, simgeler `Segoe Fluent Icons, Segoe MDL2 Assets`;
  - derleme uyarısız;
  - commit sonu `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Yalnız belgelenmiş Windows yolları.** Tek istisna Koyu tema: `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` altındaki `AppsUseLightTheme` ve `SystemUsesLightTheme` birlikte yazılır, ardından `WM_SETTINGCHANGE` ile `"ImmersiveColorSet"` yayınlanır. **Gece ışığı ve Uçak modu yok.**
- **Genişlemiş satır:** `^ | pano | Wi‑Fi | ses | pil | saat`. Wi‑Fi ve pil düğmeleri 36×34 DIP. İkisi de aynı paneli açar (panel anahtarı `"quick"`). Paneli açan bir klavye kısayolu yok.
- **Küçük ada:** Pil yalnız `Percent <= 20` ve `Charge == Discharging` iken görünür (uyarı rengi, metin "%18"). Wi‑Fi küçük adada görünmez.
- **Odak:** 30 dk (`QuickSettings.FocusMinutes`; 5–240 dışındaki değer 30'a döner). Windows'un kendi Odak süresi okunmaz.
- **Wi‑Fi şifresi** hiçbir yere yazılmaz: ayar dosyası, günlük ve `LogCrash` metni dahil. Yeni ağ `WiFiReconnectionKind.Automatic` ile kaydedilir.
- **Olay tabanlı çalışma.** Zamanlayıcı yalnız şu üç yerde kullanılır:
  - Wi‑Fi listesi açıkken 10 sn'lik tarama;
  - panel açıkken Odak'ın kalan süre sayacı;
  - kaydırıcı seyrekleştirmesinin son gönderimi.

  Bluetooth cihaz izleyicisi yalnız panel açıkken çalışır.
- **İş parçacığı kuralı:** WinRT, WMI, DDC ve KS çağrıları UI iş parçacığında beklenmez. Sonuçlar `Dispatcher`'a taşınır; panel kapandıysa arayüze yazılmaz. Bir servis kurulamazsa ilgili kutucuk soluk görünür ve "Yok" yazar, ada çalışmaya devam eder, hata `LogCrash`'e yazılır.
- **Parlaklık göstergesi:** `FlashContent("", level / 100.0, $"%{level}")`. CamAda'nın kendi kaydırıcısından gelen değişiklik gösterge çıkarmaz.
- **Açılan adresler:**

  | Amaç | Adres |
  |---|---|
  | Pil ayarları | `ms-settings:batterysaver` |
  | Wi‑Fi ayarları | `ms-settings:network-wifi` |
  | Konum izni | `ms-settings:privacy-location` |
  | Bluetooth ayarları | `ms-settings:bluetooth` |
  | Ekran alıntısı | `ms-screenclip:` |
  | Görev Yöneticisi | `taskmgr.exe` |
- **Yalnız Debug'da çalışan deneme değişkenleri:**
  - `CAMADA_FAKE_LAUNCHER=<günlük yolu>`: düğmeler gerçek işlem yerine günlüğe satır yazar.
  - `CAMADA_FAKE_BATTERY=<yüzde>,<Discharging|Charging|Idle>,<kalan dk ya da ->`: sahte pil durumu.
  - `CAMADA_QS_ABSENT=radios,focus,brightness,wifi,bluetooth`: listelenen servisler yokmuş gibi davranılır.
- **Canlı deneme kuralları (tasarım "Kısıtlar"):**
  - **Wi‑Fi:** Wi‑Fi'yi kapatan ya da bağlantıyı kesen her deneme, **aynı PowerShell komutunun içinde** Wi‑Fi'yi geri açar. Komut, ev ağına bağlanıldığını ve `Test-Connection 1.1.1.1 -Count 1 -Quiet` sonucunun `True` olduğunu görmeden bitmez (en çok 60 sn). Bağlantı gelmezse `netsh wlan connect name=<ev ağı profili>` çalıştırılır.
  - **Durum değiştiren denemeler:** Bluetooth (şu an kapalı), parlaklık (şu an %80), Koyu tema (şu an 0/0) ve Odak önce kaydedilir, deneme sonunda geri konur.
  - **Kilitle, Uyku, Görev Yöneticisi, Ekran görüntüsü:** yalnız sahte başlatıcıyla denenir.
  - **Yasaklar:** otomatik yükseltilen uygulama açılmaz; ESC tuşu SendKeys ile gönderilmez.
  - **Tıklamalar:** UIA öğesi bulunamazsa tıklanmaz. Adadaki düğmelere UIA Invoke değil gerçek tıklama yapılır. Adayı açmak için imleç (768,0)'a getirilir.

## Review Focus

1. **Servis yok ya da başarısız** (Bluetooth adaptörü devre dışı, WMI parlaklığı yok, Odak başlatılamıyor). Beklenen: ilgili kutucuk soluk ve "Yok" yazar ya da kısa bir hata metni gösterir; panel yine açılır, çökme olmaz. (Görev 3 Step 7)
2. **Süren bir işlem varken panel kapanır** (tarama, bağlanma, DDC çağrısı). Beklenen: sonuç gelince çökme olmaz; yeniden açılan panel doğru durumu gösterir. (Görev 5 Step 4)
3. **Durum dışarıdan değişir** (Fn ya da WMI ile parlaklık, kayıt defterinden tema, Windows'tan telsiz). Beklenen: panel açıkken kutucuk ve kaydırıcı yeni durumu izler; sürüklenen kaydırıcı dışarıdan gelen değerle zıplamaz; panel kapalıyken parlaklık göstergesi çıkar. (Görev 3 Step 7, Görev 4 Step 5)
4. **Garip ağ listeleri:** aynı ağ adı birden çok erişim noktasından görünür, gizli ağ var, liste boş, ağların adı gelmiyor (konum izni yok). Beklenen: tek satır; gizli ağ listede yok; boşsa "Ağ bulunamadı"; adlar gelmiyorsa izin mesajı. (Görev 1 Step 3, Görev 5 Step 4)
5. **Hızlı art arda basma** (kutucuğa çift tıklama, Bağlan'a iki kez basma, kaydırıcıyı hızla sürükleme). Beklenen: işlem bitene kadar kutucuk ya da düğme meşgul görünür ve ikinci basış yok sayılır; parlaklık son değerde kalır. (Görev 1 Step 3, Görev 3 Step 7, Görev 4 Step 5)

---

### Task 1: Riskli iki yolun doğrulanması ve Core kuralları

**Files:**
- Create:
  - `src/CamAda.Core/Network/NetworkIcons.cs`, `src/CamAda.Core/Network/WifiNetworkList.cs`
  - `src/CamAda.Core/Bluetooth/BluetoothKinds.cs`
  - `src/CamAda.Core/Power/BatteryText.cs`
  - `src/CamAda.Core/Display/SendThrottle.cs`, `src/CamAda.Core/Display/EchoFilter.cs`
  - `src/CamAda.Core/Focus/FocusText.cs`
  - `src/CamAda.Core/Theme/ThemeRule.cs`
  - `src/CamAda.Core/Settings/QuickSettingsOptions.cs`
- Modify: `src/CamAda.Core/Settings/AppSettings.cs` (`QuickSettings` özelliği), `src/CamAda.Core/Settings/SettingsStore.cs` (`Sanitize`).
- Test (`tests/CamAda.Core.Tests/` altında):
  - `Network/NetworkIconsTests.cs`, `Network/WifiNetworkListTests.cs`
  - `Bluetooth/BluetoothKindsTests.cs`
  - `Power/BatteryTextTests.cs`
  - `Display/SendThrottleTests.cs`, `Display/EchoFilterTests.cs`
  - `Focus/FocusTextTests.cs`
  - `Theme/ThemeRuleTests.cs`
  - `Settings/SettingsStoreTests.cs`
- Scratchpad (commit edilmez): `$CLAUDE_JOB_DIR/tmp/spike/` (net10.0-windows10.0.22621.0 konsol uygulaması).

**Interfaces:**
- **Ağ:**
  - `enum NetworkLink { None, Ethernet, Wifi }`
  - `enum NetworkReach { None, Local, Internet }`
  - `sealed record NetworkSnapshot(NetworkLink Link, NetworkReach Reach, int Bars, bool HasWifiAdapter, bool WifiRadioOn, string? Ssid)`; `Bars` 1–4 arasıdır.
  - `enum NetworkIconKind { Ethernet, Wifi, WifiDisconnected, WifiOff, NoNetwork }`
  - `sealed record NetworkIcon(NetworkIconKind Kind, int Bars, bool Warning)`
  - `static class NetworkIcons`: `NetworkIcon For(NetworkSnapshot s)`, `string AccessibleName(NetworkSnapshot s)`, `string WifiTileSubtitle(NetworkSnapshot s)`, `int BarsFrom(int raw, int max)`.
- **Wi‑Fi listesi:**
  - `enum WifiSecurity { Open, Password, Enterprise }`
  - `sealed record WifiNetworkSeen(string Ssid, int Bars, WifiSecurity Security)`
  - `sealed record WifiRow(string Ssid, int Bars, WifiSecurity Security, bool Connected, bool Saved)`
  - `static class WifiNetworkList`: `IReadOnlyList<WifiRow> Build(IEnumerable<WifiNetworkSeen> seen, WifiNetworkSeen? connected, IReadOnlySet<string> savedSsids)`, `WifiSecurity SecurityFrom(string authentication, string encryption)`, `bool LooksLocationBlocked(int networkCount, int namedCount)`.
- **Bluetooth:**
  - `enum BluetoothDeviceKind { Audio, Mouse, Keyboard, Gamepad, Phone, Computer, Other }`
  - `static class BluetoothKinds`: `BluetoothDeviceKind FromClassOfDevice(uint classOfDevice)`, `BluetoothDeviceKind FromLeAppearance(ushort appearance)`, `bool CanConnect(BluetoothDeviceKind kind)`, `string TileSubtitle(bool hasAdapter, bool radioOn, IReadOnlyList<string> connectedNames)`.
- **Pil:**
  - `enum BatteryCharge { NotPresent, Discharging, Charging, Idle }`
  - `sealed record BatterySnapshot(int Percent, BatteryCharge Charge, bool PluggedIn, TimeSpan? Remaining)`
  - `sealed record BatteryIcon(int Level, bool Charging, bool Low)`
  - `static class BatteryText`: `const int LowPercent = 20`, `string? Header(BatterySnapshot s)`, `string AccessibleName(BatterySnapshot s)`, `string FormatRemaining(TimeSpan t)`, `bool IsLow(BatterySnapshot s)`, `BatteryIcon Icon(BatterySnapshot s)`, `string CompactText(BatterySnapshot s)`.
- **Parlaklık yardımcıları:**
  - `sealed class SendThrottle(TimeProvider time, TimeSpan interval)`: `int? Offer(int value)`, `int? TakeDue()`, `bool HasPending`, `TimeSpan Interval`.
  - `sealed class EchoFilter(TimeProvider time, TimeSpan window)`: `void Sent(int value)`, `bool IsEcho(int value)`.
- **Odak:** `static class FocusText`: `string Subtitle(bool active, DateTimeOffset? endsAt, DateTimeOffset now)`, `TimeSpan DelayToNextUpdate(DateTimeOffset endsAt, DateTimeOffset now)`.
- **Tema:** `static class ThemeRule`: `bool IsDark(int? appsUseLightTheme)`, `int RegistryValue(bool dark)`.
- **Ayar:** `sealed record QuickSettingsOptions { const int DefaultFocusMinutes = 30; int FocusMinutes { get; init; } = 30; }`; `AppSettings.QuickSettings { get; init; } = new()`.
- **Spike sonucu** (Görev 3 ve 6 kullanır): Odak'ın ve Bluetooth ses komutunun çalışıp çalışmadığı. Sonuç bu dosyanın sonundaki "Spike sonuçları" bölümüne yazılır.

- [ ] **Step 1: Odak'ı canlı doğrula**
  - Spike konsolunda önce `FocusSessionManager.GetDefault()` alınır ve `IsFocusActive` kaydedilir (beklenen `false`).
  - `TryStartFocusSession(DateTimeOffset.Now.AddMinutes(2))` çağrılır. Beklenen: dönen oturum null değil; `IsFocusActive == true`; `IsFocusActiveChanged` tetiklenir; `session.EndTime` yaklaşık 2 dk sonrayı gösterir.
  - `DeactivateFocus()` çağrılır. Beklenen: `IsFocusActive == false`.
  - Sonuç "Spike sonuçları"na yazılır. Oturum başlamazsa (null ya da hata) Odak kutucuğu Windows'un Odak ayarlarını açar. Önce `ms-settings:focus` denenir, açılmazsa `ms-settings:quiethours`; hangisinin açıldığı da yazılır. Bu durum kullanıcıya bildirilir.
- [ ] **Step 2: Bluetooth ses komutunu canlı doğrula**
  - Bluetooth'un özgün durumu kaydedilir (Kapalı) ve Bluetooth açılır.
  - NAudio `MMDeviceEnumerator` ile `ACTIVE | UNPLUGGED` durumundaki çıkış uçlarından "Kulaklıklar (Buds2)" bulunur.
  - Şu zincir izlenir: `IDeviceTopology` → `GetConnector(0)` → `GetConnectedTo` → `IPart` → `GetTopologyObject` → `GetDeviceId` → `enumerator.GetDevice(id)` → `Activate(IKsControl)` → `KsProperty(KSPROPSETID_BtAudio {7FA06C40-B8F6-4C7E-8556-E8C33A12E54D}, KSPROPERTY_ONESHOT_RECONNECT = 0, KSPROPERTY_TYPE_GET)`.
  - Beklenen: zincirdeki her adım başarılı ve `KsProperty` `S_OK` döner. Buds2 açık ve yakındaysa 10 sn içinde bağlanır, `DISCONNECT = 1` ile ayrılır; Buds2 kapalıysa yalnız zincirin kurulduğu doğrulanır.
  - Bluetooth özgün durumuna döndürülür, sonuç "Spike sonuçları"na yazılır. Zincir kurulamazsa Görev 6'da Bağlan/Ayır yerine "Ayarlarda bağlan ›" kullanılır ve bu kullanıcıya bildirilir.
- [ ] **Step 3: Testleri yaz**
  - **`NetworkIconsTests`:**
    - `Ethernet_wins_and_warns_without_internet`: `(Ethernet, Internet)` → `new NetworkIcon(Ethernet, 0, false)`; `Reach = Local` ise `Warning = true`.
    - `Bars_scale_to_1_to_4` (`[Theory]`): (0,5)→1, (1,5)→1, (2,5)→2, (3,5)→3, (4,5)→4, (5,5)→4, (2,4)→2, (4,4)→4. Kural: `clamp(ceil(raw * 4.0 / max), 1, 4)`.
    - `Wifi_without_internet_warns`: `(Wifi, None, 3)` → `NetworkIcon(Wifi, 3, true)`.
    - `No_link_shows_radio_state`: adaptör yok → `NoNetwork`; telsiz kapalı → `WifiOff`; telsiz açık → `WifiDisconnected`.
    - `Accessible_names`:
      - Wi‑Fi "Ev" + Internet → `"Wi‑Fi: Ev, internet var"`;
      - Wi‑Fi "Ev" + Local → `"Wi‑Fi: Ev, internet yok"`;
      - Ethernet + Internet → `"Ethernet, internet var"`;
      - telsiz kapalı → `"Wi‑Fi kapalı"`;
      - telsiz açık, bağlı değil → `"Wi‑Fi: bağlı değil"`;
      - adaptör yok → `"Ağ bağlantısı yok"`.
    - `Wifi_tile_subtitle`: Wi‑Fi bağlıysa ağ adı; Ethernet varken Wi‑Fi açık ama bağlı değilse `"Bağlı değil"`; telsiz kapalıysa `"Kapalı"`; adaptör yoksa `"Yok"`.
  - **`WifiNetworkListTests`:**
    - `Same_ssid_is_one_row_with_strongest_bars`: `("Ev",2)`, `("Ev",4)` → tek satır, `Bars = 4`.
    - `Hidden_networks_are_skipped`: adı `""` ya da `"  "` olan ağ listede yok.
    - `Order_is_connected_then_saved_then_signal`: görülenler A(4, Open), B(2, Password, kayıtlı), C(3, Password); bağlı ağ D(3) görülenler arasında yok. Beklenen sıra D (`Connected`), B (`Saved`), A, C.
    - `Equal_bars_sort_by_name`: `"b"`, `"A"` → `"A"`, `"b"` (tr-TR, büyük/küçük harf yok sayılır).
    - `Security_from_authentication` (`[Theory]`):
      - `Open`: ("Open80211","None"), ("Owe","Ccmp"), ("None","None");
      - `Password`: ("Open80211","Wep"), ("SharedKey80211","Wep"), ("WpaPsk","Tkip"), ("RsnaPsk","Ccmp"), ("Wpa3Sae","Ccmp");
      - `Enterprise`: ("Wpa","Tkip"), ("Rsna","Ccmp"), ("Wpa3Enterprise","Gcmp"), ("Wpa3Enterprise192Bits","Gcmp256"), ("Ihv","None"), ("WpaNone","None"), ("Unknown","None").
    - `Location_blocked_when_networks_have_no_names`: (3,0)→true, (0,0)→false, (3,1)→false.
  - **`BluetoothKindsTests`:**
    - `Class_of_device` (`[Theory]`): 0x240404→Audio, 0x200418→Audio, 0x002580→Mouse, 0x002540→Keyboard, 0x0025C0→Keyboard, 0x002508→Gamepad, 0x5A020C→Phone, 0x3E010C→Computer, 0x000000→Other. Kural: ana sınıf `(cod >> 8) & 0x1F`; çevre biriminde (5) alt sınıfın üst iki biti 01 klavye, 10 fare, 11 klavye; alt dört biti 0001 ya da 0010 oyun kolu.
    - `Le_appearance` (`[Theory]`): 0x03C1→Keyboard, 0x03C2→Mouse, 0x03C3→Gamepad, 0x03C4→Gamepad, 0x0941→Audio, 0x0841→Audio, 0x0040→Phone, 0x0080→Computer, 0x0000→Other. Kategori `appearance >> 6`: 0x0F HID, 0x21 ve 0x25 Audio, 0x01 Phone, 0x02 Computer.
    - `Only_audio_can_connect`.
    - `Tile_subtitle`: adaptör yok → `"Yok"`; kapalı → `"Kapalı"`; `[]` → `"Açık"`; `["Buds2"]` → `"Buds2 bağlı"`; `["Buds2","R1mouse1"]` → `"2 cihaz bağlı"`.
  - **`BatteryTextTests`:**
    - `Header`:
      - (76, Discharging, fiş yok, 3 sa 10 dk) → `"%76 · 3 sa 10 dk"`;
      - aynısı kalan süresiz → `"%76"`;
      - (76, Charging, fiş var) → `"Şarj oluyor · %76"`;
      - (100, Idle, fiş var) → `"Tam dolu"`;
      - (80, Idle, fiş var) → `"Prize takılı · %80"`;
      - NotPresent → `null`;
      - (76, Discharging, 30 sa) → `"%76"`: 24 saat ve üstü bilinmiyor sayılır.
    - `FormatRemaining`: 3 sa 10 dk → `"3 sa 10 dk"`; 3 sa → `"3 sa"`; 45 dk → `"45 dk"`; 59 dk 59 sn → `"59 dk"`; 30 sn → `"1 dk"`.
    - `Accessible_name`: `"Pil %76, 3 sa 10 dk kaldı"`, `"Pil %76, şarj oluyor"`, `"Pil %100, tam dolu"`, `"Pil %80, prize takılı"`, kalan süre yokken `"Pil %76"`.
    - `Low_only_when_discharging_at_or_below_20`: (20, Discharging)→true; (21, Discharging)→false; (15, Charging)→false; (15, Idle, fiş var)→false.
    - `Icon`: (76, Discharging)→(8, false, false); (4, Discharging)→(0, false, true); (15, Discharging)→(2, false, true); (100, Charging)→(10, true, false). Düzey `round(percent / 10, AwayFromZero)`.
    - `CompactText`: 18 → `"%18"`.
  - **`SendThrottleTests`** (`FakeTimeProvider`, 100 ms):
    - `First_value_passes`: `Offer(10) == 10`.
    - `Held_values_latest_wins`: `Offer(10)`; +30 ms `Offer(20)` → null; `Offer(30)` → null; +30 ms `TakeDue()` → null; +40 ms `TakeDue()` → 30; `HasPending == false`.
    - `After_interval_offer_passes`: +150 ms sonra `Offer(20) == 20`.
    - `TakeDue_without_pending_is_null`.
  - **`EchoFilterTests`** (1,5 sn):
    - `Matching_recent_send_is_echo_once`: `Sent(40)`; +0,5 sn `IsEcho(40)` → true, ikinci kez → false.
    - `Different_value_is_not_echo`: `IsEcho(55)` → false.
    - `Old_send_expires`: +2 sn sonra false.
    - `Each_send_matches_once`: `Sent(30)`, `Sent(40)` → ikisi de birer kez true.
  - **`FocusTextTests`:**
    - `Subtitle`: kapalı → `"Kapalı"`; açık ve bitiş yok → `"Açık"`; 17 dk 10 sn kaldı → `"18 dk kaldı"`; 65 dk → `"1 sa 5 dk kaldı"`; 60 dk → `"1 sa kaldı"`; 20 sn → `"1 dk kaldı"`; bitiş geçmişte → `"Açık"`.
    - `DelayToNextUpdate`: 17 dk 10 sn → 10 sn; tam 17 dk → 60 sn.
  - **`ThemeRuleTests`:** `IsDark(0)` true, `IsDark(1)` false, `IsDark(null)` false; `RegistryValue(true) == 0`, `RegistryValue(false) == 1`.
  - **`SettingsStoreTests.Focus_minutes_default_and_range`:** değer yok → 30; 4 → 30; 241 → 30; 45 korunur; `"QuickSettings": null` → 30.
- [ ] **Step 4: Çalıştır.** `dotnet test CamAda.slnx --nologo` → Expected: derleme hatası (`NetworkIcons` yok).
- [ ] **Step 5: Core'u yaz.**
  - `WifiNetworkList.Build`: ad boş ya da yalnız boşluksa atılır. Aynı ad (sıralı karşılaştırma) en güçlü sinyalle tek satır olur. Bağlı ağ görülenlerle birleşir; görülenlerde yoksa yine eklenir. Sıra: bağlı, kayıtlı, diğer; her grupta önce sinyal (azalan), sonra ad (tr-TR, büyük/küçük harf yok sayılır).
  - `EchoFilter`: gönderilen değerleri zaman damgasıyla tutar; eşleşen kayıt tüketilir, süresi geçen kayıtlar atılır.
- [ ] **Step 6: Çalıştır.** → Expected: hepsi PASS; `dotnet test CamAda.slnx --nologo` → `Başarısız: 0`.
- [ ] **Step 7: Commit.** `feat: hızlı ayarlar için Core kuralları` (spike sonuçlarıyla güncellenmiş plan dosyası dahil).

### Task 2: Adadaki Wi‑Fi ve pil simgeleri, küçük adada düşük pil

**Files:**
- Create: `src/CamAda/QuickSettings/RadioService.cs`, `src/CamAda/QuickSettings/NetworkStatusService.cs`, `src/CamAda/QuickSettings/BatteryService.cs`, `src/CamAda/QuickSettings/QuickGlyphs.cs`
- Modify: `src/CamAda.Core/Island/IslandLayout.cs`, `src/CamAda/Island/IslandBar.xaml(.cs)`, `src/CamAda/Island/IslandWindow.xaml(.cs)`, `src/CamAda/App.xaml.cs`
- Test: `tests/CamAda.Core.Tests/Apps/DockOverflowTests.cs` (`IslandLayoutTests`)

**Interfaces:**
- Consumes: `NetworkIcons`, `BatteryText`, `NetworkSnapshot`, `BatterySnapshot`.
- Produces:
  - **`RadioService : IDisposable`:**
    - `static Task<RadioService> CreateAsync(Dispatcher dispatcher)`: `Radio.RequestAccessAsync` çağırır; erişim yoksa `Has*` false olur.
    - `bool HasWifi`, `bool HasBluetooth`, `bool WifiOn`, `bool BluetoothOn`.
    - `Task<bool> SetAsync(RadioKind kind, bool on)`: `Windows.Devices.Radios.RadioKind`; başarısızsa false döner.
    - `event EventHandler? Changed`: `Radio.StateChanged` UI iş parçacığına taşınır.
  - **`NetworkStatusService(Dispatcher dispatcher, RadioService? radios) : IDisposable`:** `NetworkSnapshot Current`, `void Refresh()`, `event EventHandler? Changed`.
  - **`BatteryService(Dispatcher dispatcher) : IDisposable`:** `BatterySnapshot Current`, `bool Present`, `event EventHandler? Changed`.
  - **`IslandBar`:** `event EventHandler? WifiClicked`, `event EventHandler? BatteryClicked`, `void ShowNetwork(NetworkIcon icon, string accessibleName)`, `void ShowBattery(BatteryIcon? icon, string accessibleName)`. `ShowBattery` null alırsa pil düğmesi gizlenir.
  - **`IslandWindow`:** `void ShowLowBattery(string? text)`, `event EventHandler? Expanding`.
  - **`IslandLayout.SystemSectionWidth = 144`.**
  - **`static class QuickGlyphs`:** `(string Glyph, bool Dim, string? Badge) Network(NetworkIcon icon)`, `string Battery(BatteryIcon icon)`, `string Bars(int bars)`.

- [ ] **Step 1: Genişlik testini güncelle.** `Expanded_width_grows_with_app_buttons`: (0,1064), (5,1064), (6,1102), (9,1216), (10,1250), (30,1250). Yoruma "Plan 4.3: Wi‑Fi + pil 72 DIP (1064)" eklenir.
- [ ] **Step 2: Çalıştır.** → Expected: FAIL `Expected: 1064, Actual: 992`.
- [ ] **Step 3: Değeri değiştir.** `SystemSectionWidth = 144` (pano, Wi‑Fi, ses, pil; 36'şar DIP).
- [ ] **Step 4: Çalıştır.** → PASS.
- [ ] **Step 5: Servisleri yaz.**
  - **`NetworkStatusService`:**
    - Önce `NetworkInformation.GetInternetConnectionProfile()` kullanılır. Bu yoksa bağlı ilk WLAN ya da Ethernet profili alınır (`GetNetworkConnectivityLevel() != None`).
    - WLAN profilinde ağ adı `WlanConnectionProfileDetails.GetConnectedSsid()`'den, çubuklar `BarsFrom(GetSignalBars() ?? 0, 5)` ile gelir. `IanaInterfaceType == 6` ise bağlantı Ethernet'tir.
    - Erişim düzeyi: `InternetAccess` → Internet; `LocalAccess` ve `ConstrainedInternetAccess` → Local; diğerleri None.
    - `HasWifiAdapter = radios?.HasWifi ?? false`, `WifiRadioOn = radios?.WifiOn ?? false`.
    - Yeniden hesaplama zamanları: `NetworkStatusChanged` (iş parçacığı havuzundan gelir, Dispatcher'a taşınır), `radios.Changed` ve `Refresh()` çağrısı. `Refresh()`'i App, ada genişlerken çağırır; sinyal gücü böylece yoklama yapmadan tazelenir.
  - **`BatteryService`:**
    - Kaynak `PowerManager`: `BatteryStatus` → `BatteryCharge`; `PowerSupplyStatus != NotPresent` → `PluggedIn`.
    - `RemainingDischargeTime` `TimeSpan.MaxValue` ise ya da sıfır veya altındaysa → null.
    - Dört `*Changed` olayı dinlenir.
    - Debug'da `CAMADA_FAKE_BATTERY` varsa olay dinlenmez, o değer döner.
- [ ] **Step 6: Glifleri yaz.** `QuickGlyphs` şu eşlemeyi kullanır:

  | Durum | Glif |
  |---|---|
  | Ethernet | `E839` |
  | Wi‑Fi 1–4 çubuk | `E872`, `E873`, `E874`, `E701` |
  | Bağlı değil | `E701`, soluk |
  | Wi‑Fi kapalı | `E701` soluk + rozet `E711` |
  | Ağ yok | `E839` soluk + rozet `E711` |
  | Uyarı (internet yok) | rozet `E7BA`, `WarningBrush` |
  | Pil düzey 0–9 | `E850 + düzey` |
  | Pil düzey 10 | `E83F` |
  | Şarjda düzey 0–8 | `E85A + düzey` |
  | Şarjda düzey 9 | `E83E` |
  | Şarjda düzey 10 | `EA93` |

  Her kod kullanılmadan önce `GlyphTypeface.CharacterToGlyphMap` ile her iki yazı tipinde de aranır ve ekran görüntüsüyle bakılır. Yoksa ya da yanlış çiziliyorsa en yakın glif seçilir ve tabloya yazılır.
- [ ] **Step 7: Adadaki düğmeleri ekle.** `IslandBar.xaml`'da:
  - `ClipboardButton` ile `SoundButton` arasına `WifiButton` gelir: 36×34; içinde `WifiGlyph` (15) ve sağ üstte `WifiBadge` (10), `MicOffBadge` ile aynı düzende.
  - `SoundButton` ile `ClockButton` arasına `BatteryButton` gelir: 36×34; içinde `BatteryGlyph` (15). Pil düşükken glif `WarningBrush` olur.
  - İki düğmenin `AutomationProperties.Name` değeri, `Show*` çağrısındaki erişilebilirlik adıdır.
- [ ] **Step 8: Küçük adaya pil uyarısını ekle.** `IslandWindow.xaml`'da `CompactMicOff`'tan sonra `CompactBattery` gelir: yatay `StackPanel`, içinde glif (13) ve metin (12, kalın), ikisi de `WarningBrush`, kenar boşluğu `0,0,10,0`, başta gizli.
  - `ShowLowBattery(text)` uyarıyı gösterip gizler ve `UpdateDateText`'i yeniden çağırır. `ShowMicOff` de aynısını yapar.
  - `Fits()` artık görünen sağ sütun işaretlerinin genişliğini (mikrofon ve pil, kenar boşluklarıyla) kullanılabilir genişlikten düşer. Bu, Plan 4.1'deki ertelenen 3'ün tarih kısmını kapatır.
  - `Expanding` olayı `HoverIntent.ExpandedChanged(true)` ile tetiklenir.
- [ ] **Step 9: App'i bağla.** `StartQuickSettingsAsync()`, `StartClipboard()`'dan sonra `await` ile çağrılır.
  - Sırayla oluşturulur: `_radios = await RadioService.CreateAsync(Dispatcher)` (hata olursa `LogCrash`, değer null), `_network`, `_battery`.
  - `UpdateNetworkButton()`: `Bar.ShowNetwork(NetworkIcons.For(s), NetworkIcons.AccessibleName(s))`.
  - `UpdateBatteryButton()`: `Bar.ShowBattery(Present ? Icon : null, AccessibleName)` ve `_island.ShowLowBattery(IsLow ? CompactText : null)`.
  - `_island.Expanding` → `_network.Refresh()`.
  - `ExitGracefully` üç servisi `Dispose` eder.
- [ ] **Step 10: Derle, test et, kur, canlı dene.**
  - `dotnet build CamAda.slnx --nologo` → `0 Uyarı`.
  - `dotnet test CamAda.slnx --nologo` → `Başarısız: 0`.
  - `powershell -File tools/install.ps1`.
  - Ada açılır (imleç (768,0)). Ekran görüntüsünde dört çubuklu Wi‑Fi ve dolu pil (fiş takılı) görünür.
  - Tek bir PowerShell komutunda (Wi‑Fi kuralı): Wi‑Fi telsizi Radio API ile kapatılır → 3 sn beklenir → UIA'da `WifiButton` adı `"Wi‑Fi kapalı"` → telsiz açılır → internet gelene kadar beklenir → ad `"Wi‑Fi: <ev ağı>, internet var"`.
  - Kurulu CamAda menüdeki "Çıkış"la kapatılır. Debug sürümü `CAMADA_FAKE_BATTERY=18,Discharging,95` ile açılır. Beklenen: küçük adada uyarı renginde "%18", pil düğmesinin adı `"Pil %18, 1 sa 35 dk kaldı"` (ekran görüntüsü alınır). Sonra Debug "Çıkış"la kapatılır, kurulu sürüm yeniden başlatılır.
- [ ] **Step 11: Commit.** `feat: adada Wi‑Fi ve pil simgeleri`

### Task 3: Hızlı ayarlar paneli: pil başlığı, kutucuklar, düğmeler

**Files:**
- Create:
  - `src/CamAda/QuickSettings/FocusService.cs`, `ThemeService.cs`, `QuickLauncher.cs`, `QuickServices.cs`
  - `src/CamAda/Panels/QuickTile.xaml(.cs)`, `src/CamAda/Panels/QuickSettingsPanel.xaml(.cs)`, `src/CamAda/Panels/IPanelEscape.cs`
- Modify: `src/CamAda/Panels/PanelWindow.cs`, `src/CamAda/App.xaml.cs`

**Interfaces:**
- Consumes: `RadioService`, `NetworkStatusService`, `BatteryService`, `NetworkIcons.WifiTileSubtitle`, `BluetoothKinds.TileSubtitle`, `BatteryText.Header`, `FocusText`, `ThemeRule`, `QuickSettingsOptions.FocusMinutes`, spike sonucu (Odak).
- Produces:
  - **`FocusService(Dispatcher dispatcher) : IDisposable`:**
    - `bool Supported`, `bool Active`.
    - `DateTimeOffset? EndsAt`: yalnız CamAda'nın başlattığı oturumda dolu.
    - `bool TryStart(TimeSpan duration)`, `void Stop()`, `event EventHandler? Changed`.
  - **`ThemeService : IDisposable`:**
    - `bool IsDark`, `Task SetDarkAsync(bool dark)`.
    - `event EventHandler? Changed`: `SystemEvents.UserPreferenceChanged` gelince kayıt defteri yeniden okunur; olay yalnız değer değiştiyse tetiklenir.
  - **Başlatıcı:**
    - `interface IQuickLauncher { void Screenshot(); void Lock(); void Sleep(); void TaskManager(); void Open(string uri); }`
    - `sealed class SystemQuickLauncher : IQuickLauncher`.
    - `sealed class RecordingQuickLauncher(string logPath) : IQuickLauncher`: her çağrıda `screenshot`, `lock`, `sleep`, `taskmgr` ya da `open <uri>` satırı ekler.
  - **`sealed class QuickServices`:** `required NetworkStatusService Network`, `required BatteryService Battery`, `required ThemeService Theme`, `required IQuickLauncher Launcher`, `required Func<int> FocusMinutes`, `RadioService? Radios`, `FocusService? Focus`. Sonraki görevler `BrightnessService? Brightness` (Görev 4), `WifiService? Wifi` (Görev 5) ve `BluetoothService? Bluetooth` (Görev 6) özelliklerini ekler; hepsi `init`.
  - **`QuickTile`:**
    - Bağımlılık özellikleri: `Glyph`, `Title`, `Subtitle`, `IsOn`, `IsBusy`, `HasDetails`, `IsAvailable`.
    - Olaylar: `event EventHandler? Toggled`, `event EventHandler? DetailsRequested`.
  - **`QuickSettingsPanel(QuickServices services) : UserControl, IPanelEscape`:** `const double PanelWidth = 380`, `enum QuickPage { Main, Wifi, Bluetooth }`, `void ShowPage(QuickPage page)`.
  - **`interface IPanelEscape { bool HandleEscape(); }`**

- [ ] **Step 1: `QuickTile`'ı yaz.** Kutucuk yaklaşık 170×64 DIP, köşe yarıçapı 12.
  - **Sol (ana) bölüm:** glif (16), başlık (12,5 kalın) ve alt yazı (11,5 ikincil). Kapalıyken `PanelItemBrush`, açıkken `AccentBrush` zemin ve `OnAccentBrush` yazı.
  - **Sağdaki ›** (`E76C`): yalnız `HasDetails` ise görünür.
  - **Durumlar:** `IsAvailable == false` → saydamlık 0,45, tıklanmaz. `IsBusy` → basış yok sayılır.
  - **Erişilebilirlik:** ana bölümün adı `"<Title>: <Subtitle>"`, okun adı `"<Title> listesi"`. Boşluk ya da Enter `Toggled`, → tuşu `DetailsRequested` verir.
- [ ] **Step 2: `QuickSettingsPanel` ana sayfasını yaz** (tasarımdaki çizim).
  - **Başlık:** solda "Hızlı ayarlar" (15 kalın). Sağda pil düğmesi (`PanelButton`, metni `BatteryText.Header`); başlık null ise gizli, tıklanınca `Open("ms-settings:batterysaver")`.
  - **Kutucuklar:** 2×2 `UniformGrid`.
  - **Parlaklık:** satırlar için yer tutucu `BrightnessRows` (Görev 4 doldurur).
  - **Düğmeler:** altta dört `PanelButton` ("Ekran görüntüsü", "Kilitle", "Uyku", "Görev Yöneticisi").
  - **Olaylar:** servislere `Loaded`'da abone olunur, `Unloaded`'da bırakılır (SoundPanel düzeni).
- [ ] **Step 3: Kutucuk davranışını yaz.** Ortak kurallar: `IsBusy` sürerken ikinci basış yok sayılır (Review Focus 5). Bir işlem false dönerse alt yazı 3 sn boyunca `"Değiştirilemedi"` olur.
  - **Wi‑Fi:** `IsOn = WifiOn`; alt yazı `WifiTileSubtitle`. Basınca `IsBusy` olur, `await SetAsync(WiFi, !on)` çağrılır. Bu görevde `HasDetails = false`.
  - **Bluetooth:** Wi‑Fi gibi; alt yazı `TileSubtitle(has, on, [])`.
  - **Odak:**
    - `IsAvailable = Focus?.Supported == true`; `IsOn = Active`; alt yazı `FocusText.Subtitle`.
    - Basınca: açıksa `Stop()`. Kapalıysa `TryStart(TimeSpan.FromMinutes(FocusMinutes()))`; false dönerse alt yazı `"Başlatılamadı"`. Spike oturumun başlamadığını gösterdiyse bunun yerine Odak ayarları açılır.
    - Açık ve `EndsAt` doluyken bir `DispatcherTimer` `DelayToNextUpdate` aralıklarıyla alt yazıyı tazeler; zamanlayıcı yalnız panel yüklüyken çalışır.
  - **Koyu tema:** `IsOn = IsDark`; alt yazı `"Açık"` ya da `"Kapalı"`. Basınca `SetDarkAsync(!IsDark)`.
    - İki değer yazılır, ardından `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 1000)` çağrılır. Bu çağrı `Task.Run` içinde yapılır.
- [ ] **Step 4: Düğmeleri yaz.** Her düğme önce paneli `(Window.GetWindow(this) as PanelWindow)?.Dismiss()` ile kapatır.
  - **Ekran görüntüsü:** 300 ms bekler, sonra `Screenshot()` (`ms-screenclip:`).
  - **Kilitle:** `Native.LockWorkStation()`.
  - **Uyku:** `PowerHelper.Sleep()`.
  - **Görev Yöneticisi:** `taskmgr.exe` (`UseShellExecute`).
- [ ] **Step 5: Esc davranışını değiştir.** `PanelWindow` içeriği saklar. Esc'te içerik `IPanelEscape` ise ve `HandleEscape()` true dönerse panel kapanmaz. `QuickSettingsPanel` liste sayfasındayken `Main`'e döner ve true verir.
- [ ] **Step 6: App'i bağla.**
  - `FocusService` (hata olursa null), `ThemeService` ve başlatıcı oluşturulur: Debug'da `CAMADA_FAKE_LAUNCHER` varsa `RecordingQuickLauncher`, yoksa `SystemQuickLauncher`.
  - `_quick = new QuickServices { … }` kurulur. Debug'da `CAMADA_QS_ABSENT`'te sayılanlar null verilir.
  - `Bar.WifiClicked` ve `Bar.BatteryClicked` → `_island.Panels.Toggle("quick", () => new QuickSettingsPanel(_quick), QuickSettingsPanel.PanelWidth)`.
- [ ] **Step 7: Derle, test et, kur, canlı dene.** Derleme, test ve kurulum Görev 2'deki gibi. Panel Wi‑Fi simgesine **gerçek tıklamayla** açılır; ekran görüntüsü alınır.
  - **Koyu tema:** iki değer kaydedilir. Kutucuğa UIA Invoke → kayıt defterinde 1/1, kutucukta "Kapalı" → tekrar Invoke → 0/0.
  - **Review Focus 3:** panel açıkken PowerShell ile kayıt defterine 1 yazılıp yayın yapılır → kutucuk "Kapalı"ya döner → sonra eski değerler geri konur.
  - **Bluetooth (özgün durumu Kapalı):** Invoke → telsiz Açık → Invoke → Kapalı.
  - **Review Focus 5:** 50 ms arayla iki Invoke → son durum başlangıçtan tam bir kez değişmiş olur → özgün durum geri konur.
  - **Wi‑Fi (tek komut, Wi‑Fi kuralı):** Invoke → 3 sn → telsiz Kapalı ve alt yazı "Kapalı" → Invoke → internet gelene kadar beklenir.
  - **Odak:** Invoke → `IsFocusActive` true, alt yazı "30 dk kaldı" → Invoke → false.
  - **Düğmeler:** kurulu sürüm "Çıkış"la kapatılır. Debug sürümü `CAMADA_FAKE_LAUNCHER=%TEMP%\camada-launcher.log` ile açılır. Dört düğme ve pil başlığı sırayla basılır (her seferinde panel yeniden açılır). Günlükte sırasıyla `screenshot`, `lock`, `sleep`, `taskmgr`, `open ms-settings:batterysaver` olmalı.
  - **Review Focus 1:** Debug sürümü `CAMADA_QS_ABSENT=radios,focus` ile yeniden açılır → Wi‑Fi, Bluetooth ve Odak soluk "Yok", panel açılıyor, çökme yok (ekran görüntüsü).
  - Bitişte Debug kapatılır, kurulu sürüm yeniden başlatılır, günlük silinir.
- [ ] **Step 8: Commit.** `feat: hızlı ayarlar paneli`

### Task 4: Parlaklık kaydırıcısı ve adada parlaklık göstergesi

**Files:**
- Create: `src/CamAda/QuickSettings/BrightnessService.cs`, `src/CamAda/Interop/Native.Monitor.cs`
- Modify: `src/CamAda/CamAda.csproj` (`System.Management` 10.0.15), `src/CamAda/QuickSettings/QuickServices.cs`, `src/CamAda/Panels/QuickSettingsPanel.xaml(.cs)`, `src/CamAda/App.xaml.cs`

**Interfaces:**
- Consumes: `SendThrottle`, `EchoFilter`, `IslandWindow.Flash`.
- Produces:
  - `sealed record BrightnessDisplay(string Id, string Name, int Level, bool IsInternal)`
  - `sealed record BrightnessChange(int Level, bool FromCamAda)`
  - **`BrightnessService(Dispatcher dispatcher, TimeProvider time) : IDisposable`:**
    - `IReadOnlyList<BrightnessDisplay> Displays`.
    - `Task RefreshAsync()`: dahili ekranı WMI'dan, harici ekranları DDC/CI'dan okur.
    - `void Set(string id, int level)`: seyrekleştirilmiş gönderim; dahili ekranda 100 ms, harici ekranda 250 ms; son değer her zaman gider.
    - `event EventHandler<BrightnessChange>? InternalChanged`.
  - `QuickServices.Brightness`.

- [ ] **Step 1: Paketi ve Win32 çağrılarını ekle.** Paket: `System.Management` 10.0.15. `Native.Monitor.cs`:
  - `EnumDisplayMonitors`, `GetMonitorInfo` (`MONITORINFOEX`);
  - `EnumDisplayDevices` (`EDD_GET_DEVICE_INTERFACE_NAME`), monitör adını `WmiMonitorID.InstanceName` ile eşlemek için;
  - `dxva2`: `GetNumberOfPhysicalMonitorsFromHMONITOR`, `GetPhysicalMonitorsFromHMONITOR`, `GetMonitorBrightness`, `SetMonitorBrightness`, `DestroyPhysicalMonitors`.
- [ ] **Step 2: `BrightnessService`'i yaz.**
  - **Dahili ekran:**
    - Okuma: `root\WMI` ad alanında `SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE`. Sonuç boşsa ya da hata olursa dahili satır olmaz.
    - Yazma: `WmiMonitorBrightnessMethods.WmiSetBrightness(Timeout: 1, Brightness: level)`; gönderilen her değer `EchoFilter.Sent`'e verilir.
    - Değişiklik olayı: `ManagementEventWatcher` ile `root\WMI` altında `SELECT * FROM WmiMonitorBrightnessEvent`. Olaydaki `Brightness` değeri için `IsEcho` true ise `FromCamAda = true` olur.
  - **Harici ekran:**
    - Yalnız `GetMonitorBrightness`'ın başarılı olduğu fiziksel monitörler listelenir.
    - Düzey `(cur - min) * 100 / (max - min)` ile normalleştirilir.
    - Bütün DDC çağrıları tek bir sıralı işçide yapılır (`SemaphoreSlim(1)` + `Task.Run`), aynı anda iki çağrı olmaz.
    - Tutamaçlar her yenilemede ve `Dispose`'da bırakılır.
  - **Adlar:** dahili ekran "Ekran"; harici ekranda EDID adı (`WmiMonitorID.UserFriendlyName`), yoksa "Harici ekran".
- [ ] **Step 3: Panele kaydırıcıları ekle.** `BrightnessRows` her ekran için bir satır içerir: glif `E706`, `PanelSlider` (0–100) ve yüzde. Ekran adı yalnız birden fazla ekran varken satırın üstünde görünür; hiç ekran yoksa bölüm gizlenir.
  - `Loaded` → `RefreshAsync`.
  - `ValueChanged` → `Set` (değer kod tarafından yazılırken değil).
  - Panel açıkken `InternalChanged` kaydırıcıyı günceller; ancak kaydırıcı fareyle tutuluyorsa ya da klavye odağı kaydırıcıdaysa güncellemez (SoundPanel düzeni).
- [ ] **Step 4: App'i bağla.** Servis açılışta kurulur; WMI izleyicisi olay tabanlıdır. `InternalChanged` gelir ve `FromCamAda` false ise `_island.Flash(new FlashContent("", level / 100.0, $"%{level}"))` çağrılır. Ada genişken ya da panel açıkken `Flash` zaten bir şey yapmaz.
- [ ] **Step 5: Derle, test et, kur, canlı dene.** Önce özgün değer (80) kaydedilir.
  - **Kaydırıcı:** panel açıkken kaydırıcıya UIA `RangeValue` ile 40 verilir → WMI `CurrentBrightness` 40 olur, adada gösterge çıkmaz.
  - **Review Focus 3:** panel kapalıyken PowerShell `WmiSetBrightness(1, 60)` çağırır → adada "%60" göstergesi (1,5 sn içinde ekran görüntüsü). Panel açıkken dışarıdan 70 verilir → kaydırıcı 70'e gelir, gösterge çıkmaz.
  - **Review Focus 5:** 300 ms içinde 10, 20, …, 90 verilir → 1 sn sonra WMI değeri 90.
  - Bitişte 80 geri konur. Harici monitör bağlı olmadığı için tek satır görünür ve ad yazmaz; harici ekran kullanıcının elle deneme listesindedir.
- [ ] **Step 6: Commit.** `feat: parlaklık kaydırıcısı ve adada parlaklık göstergesi`

### Task 5: Wi‑Fi listesi ve bağlanma

**Files:**
- Create: `src/CamAda/QuickSettings/WifiService.cs`, `src/CamAda/Panels/WifiListView.xaml(.cs)`
- Modify: `src/CamAda/QuickSettings/QuickServices.cs`, `src/CamAda/Panels/QuickSettingsPanel.xaml(.cs)`, `src/CamAda/App.xaml.cs`

**Interfaces:**
- Consumes: `WifiNetworkList`, `NetworkIcons.BarsFrom`, `QuickGlyphs.Bars`, `RadioService`, `IQuickLauncher.Open`, `QuickSettingsPanel.ShowPage`.
- Produces:
  - **`WifiService`:**
    - `static Task<WifiService?> CreateAsync()`: adaptör yoksa null döner.
    - `bool AccessDenied`: `WiFiAdapter.RequestAccessAsync() != Allowed`.
    - `Task<WifiScan> ScanAsync(CancellationToken ct)`.
    - `Task<WifiConnectOutcome> ConnectAsync(string ssid, string? password, CancellationToken ct)`.
    - `void Disconnect()`.
  - `sealed record WifiScan(IReadOnlyList<WifiRow> Rows, bool LocationBlocked)`
  - `enum WifiConnectOutcome { Connected, WrongPassword, NotFound, Failed }`
  - `WifiListView(QuickServices services)`
  - `QuickServices.Wifi`

- [ ] **Step 1: `WifiService`'i yaz.**
  - **Tarama:**
    - `adapter.ScanAsync()` çalıştırılır, sonra `NetworkReport.AvailableNetworks` okunur. Her ağ `new WifiNetworkSeen(Ssid, BarsFrom(SignalBars, 4), SecurityFrom(auth.ToString(), enc.ToString()))` olur.
    - Kayıtlı ağlar: `NetworkInformation.GetConnectionProfiles()` içindeki WLAN profillerinin `ProfileName`'i.
    - Bağlı ağ: `adapter.NetworkAdapter.GetConnectedProfileAsync()`, adı `GetConnectedSsid()`, çubuklar `BarsFrom(GetSignalBars(), 5)`. Güvenlik türü görülen aynı adlı ağdan alınır; o ağ yoksa `Password` sayılır.
    - Her ad için en güçlü `WiFiAvailableNetwork` saklanır.
    - `LocationBlocked = AccessDenied || LooksLocationBlocked(toplam, adlı)`.
  - **Bağlanma:**
    - Saklanan ağ yoksa `NotFound` döner.
    - `ConnectAsync(network, WiFiReconnectionKind.Automatic, password is null ? null : new PasswordCredential { Password = password })` çağrılır.
    - Sonuçlar: `Success` → Connected; `InvalidCredential` → WrongPassword; `NetworkNotAvailable` → NotFound; diğerleri → Failed.
    - Şifre başka hiçbir yerde tutulmaz.
- [ ] **Step 2: `WifiListView`'i yaz** (tasarımdaki çizim).
  - **Başlık:** geri düğmesi (`E72B`, adı "Geri"), "Wi‑Fi" (15 kalın), sağda telsizi açıp kapatan "Açık"/"Kapalı" düğmesi.
  - **Satırlar** (`PanelRowButton`): çubuk glifi, ağ adı, sağda "Bağlı", kilit (`E72E`, şifreli) ya da "Açık ağ". Satıra tıklayınca satır genişler:
    - bağlı ağda **Bağlantıyı kes**;
    - kayıtlı ya da şifresiz ağda **Bağlan**;
    - şifreli yeni ağda `PasswordBox` + göster (`E7B3`) + **Bağlan**; Enter da bağlanır;
    - kurumsal ağda satır `Open("ms-settings:network-wifi")` çağırır.
  - **Bağlanma metinleri:** "Bağlanıyor…", "Bağlandı", "Bağlanamadı", "Şifre yanlış olabilir". Bağlan düğmesi işlem bitene kadar devre dışıdır (Review Focus 5).
  - **Alt satır:** "Diğer ağlar ve Wi‑Fi ayarları ›" → `Open("ms-settings:network-wifi")`.
  - **Özel durumlar:**
    - telsiz kapalıysa "Wi‑Fi kapalı";
    - `LocationBlocked` ise "Ağ adlarını görmek için Windows konum izni gerekiyor" + "Konum ayarlarını aç" (`ms-settings:privacy-location`);
    - satır yoksa "Ağ bulunamadı";
    - ilk taramada "Aranıyor…".
  - **Boyut:** en çok 360 DIP yükseklik, `ScrollViewer` ile kayar.
  - **Tarama zamanlaması:** sayfa görününce bir tarama yapılır, sayfa açık kaldıkça 10 sn'lik `DispatcherTimer` ile tekrarlanır. Sayfadan çıkınca ya da `Unloaded`'da `CancellationTokenSource` iptal edilir; iptalden sonra gelen sonuç yok sayılır (Review Focus 2).
  - **Bağlanma süreci:** bağlanma panel kapanınca iptal edilmez, ama sonucu arayüze yazılmaz.
  - **Şifre:** `PasswordBox.Password` yalnız Bağlan anında okunur. Çağrıdan sonra, satır kapanınca ve `Unloaded`'da kutu `Clear()` ile temizlenir.
- [ ] **Step 3: Sayfa geçişini yaz.** Wi‑Fi kutucuğunda `HasDetails = Wifi is not null`; `DetailsRequested` → `ShowPage(Wifi)`.
  - Geçiş 0,18 sn yatay kayma ve solmadır; `ClientAreaAnimation` kapalıysa anındadır.
  - ‹ düğmesi ve Esc ana sayfaya döner. Ana sayfaya dönünce kutucuklar tazelenir.
- [ ] **Step 4: Derle, test et, kur, canlı dene.**
  - Liste açılır → ev ağı en üstte "Bağlı"; adı boş satır yok (ekran görüntüsü).
  - **Review Focus 2:** liste açılır, 0,5 sn içinde iki kez Esc gönderilir (`PostMessage`) → çökme yok; yeniden açılınca liste dolar.
  - **Kes ve yeniden bağlan (tek komut, Wi‑Fi kuralı):** "Bağlantıyı kes" Invoke → `netsh wlan show interfaces` bağlı değil gösterir → ev ağı satırı Invoke → "Bağlan" Invoke → satırda "Bağlandı" → internet beklenir. Olmazsa `netsh wlan connect`.
  - Şifreyle yeni ağa bağlanmak kullanıcının elle deneme listesindedir.
- [ ] **Step 5: Commit.** `feat: Wi‑Fi listesi ve bağlanma`

### Task 6: Bluetooth listesi ve ses cihazı bağlama

**Files:**
- Create: `src/CamAda/QuickSettings/BluetoothService.cs`, `src/CamAda/QuickSettings/BtAudioControl.cs`, `src/CamAda/Interop/Native.KsAudio.cs`, `src/CamAda/Panels/BluetoothListView.xaml(.cs)`
- Modify: `src/CamAda/QuickSettings/QuickServices.cs`, `src/CamAda/Panels/QuickSettingsPanel.xaml(.cs)`, `src/CamAda/App.xaml.cs`

**Interfaces:**
- Consumes: `BluetoothKinds`, `RadioService`, `IQuickLauncher.Open`, `QuickSettingsPanel.ShowPage`, spike sonucu (Bluetooth ses komutu).
- Produces:
  - `sealed record BluetoothEntry(string Id, string Name, BluetoothDeviceKind Kind, bool Connected, string? ContainerId)`
  - **`BluetoothService(Dispatcher dispatcher) : IDisposable`:**
    - `void Start()`, `void Stop()`.
    - `IReadOnlyList<BluetoothEntry> Devices`: bağlılar üstte, sonra ada göre.
    - `event EventHandler? Changed`.
    - `bool CanSendAudioCommands`: spike sonucuna göre.
    - `Task<bool> ConnectAsync(BluetoothEntry device)`, `Task<bool> DisconnectAsync(BluetoothEntry device)`.
  - **`static class BtAudioControl`:** `bool TrySend(string containerId, bool reconnect)`; en az bir ses ucuna komut `S_OK` ile gittiyse true.
  - `BluetoothListView(QuickServices services)`
  - `QuickServices.Bluetooth`

- [ ] **Step 1: `BluetoothService` ve `BtAudioControl`'ü yaz.**
  - **İzleyiciler:** iki `DeviceInformation.CreateWatcher(aqs, props, DeviceInformationKind.AssociationEndpoint)` kurulur:
    - klasik cihazlar için `aqs = BluetoothDevice.GetDeviceSelectorFromPairingState(true)`;
    - LE cihazlar için `BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)`.
  - **İstenen özellikler:** `System.Devices.Aep.IsConnected`, `System.Devices.Aep.ContainerId`, `System.Devices.Aep.Bluetooth.Cod` (klasik), `System.Devices.Aep.Bluetooth.Le.Appearance` (LE).
  - **Tekilleştirme:** aynı `ContainerId` iki izleyicide de görünürse tek kayıt olur, klasik olan seçilir.
  - **Olaylar:** `Added`, `Updated` ve `Removed` UI iş parçacığına taşınır ve `Changed` tetiklenir.
  - **`BtAudioControl`:**
    - Ses uçları NAudio ile bulunur: `ACTIVE | UNPLUGGED` durumundaki ve `PKEY_Device_ContainerId`'si eşleşen çıkış uçları.
    - Her uçta Görev 1 Step 2'deki zincir izlenir. Arayüzler `Native.KsAudio.cs`'te `ComImport` ile tanımlanır: `IDeviceTopology`, `IConnector`, `IPart`, `IKsControl`.
  - **Bağlanma:** `ConnectAsync` komutu gönderir, ardından `IsConnected` için en çok 10 sn bekler.
- [ ] **Step 2: `BluetoothListView`'i yaz.**
  - **Başlık:** geri, "Bluetooth", telsiz düğmesi.
  - **Satırlar:** tür glifi, ad ve "Bağlı"/"Bağlı değil" durumu. Glifler: Audio `E7F6`, Mouse `E962`, Keyboard `E765`, Gamepad `E7FC`, Phone `E8EA`, Computer `E7F8`, Other `E702`. Glifler Görev 2'deki gibi doğrulanır.
  - **Ses cihazı satırı:**
    - `CanSendAudioCommands` ise **Bağlan**/**Ayır** düğmesi; işlem sürerken meşgul görünür; başarısızsa "Bağlanamadı — cihaz açık ve yakında mı?".
    - Değilse "Ayarlarda bağlan ›" → `Open("ms-settings:bluetooth")`.
  - **Özel durumlar:** telsiz kapalıysa "Bluetooth kapalı"; cihaz yoksa "Eşleşmiş cihaz yok".
  - **Alt satır:** "Yeni cihaz ekle ›" → `Open("ms-settings:bluetooth")`.
- [ ] **Step 3: Kutucuğu ve sayfayı bağla.** Panel `Loaded`'da `Bluetooth.Start()`, `Unloaded`'da `Stop()` çağırır. Kutucuk alt yazısı `TileSubtitle(has, on, Devices.Where(d => d.Connected).Select(d => d.Name).ToList())`. `HasDetails = Bluetooth is not null`; `DetailsRequested` → `ShowPage(Bluetooth)`.
- [ ] **Step 4: Derle, test et, kur, canlı dene.**
  - Bluetooth'un özgün durumu kaydedilir (Kapalı). Kutucukla Bluetooth açılır → listede Buds2 (Audio), R1mouse1 (Mouse) ve Wireless Controller (Gamepad) görünür (ekran görüntüsü).
  - Buds2'de Bağlan'a bir kez basılır. Bağlanırsa Ayır'a basılır. 10 sn içinde bağlanmazsa satırda "Bağlanamadı — cihaz açık ve yakında mı?" görünmeli; bu da hata yolunu doğrular.
  - Bitişte Bluetooth özgün durumuna döndürülür.
- [ ] **Step 5: Commit.** `feat: Bluetooth listesi ve ses cihazı bağlama`

## Spike sonuçları

- **Odak (24.11):** `FocusSessionManager.IsSupported = True`, ama `TryStartFocusSession` ve `DeactivateFocus` `UnauthorizedAccessException 0x80070005` ile reddediliyor: "Feature com.microsoft.windows.focussessionmanager.1 is not available". Bu, Microsoft'un yalnız izin verdiği uygulamalara açtığı bir özellik (Limited Access Feature). `IsFocusActive` hata vermiyor, ama Odak açıkken doğru okunduğu doğrulanamadı. **Sonuç:** Odak kutucuğu yedek plana geçer. Durum göstermez; paneli kapatıp `ms-settings:focus` adresini açar. Bu adres "Odaklanma" sayfasını açıyor ("Odaklanma oturumunu başlat", "Oturum Süresi 30 dk"); `ms-settings:quiethours` da aynı sayfayı açıyor.
- **Bluetooth ses komutu (24.11):**
  - Bluetooth açıldı; "Kulaklıklar (Buds2)" ucu `UNPLUGGED` (8) durumunda, kapsayıcı kimliği `8efabee3-…`.
  - Zincirin her adımı `S_OK` döndü: `GetDevice` → `Activate(IDeviceTopology)` → `GetConnector(0)` → `GetConnectedTo` → `IPart.GetTopologyObject` → `GetDeviceId` (`{2}.\\?\bthenum#{0000110b-…}…\src`) → `GetDevice(filtre)` → `Activate(IKsControl)` → `KsProperty(ONESHOT_RECONNECT)`.
  - Buds2 kapalı ya da kutusunda olduğu için 10 sn içinde bağlanmadı (uç durumu 8 kaldı).
  - Varsayılan ses cihazları değişmedi; Bluetooth özgün durumuna (Kapalı) döndü.
  - **Sonuç:** Bağlan/Ayır düğmesi kullanılır (`CanSendAudioCommands = true`). Gerçek bağlanma kullanıcının elle deneme listesinde.

## Kullanıcının elle denemesi (son)

1. Telefonun erişim noktasına panelden şifreyle bağlan. Önce yanlış şifre dene ("Şifre yanlış olabilir" görünmeli), sonra doğrusuyla bağlan. Ardından ev ağına geri dön.
2. Buds2'yi aç, Bluetooth listesinden Bağlan'a bas. Ses Buds2'ye geçiyor mu? Sonra Ayır'a bas.
3. Fn tuşlarıyla parlaklığı değiştir: adada gösterge çıkıyor mu, Windows'unki de çıkıyor mu?
4. Fişi çek: pil simgesinde şimşek kayboluyor mu, panelde kalan süre görünüyor mu? Pil %20'nin altına düşünce küçük adada uyarı çıkıyor mu?
5. Ekran görüntüsü (alıntı aracı açılıyor, sonra adada önizleme), Kilitle, Uyku ve Görev Yöneticisi düğmeleri.
6. Harici monitör bağlıyken parlaklık satırı (monitör DDC/CI destekliyorsa).
7. Odak: 30 dakikalık oturum başlıyor mu, Saat uygulamasında görünüyor mu, bildirimler susuyor mu?
