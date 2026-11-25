using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CamAda.Panels;

/// <summary>
/// Satırları baştan kurulan listeler (ses çıkışları, Wi‑Fi, Bluetooth) için: satır öğesine bir anahtar verilir, liste
/// yeniden kurulurken klavye odağı aynı anahtarlı öğeye geri taşınır; fare düğmesi listede basılıyken yeniden kurulum
/// bırakılınca yapılır (yoksa basılan düğme silinir ve tıklama kaybolur).
/// </summary>
public static class RowKeys
{
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.RegisterAttached("Key", typeof(string), typeof(RowKeys));

    public static void SetKey(DependencyObject element, string? key) => element.SetValue(KeyProperty, key);

    public static string? GetKey(DependencyObject element) => (string?)element.GetValue(KeyProperty);

    /// <summary>Listede klavye odağı varsa odaklı öğenin (ya da onu içeren anahtarlı öğenin) anahtarı.</summary>
    public static string? Focused(Panel list)
    {
        if (!list.IsKeyboardFocusWithin)
        {
            return null;
        }

        for (var node = Keyboard.FocusedElement as DependencyObject; node is not null && node != list;
             node = node is Visual ? VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (GetKey(node) is { } key)
            {
                return key;
            }
        }

        return null;
    }

    /// <summary>Yeniden kurulan listede aynı anahtarlı öğeye odağı verir (yerleşimden sonra).</summary>
    public static void Restore(Panel list, string? key)
    {
        if (key is null)
        {
            return;
        }

        list.Dispatcher.BeginInvoke(() =>
        {
            if (Find(list, key) is { } element)
            {
                element.Focus();
            }
        }, DispatcherPriority.Loaded); // yerleşimden sonra (yeni satırlar görünür olsun)
    }

    /// <summary>
    /// Fare düğmesi listenin üstünde basılıysa <paramref name="rebuild"/>'i bırakılınca (tıklama işlendikten sonra) bir kez
    /// çalıştırır ve true döner; basılı değilse false (çağıran hemen kurar).
    /// </summary>
    public static bool DeferWhilePressed(Panel list, Action rebuild)
    {
        if (!list.IsMouseOver || Mouse.LeftButton != MouseButtonState.Pressed)
        {
            return false;
        }

        if (list.GetValue(PendingProperty) is Action)
        {
            list.SetValue(PendingProperty, rebuild); // zaten bekleniyor: en son istenen kurulum geçerli
            return true;
        }

        list.SetValue(PendingProperty, rebuild);
        MouseButtonEventHandler? onUp = null;
        MouseEventHandler? onLeave = null;
        void Run()
        {
            list.PreviewMouseLeftButtonUp -= onUp;
            list.MouseLeave -= onLeave;
            var pending = list.GetValue(PendingProperty) as Action;
            list.ClearValue(PendingProperty);
            if (pending is not null)
            {
                list.Dispatcher.BeginInvoke(pending, DispatcherPriority.Background); // tıklama işlendikten sonra
            }
        }

        onUp = (_, _) => Run();
        onLeave = (_, _) => Run(); // düğme liste dışında bırakılırsa
        list.PreviewMouseLeftButtonUp += onUp;
        list.MouseLeave += onLeave;
        return true;
    }

    private static readonly DependencyProperty PendingProperty =
        DependencyProperty.RegisterAttached("Pending", typeof(object), typeof(RowKeys));

    private static UIElement? Find(DependencyObject root, string key)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (GetKey(child) == key && child is UIElement { Focusable: true, IsVisible: true } element)
            {
                return element;
            }

            if (Find(child, key) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
