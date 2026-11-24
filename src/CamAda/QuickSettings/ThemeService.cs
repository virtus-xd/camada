using System.Runtime.InteropServices;
using System.Windows.Threading;
using CamAda.Core.Theme;
using Microsoft.Win32;

namespace CamAda.QuickSettings;

/// <summary>
/// Koyu tema kutucuğu (Plan 4.3; kullanıcının onayladığı tek belgelenmemiş yol): Windows'un <c>Personalize</c> anahtarındaki
/// uygulama ve Windows modu birlikte yazılır, ardından "ImmersiveColorSet" ayar değişikliği yayınlanır. Ayar başka bir
/// yerden değişirse (Windows Ayarları) <see cref="Changed"/> ile izlenir.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int HWND_BROADCAST = 0xFFFF;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int SMTO_ABORTIFHUNG = 0x0002;

    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    public ThemeService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        IsDark = Read();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public bool IsDark { get; private set; }

    /// <summary>Tema değişince (arayüz iş parçacığında).</summary>
    public event EventHandler? Changed;

    public async Task SetDarkAsync(bool dark)
    {
        var value = ThemeRule.RegistryValue(dark);
        await Task.Run(() =>
        {
            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                key.SetValue("AppsUseLightTheme", value, RegistryValueKind.DWord);
                key.SetValue("SystemUsesLightTheme", value, RegistryValueKind.DWord);
            }

            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 1000, out _);
        });
        Update();
    }

    public void Dispose()
    {
        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => _dispatcher.BeginInvoke(Update);

    private void Update()
    {
        if (_disposed)
        {
            return;
        }

        var dark = Read();
        if (dark != IsDark)
        {
            IsDark = dark;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return ThemeRule.IsDark(key?.GetValue("AppsUseLightTheme") as int?);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or System.IO.IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SendMessageTimeout(nint hWnd, int msg, nint wParam, string lParam, int flags, int timeout,
        out nint result);
}
