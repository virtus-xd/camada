using System.Windows;
using CamAda.Core.Island;

namespace CamAda.Panels;

/// <summary>
/// Adanın panelleri: aynı anda tek panel açık olur, açıkken ada büyük kalır. Aynı düğmeye ikinci tıklama paneli kapatır.
/// </summary>
public sealed class PanelHost
{
    private readonly Window _island;
    private readonly HoverIntent _hover;
    private PanelWindow? _current;
    private object? _currentKey;

    public PanelHost(Window island, HoverIntent hover)
    {
        _island = island;
        _hover = hover;
    }

    public bool IsOpen => _current is not null;

    /// <summary><paramref name="key"/> ile açık bir panel varsa kapatır, yoksa yenisini açar.</summary>
    public void Toggle(object key, Func<FrameworkElement> content, double width, bool keepOpenWhilePointerInside = false)
    {
        if (_current is not null && Equals(_currentKey, key))
        {
            Close();
            return;
        }

        Show(key, content(), width, keepOpenWhilePointerInside);
    }

    public void Show(object key, FrameworkElement content, double width, bool keepOpenWhilePointerInside = false)
    {
        var previous = _current;
        var panel = new PanelWindow(content, width) { KeepOpenWhilePointerInside = keepOpenWhilePointerInside };
        _current = panel;
        _currentKey = key;

        // Panel değişirken adayı küçültme: HoverIntent.SetPanelOpen(false) imleç dışarıdaysa hemen küçültür.
        // Bu yüzden yalnız o an geçerli panel kapanınca bildirilir.
        panel.PanelClosed += (_, _) =>
        {
            if (ReferenceEquals(_current, panel))
            {
                _current = null;
                _currentKey = null;
                _hover.SetPanelOpen(false);
            }
        };

        _hover.SetPanelOpen(true);
        panel.ShowUnder(_island);
        previous?.Dismiss();
    }

    public void Close() => _current?.Dismiss();
}
