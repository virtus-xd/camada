using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CamAda.Core.Calendar;

namespace CamAda.Panels;

/// <summary>
/// Takvim paneli (spec §7.4 "Takvim"): yalnız ay görünümü, Pazartesi başlangıçlı, bugün vurgulu, ay ileri/geri. Etkinlik ve
/// hesap yok. Her açılışta bugünün ayıyla başlar.
/// </summary>
public partial class CalendarPanel : UserControl
{
    public const double PanelWidth = 300;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly DateOnly _today;
    private int _year;
    private int _month;

    public CalendarPanel(DateOnly today)
    {
        InitializeComponent();
        _today = today;
        _year = today.Year;
        _month = today.Month;

        foreach (var header in MonthGrid.WeekdayHeaders(System.Globalization.CultureInfo.CurrentUICulture))
        {
            Headers.Children.Add(new TextBlock
            {
                Text = header,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        ShowMonth();
    }

    private void ShowMonth()
    {
        TitleText.Text = MonthGrid.Title(_year, _month, System.Globalization.CultureInfo.CurrentUICulture);
        Days.Children.Clear();
        foreach (var day in MonthGrid.Build(_year, _month, _today))
        {
            Days.Children.Add(CreateCell(day));
        }
    }

    private FrameworkElement CreateCell(CalendarDay day)
    {
        var text = new TextBlock
        {
            Text = day.Date.Day.ToString(Turkish),
            FontSize = 12.5,
            FontWeight = day.IsToday ? FontWeights.ExtraBold : FontWeights.SemiBold,
            Foreground = (Brush)FindResource(day.IsToday ? "OnAccentBrush" : "TextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var cell = new Grid { Width = 36, Height = 32, Opacity = day.InMonth ? 1 : 0.4 };
        if (day.IsToday)
        {
            cell.Children.Add(new Ellipse { Width = 28, Height = 28, Fill = (Brush)FindResource("AccentBrush") });
        }

        cell.Children.Add(text);
        // Ad metne verilir: Grid'in erişilebilirlik karşılığı yok, ekran okuyucu yalnız metni görür.
        AutomationProperties.SetName(text, day.Date.ToString("d MMMM yyyy, dddd", Turkish) + (day.IsToday ? ", bugün" : ""));
        return cell;
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e) => Move(-1);

    private void NextButton_Click(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int delta)
    {
        (_year, _month) = MonthGrid.Shift(_year, _month, delta);
        ShowMonth();
    }
}
