namespace CamAda.Core.Bluetooth;

public enum BluetoothDeviceKind
{
    Audio,
    Mouse,
    Keyboard,
    Gamepad,
    Phone,
    Computer,
    Other,
}

/// <summary>Eşleşmiş Bluetooth cihazının türü ve Bluetooth kutucuğunun alt yazısı (Plan 4.3).</summary>
public static class BluetoothKinds
{
    /// <summary>
    /// Klasik cihazın sınıf kodu: ana sınıf <c>(cod &gt;&gt; 8) &amp; 0x1F</c>; çevre biriminde (5) alt sınıfın üst iki biti
    /// 01 klavye, 10 fare, 11 klavye+fare; alt dört biti 0001 ya da 0010 oyun kolu.
    /// </summary>
    public static BluetoothDeviceKind FromClassOfDevice(uint classOfDevice)
    {
        var major = (classOfDevice >> 8) & 0x1F;
        var minor = (classOfDevice >> 2) & 0x3F;
        return major switch
        {
            0x01 => BluetoothDeviceKind.Computer,
            0x02 => BluetoothDeviceKind.Phone,
            0x04 => BluetoothDeviceKind.Audio,
            0x05 => (minor >> 4) switch
            {
                1 or 3 => BluetoothDeviceKind.Keyboard,
                2 => BluetoothDeviceKind.Mouse,
                _ => (minor & 0x0F) is 1 or 2 ? BluetoothDeviceKind.Gamepad : BluetoothDeviceKind.Other,
            },
            _ => BluetoothDeviceKind.Other,
        };
    }

    /// <summary>LE cihazın görünüm değeri: kategori <c>appearance &gt;&gt; 6</c> (0x0F HID, 0x21 ve 0x25 ses).</summary>
    public static BluetoothDeviceKind FromLeAppearance(ushort appearance)
    {
        var category = appearance >> 6;
        var sub = appearance & 0x3F;
        return category switch
        {
            0x01 => BluetoothDeviceKind.Phone,
            0x02 => BluetoothDeviceKind.Computer,
            0x0F => sub switch
            {
                1 => BluetoothDeviceKind.Keyboard,
                2 => BluetoothDeviceKind.Mouse,
                3 or 4 => BluetoothDeviceKind.Gamepad,
                _ => BluetoothDeviceKind.Other,
            },
            0x21 or 0x25 => BluetoothDeviceKind.Audio,
            _ => BluetoothDeviceKind.Other,
        };
    }

    /// <summary>Panelden yalnız ses cihazları bağlanıp ayrılır; diğerleri kendiliğinden bağlanır.</summary>
    public static bool CanConnect(BluetoothDeviceKind kind) => kind == BluetoothDeviceKind.Audio;

    public static string TileSubtitle(bool hasAdapter, bool radioOn, IReadOnlyList<string> connectedNames) =>
        !hasAdapter ? "Yok"
        : !radioOn ? "Kapalı"
        : connectedNames.Count switch
        {
            0 => "Açık",
            1 => $"{connectedNames[0]} bağlı",
            var n => $"{n} cihaz bağlı",
        };
}
