using System.ComponentModel;
using System.Diagnostics;
using CamAda.Core.Search;
using ManagedShell.Common.Helpers;

namespace CamAda.Panels;

/// <summary>Başlat panelinin ve Ada'nın ortak açıcısı.</summary>
public static class ItemLauncher
{
    /// <summary>
    /// Uygulama/dosya yolları ManagedShell ile; Store uygulamaları, Ayarlar sayfaları ve web adresleri kabukla açılır.
    /// Açılamazsa false.
    /// </summary>
    public static bool Open(SearchItem item)
    {
        try
        {
            var isPath = item.Kind is SearchKind.App or SearchKind.File
                && !item.Target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);
            if (isPath)
            {
                ShellHelper.StartProcess(item.Target);
            }
            else
            {
                Process.Start(new ProcessStartInfo(item.Target) { UseShellExecute = true })?.Dispose();
            }

            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or System.IO.FileNotFoundException)
        {
            Debug.WriteLine($"Açılamadı: {item.Target} ({ex.Message})");
            return false;
        }
    }
}
