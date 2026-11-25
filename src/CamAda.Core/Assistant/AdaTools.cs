using System.Text.Json.Nodes;

namespace CamAda.Core.Assistant;

/// <summary>
/// Ada'nın işlevleri (tasarım "İşlevler (v1)"). Gemini'ye işlev tanımları olarak gider; yürütmeyi uygulamadaki
/// <c>AdaToolRunner</c> yapar. Açıklamalar modelin hangi işlevi seçeceğini belirler: denemede ifadeler bunlarla oturdu.
/// </summary>
public static class AdaTools
{
    public const string SesAyarla = "ses_ayarla";
    public const string SesDegistir = "ses_degistir";
    public const string SessizeAl = "sessize_al";
    public const string CikisCihaziSec = "cikis_cihazi_sec";
    public const string GirisCihaziSec = "giris_cihazi_sec";
    public const string Mikrofon = "mikrofon";
    public const string ParlaklikAyarla = "parlaklik_ayarla";
    public const string ParlaklikDegistir = "parlaklik_degistir";
    public const string Wifi = "wifi";
    public const string Bluetooth = "bluetooth";
    public const string MedyaKontrol = "medya_kontrol";
    public const string SpotifyCal = "spotify_cal";
    public const string UygulamaAc = "uygulama_ac";
    public const string UygulamaKapat = "uygulama_kapat";
    public const string WebSitesiAc = "web_sitesi_ac";
    public const string TarayicidaGoogleAc = "tarayicida_google_ac";
    public const string GuncelBilgi = "guncel_bilgi";
    public const string WhatsAppMesaj = "whatsapp_mesaj";
    public const string EkranGoruntusu = "ekran_goruntusu";
    public const string Kilitle = "kilitle";
    public const string Uyku = "uyku";
    public const string BilgisayariKapat = "bilgisayari_kapat";
    public const string KonusmayiBitir = "konusmayi_bitir";
    public const string Hatirla = "hatirla";
    public const string Unut = "unut";

    public static JsonArray Declarations() =>
    [
        Fn(SesAyarla, "Bilgisayarın ana ses düzeyini verilen yüzdeye ayarlar.", ("yuzde", "INTEGER", "0-100")),
        Fn(SesDegistir, "Ana sesi göreli değiştirir ('biraz kıs', 'sesi aç' gibi). Artı artırır, eksi azaltır; 'biraz' yaklaşık 10.",
            ("fark", "INTEGER", "-100..100")),
        Fn(SessizeAl, "Bilgisayarın sesini kapatır ya da geri açar.", ("sessiz", "BOOLEAN", "true: sessiz")),
        Fn(CikisCihaziSec, "Ses çıkış cihazını değiştirir (hoparlör, kulaklık, monitör, Buds…).", ("ad", "STRING", "cihazın adı ya da türü")),
        Fn(GirisCihaziSec, "Varsayılan mikrofonu (ses giriş cihazını) değiştirir.", ("ad", "STRING", "cihazın adı ya da türü")),
        Fn(Mikrofon, "Mikrofonu kapatır ya da açar. Kapatılınca konuşma da biter.", ("acik", "BOOLEAN", "true: açık")),
        Fn(ParlaklikAyarla, "Ekran parlaklığını verilen yüzdeye ayarlar ('fulle' = 100).", ("yuzde", "INTEGER", "0-100")),
        Fn(ParlaklikDegistir, "Parlaklığı göreli değiştirir. Artı artırır, eksi azaltır; 'biraz' yaklaşık 10.", ("fark", "INTEGER", "-100..100")),
        Fn(Wifi, "Wi-Fi'yi açar ya da kapatır.", ("acik", "BOOLEAN", "true: açık")),
        Fn(Bluetooth, "Bluetooth'u açar ya da kapatır.", ("acik", "BOOLEAN", "true: açık")),
        Fn(MedyaKontrol, "Çalan müziği ya da videoyu yönetir.", ("komut", "STRING", "oynat | duraklat | sonraki | onceki")),
        Fn(SpotifyCal, "Spotify'da bir şarkı, sanatçı ya da çalma listesi çalar.", ("sorgu", "STRING", "ne çalınacak")),
        Fn(UygulamaAc, "Bilgisayardaki bir uygulamayı adıyla açar (Discord, Steam, Dosya Gezgini…).", ("ad", "STRING", "uygulamanın adı")),
        Fn(UygulamaKapat, "Açık bir uygulamanın pencerelerini kapatır (Chrome, Spotify, Discord…). Uygulama kaydetmek isteyebilir.",
            ("ad", "STRING", "uygulamanın adı")),
        Fn(WebSitesiAc, "Bir web sitesini varsayılan tarayıcıda açar (YouTube, Instagram, Netflix…). Tam adresi ver.",
            ("adres", "STRING", "ör. https://www.youtube.com")),
        Fn(TarayicidaGoogleAc,
            "YALNIZ kullanıcı açıkça tarayıcıda Google aramasını açmanı isterse ('Google'da aç', 'tarayıcıda arat'). " +
            "Bilgi sorularında ASLA kullanma; onlar için guncel_bilgi'yi kullan.",
            ("sorgu", "STRING", "aranacak metin")),
        Fn(GuncelBilgi,
            "YALNIZ zamanla değişen bilgiler için çağır: döviz, altın, borsa, hava durumu, maç sonucu, haber, güncel fiyat, son olaylar. " +
            "Tarih, bilim, coğrafya, genel kültür gibi kalıcı bilgiler için ÇAĞIRMA; onları kendi bilginle yanıtla.",
            ("soru", "STRING", "aranacak soru, Türkçe")),
        Fn(WhatsAppMesaj, "WhatsApp'ta rehberdeki bir kişiyle sohbeti mesaj hazır yazılmış olarak açar; göndermek kullanıcıdadır.",
            ("kisi", "STRING", "kişinin adı"), ("mesaj", "STRING", "mesaj metni")),
        Fn(EkranGoruntusu, "Ekran alıntısı aracını açar (Win+Shift+S gibi)."),
        Fn(Kilitle, "Bilgisayarı kilitler."),
        Fn(Uyku, "Bilgisayarı uyku moduna alır."),
        Fn(BilgisayariKapat,
            "Bilgisayarı kapatır ya da yeniden başlatır. İlk çağrı onay ister: kullanıcıya 'emin misin?' diye sor, onaylarsa yeniden çağır.",
            ("yeniden_baslat", "BOOLEAN", "true: yeniden başlat")),
        Fn(KonusmayiBitir, "Kullanıcı vedalaşınca ('tamam teşekkürler', 'görüşürüz') konuşmayı bitirir."),
        Fn(Hatirla,
            "Kullanıcı hakkında önemli bir bilgiyi, tercihi ya da düzeltmeyi kalıcı olarak kaydeder. Aynı konu yeniden kaydedilirse güncellenir.",
            ("konu", "STRING", "kısa anahtar, ör. 'kulaklık', 'şehir'"), ("bilgi", "STRING", "kaydedilecek bilgi, en çok 200 karakter")),
        Fn(Unut, "Kaydedilmiş bir bilgiyi siler.", ("konu", "STRING", "silinecek konunun adı")),
    ];

    private static JsonObject Fn(string name, string description, params (string Name, string Type, string Description)[] parameters)
    {
        var fn = new JsonObject { ["name"] = name, ["description"] = description };
        if (parameters.Length > 0)
        {
            var properties = new JsonObject();
            foreach (var p in parameters)
            {
                properties[p.Name] = new JsonObject { ["type"] = p.Type, ["description"] = p.Description };
            }

            fn["parameters"] = new JsonObject
            {
                ["type"] = "OBJECT",
                ["properties"] = properties,
                ["required"] = new JsonArray(parameters.Select(p => (JsonNode)p.Name).ToArray()),
            };
        }

        return fn;
    }
}
