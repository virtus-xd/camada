using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CamAda.Island;

/// <summary>Genişlemiş adanın satırı: Başlat, Ara | uygulamalar | ^ | saat (spec §7.2).</summary>
public partial class IslandBar : UserControl
{
    public IslandBar()
    {
        InitializeComponent();
    }

    public event EventHandler? StartClicked;
    public event EventHandler? SearchClicked;
    public event EventHandler? TrayClicked;
    public event EventHandler<AppButtonEventArgs>? AppClicked;
    public event EventHandler<AppButtonEventArgs>? AppMiddleClicked;

    public IEnumerable? AppsSource
    {
        get => Apps.ItemsSource;
        set => Apps.ItemsSource = value;
    }

    public void SetTime(string time) => TimeText.Text = time;

    private void StartButton_Click(object sender, RoutedEventArgs e) => StartClicked?.Invoke(this, EventArgs.Empty);

    private void SearchButton_Click(object sender, RoutedEventArgs e) => SearchClicked?.Invoke(this, EventArgs.Empty);

    private void TrayButton_Click(object sender, RoutedEventArgs e) => TrayClicked?.Invoke(this, EventArgs.Empty);

    private void App_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DockItemViewModel item } button)
        {
            AppClicked?.Invoke(this, new AppButtonEventArgs(item, button));
        }
    }

    private void App_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && sender is Button { Tag: DockItemViewModel item } button)
        {
            AppMiddleClicked?.Invoke(this, new AppButtonEventArgs(item, button));
            e.Handled = true;
        }
    }
}

public sealed class AppButtonEventArgs(DockItemViewModel item, Button button) : EventArgs
{
    public DockItemViewModel Item { get; } = item;

    /// <summary>Tıklanan düğme (başlatma "zıplaması" ve panel konumu için).</summary>
    public Button Button { get; } = button;
}
