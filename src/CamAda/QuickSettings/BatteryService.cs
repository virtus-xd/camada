using System.Windows.Threading;
using CamAda.Core.Power;
using Windows.System.Power;

namespace CamAda.QuickSettings;

/// <summary>
/// Pil durumu (belgelenmiş <c>PowerManager</c>, Plan 4.3): yüzde, şarj durumu, fiş ve kalan süre; dört değişiklik olayıyla
/// güncellenir. Debug derlemesinde <c>CAMADA_FAKE_BATTERY=18,Discharging,95</c> sahte bir durum verir (deneme için).
/// </summary>
public sealed class BatteryService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly bool _listening;
    private bool _disposed;

    public BatteryService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
#if DEBUG
        if (FakeFromEnvironment() is { } fake)
        {
            Current = fake;
            return;
        }
#endif
        Current = Read();
        try
        {
            PowerManager.BatteryStatusChanged += OnChanged;
            PowerManager.PowerSupplyStatusChanged += OnChanged;
            PowerManager.RemainingChargePercentChanged += OnChanged;
            PowerManager.RemainingDischargeTimeChanged += OnChanged;
            _listening = true;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            // Pil olayları yoksa ilk okuma gösterilir.
        }
    }

    public BatterySnapshot Current { get; private set; }

    public bool Present => Current.Charge != BatteryCharge.NotPresent;

    /// <summary>Pil durumu değişince (arayüz iş parçacığında).</summary>
    public event EventHandler? Changed;

    public void Dispose()
    {
        _disposed = true;
        if (_listening)
        {
            PowerManager.BatteryStatusChanged -= OnChanged;
            PowerManager.PowerSupplyStatusChanged -= OnChanged;
            PowerManager.RemainingChargePercentChanged -= OnChanged;
            PowerManager.RemainingDischargeTimeChanged -= OnChanged;
        }
    }

    private void OnChanged(object? sender, object e) => _dispatcher.BeginInvoke(() =>
    {
        if (_disposed)
        {
            return;
        }

        var next = Read();
        if (next != Current)
        {
            Current = next;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    });

    private static BatterySnapshot Read()
    {
        try
        {
            return ReadUnsafe();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException
                                       or UnauthorizedAccessException)
        {
            return new BatterySnapshot(0, BatteryCharge.NotPresent, false, null); // pil simgesi gizlenir
        }
    }

    private static BatterySnapshot ReadUnsafe()
    {
        var charge = PowerManager.BatteryStatus switch
        {
            BatteryStatus.NotPresent => BatteryCharge.NotPresent,
            BatteryStatus.Discharging => BatteryCharge.Discharging,
            BatteryStatus.Charging => BatteryCharge.Charging,
            _ => BatteryCharge.Idle,
        };
        var remaining = PowerManager.RemainingDischargeTime;
        return new BatterySnapshot(
            PowerManager.RemainingChargePercent,
            charge,
            PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent,
            remaining == TimeSpan.MaxValue || remaining <= TimeSpan.Zero ? null : remaining);
    }

#if DEBUG
    /// <summary>"yüzde,Discharging|Charging|Idle,kalan dk ya da -".</summary>
    private static BatterySnapshot? FakeFromEnvironment()
    {
        var parts = Environment.GetEnvironmentVariable("CAMADA_FAKE_BATTERY")?.Split(',');
        if (parts is not { Length: 3 } || !int.TryParse(parts[0], out var percent)
            || !Enum.TryParse<BatteryCharge>(parts[1], ignoreCase: true, out var charge))
        {
            return null;
        }

        TimeSpan? remaining = int.TryParse(parts[2], out var minutes) ? TimeSpan.FromMinutes(minutes) : null;
        return new BatterySnapshot(percent, charge, charge != BatteryCharge.Discharging, remaining);
    }
#endif
}
