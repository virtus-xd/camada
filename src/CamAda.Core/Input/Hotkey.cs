namespace CamAda.Core.Input;

/// <summary>Kısayol değiştiricileri; değerler RegisterHotKey'in MOD_* değerleriyle aynı.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// Genel kısayol (spec §8: kısayol tuşları ayarlardan). Metin biçimi "Ctrl+Shift+M": en az bir değiştirici ve tek bir tuş
/// (A–Z, 0–9, F1–F24); büyük/küçük harf ve boşluklar önemsiz.
/// </summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>Yazım sırası Windows'unki gibi: Win, Ctrl, Alt, Shift (ör. "Win+Alt+V", "Ctrl+Shift+M").</summary>
    private static readonly (string Name, HotkeyModifiers Flag)[] ModifierNames =
    [
        ("Win", HotkeyModifiers.Win),
        ("Ctrl", HotkeyModifiers.Control),
        ("Alt", HotkeyModifiers.Alt),
        ("Shift", HotkeyModifiers.Shift),
    ];

    public static Hotkey? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var modifiers = HotkeyModifiers.None;
        int? key = null;
        foreach (var raw in text.Split('+'))
        {
            var token = raw.Trim();
            var modifier = ModifierFor(token);
            if (modifier != HotkeyModifiers.None)
            {
                if (key is not null || modifiers.HasFlag(modifier))
                {
                    return null; // tuştan sonra değiştirici ya da aynı değiştirici iki kez
                }

                modifiers |= modifier;
                continue;
            }

            if (key is not null || KeyFor(token) is not { } vk)
            {
                return null; // ikinci tuş ya da tanınmayan ad
            }

            key = vk;
        }

        return modifiers == HotkeyModifiers.None || key is null ? null : new Hotkey(modifiers, key.Value);
    }

    public override string ToString()
    {
        var parts = ModifierNames.Where(m => Modifiers.HasFlag(m.Flag)).Select(m => m.Name).ToList();
        parts.Add(KeyName(VirtualKey));
        return string.Join("+", parts);
    }

    private static HotkeyModifiers ModifierFor(string token) => token.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => HotkeyModifiers.Control,
        "ALT" => HotkeyModifiers.Alt,
        "SHIFT" => HotkeyModifiers.Shift,
        "WIN" => HotkeyModifiers.Win,
        _ => HotkeyModifiers.None,
    };

    private static int? KeyFor(string token)
    {
        var upper = token.ToUpperInvariant();
        if (upper.Length == 1 && upper[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return upper[0]; // VK_A..VK_Z ve VK_0..VK_9 ASCII kodlarıyla aynı
        }

        if (upper.Length is 2 or 3 && upper[0] == 'F' && int.TryParse(upper[1..], out var n) && n is >= 1 and <= 24)
        {
            return 0x70 + n - 1; // VK_F1 = 0x70
        }

        return null;
    }

    private static string KeyName(int virtualKey) =>
        virtualKey is >= 0x70 and <= 0x87 ? $"F{virtualKey - 0x70 + 1}" : ((char)virtualKey).ToString();
}
