namespace CamAda.Core.Glass;

/// <summary>WPF'e bağımlı olmayan basit boyut.</summary>
public readonly record struct SizeD(double Width, double Height);

/// <summary>WPF'e bağımlı olmayan basit dikdörtgen.</summary>
public readonly record struct RectD(double X, double Y, double Width, double Height);
