namespace CamAda.Island;

/// <summary>Adanın kısa göstergesinin içeriği (spec §7.5).</summary>
/// <param name="Glyph">Solda Segoe Fluent Icons glifi.</param>
/// <param name="Level">0–1 doluluk çubuğu; null ise çubuk yok (ör. "Mikrofon kapalı").</param>
/// <param name="Text">Sağdaki metin, ör. "%45" ya da "Sessiz".</param>
public sealed record FlashContent(string Glyph, double? Level, string Text);
