using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CamAda.Core.Apps;
using CamAda.Panels;
using ManagedShell.Common.Helpers;

namespace CamAda.Island;

/// <summary>
/// Adadaki uygulama düğmelerine tıklama (spec §7.3): başlat, öne getir, küçült ya da pencere panelini aç.
/// Yönetici olarak çalışan pencereler Windows tarafından reddedilebilir; ada çökmeden sessizce dener.
/// </summary>
public sealed class DockActions
{
    private readonly DockViewModel _dock;
    private readonly PanelHost _panels;

    public DockActions(DockViewModel dock, PanelHost panels)
    {
        _dock = dock;
        _panels = panels;
    }

    public void Click(DockItemViewModel item, Button button)
    {
        if (item.Overflow is { } overflow)
        {
            _panels.Toggle("overflow", () => new OverflowPanel(overflow, _dock.IconFor, entry =>
            {
                _panels.Close();
                Apply(entry, button: null);
            }), OverflowPanel.PanelWidth);
            return;
        }

        if (item.Entry is { } entry)
        {
            Apply(entry, button);
        }
    }

    /// <summary>Orta tık: her zaman yeni pencere/örnek başlatır.</summary>
    public void MiddleClick(DockItemViewModel item, Button button)
    {
        if (item.Entry is { } entry && Launch(entry))
        {
            Bounce(button);
        }
    }

    private void Apply(DockEntry entry, Button? button)
    {
        var decision = TaskClickAction.Decide(_dock.Fresh(entry));
        switch (decision.Kind)
        {
            case ClickKind.Launch:
                if (Launch(entry) && button is not null)
                {
                    Bounce(button);
                }

                break;

            case ClickKind.Activate:
                Activate(decision.Window!);
                break;

            case ClickKind.Minimize:
                TryShell(() => _dock.Find(decision.Window!.Handle)?.Minimize());
                break;

            case ClickKind.ShowWindows:
                _panels.Toggle(entry.Key, () => new WindowsPanel(
                    entry,
                    activate: window =>
                    {
                        Activate(window);
                        _panels.Close();
                    },
                    close: window => TryShell(() => _dock.Find(window.Handle)?.Close()),
                    launchNew: () =>
                    {
                        _panels.Close();
                        Launch(entry);
                    }), WindowsPanel.WidthFor(entry.Windows.Count));
                break;
        }
    }

    private void Activate(RunningWindow window) => TryShell(() => _dock.Find(window.Handle)?.BringToFront());

    /// <summary>Sabitlenmişte ayardaki yol ve argümanlar, açık uygulamada penceresinin exe'si. Başarısızsa hiçbir şey olmaz.</summary>
    private bool Launch(DockEntry entry)
    {
        string? path;
        var arguments = string.Empty;
        if (entry.Pinned is { } pinned)
        {
            path = pinned.LaunchPath;
            arguments = pinned.Arguments;
        }
        else
        {
            path = entry.Windows.Select(w => _dock.Find(w.Handle)?.WinFileName).FirstOrDefault(p => !string.IsNullOrEmpty(p));
        }

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return false;
        }

        var started = false;
        TryShell(() => started = ShellHelper.StartProcess(path, arguments));
        return started;
    }

    private static void TryShell(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Pencere işlemi reddedildi: {ex.Message}");
        }
    }

    /// <summary>Başlatılan uygulamanın simgesi 0,5 sn zıplar ("animasyonları azalt" açıksa zıplamaz).</summary>
    private static void Bounce(Button button)
    {
        if (!SystemParameters.ClientAreaAnimation
            || button.Content is not Panel content
            || content.Children.OfType<Image>().FirstOrDefault() is not { } icon)
        {
            return;
        }

        // Şablondaki dönüşüm donmuş olabilir; canlandırmak için yenisi atanır.
        var shift = new TranslateTransform();
        icon.RenderTransform = shift;

        var hop = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(0.5) };
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(-7, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.16)),
            new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5)),
            new BounceEase { Bounces = 1, Bounciness = 3, EasingMode = EasingMode.EaseOut }));
        shift.BeginAnimation(TranslateTransform.YProperty, hop);
    }
}
