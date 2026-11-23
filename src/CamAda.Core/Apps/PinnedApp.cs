namespace CamAda.Core.Apps;

/// <summary>Adada sabitlenmiş bir uygulama.</summary>
/// <param name="ProcessName">Açık pencereleri bu uygulamaya bağlamak için küçük harfli exe adı (ör. "chrome.exe").</param>
public sealed record PinnedApp(string Id, string Name, string LaunchPath, string Arguments, string ProcessName);
