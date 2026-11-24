using System.Windows.Interop;
using CamAda.Core.Input;
using CamAda.Interop;

namespace CamAda.Input;

/// <summary>
/// Her yerden çalışan kısayollar (RegisterHotKey): görünmez bir mesaj penceresinde WM_HOTKEY dinlenir. Tuşa basılı
/// tutulunca tekrar etmez (MOD_NOREPEAT). Kısayolu başka bir uygulama almışsa kayıt başarısız olur.
/// </summary>
public sealed class GlobalHotkeys : IDisposable
{
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public GlobalHotkeys()
    {
        _source = new HwndSource(new HwndSourceParameters("CamAda kısayollar") { ParentWindow = HwndMessage, WindowStyle = 0 });
        _source.AddHook(Hook);
    }

    /// <summary>Kısayolu kaydeder; başka bir uygulamaya kayıtlıysa false.</summary>
    public bool Register(Hotkey hotkey, Action action)
    {
        var id = _nextId++;
        if (!Native.RegisterHotKey(_source.Handle, id, (uint)hotkey.Modifiers | Native.MOD_NOREPEAT, (uint)hotkey.VirtualKey))
        {
            return false;
        }

        _actions[id] = action;
        return true;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys)
        {
            Native.UnregisterHotKey(_source.Handle, id);
        }

        _actions.Clear();
        _source.RemoveHook(Hook);
        _source.Dispose();
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }

        return IntPtr.Zero;
    }
}
