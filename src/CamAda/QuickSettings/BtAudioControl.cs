using System.Runtime.InteropServices;
using CamAda.Interop;

namespace CamAda.QuickSettings;

/// <summary>
/// Eşleşmiş Bluetooth ses cihazını bağlar ya da ayırır: kapsayıcı kimliği eşleşen her çıkış ucunun ses filtresine
/// Microsoft'un belgelediği tek seferlik yeniden bağlan/ayır komutu gönderilir (KSPROPSETID_BtAudio). Arka plan
/// iş parçacığında çağrılır. Edinilen COM nesneleri iş bitince serbest bırakılır (çöp toplayıcıya bırakılmaz).
/// </summary>
public static class BtAudioControl
{
    /// <summary>En az bir ses ucuna komut S_OK ile gittiyse true.</summary>
    public static bool TrySend(string containerId, bool reconnect)
    {
        if (!Guid.TryParse(containerId, out var container))
        {
            return false;
        }

        var sent = false;
        var com = new ComObjects();
        try
        {
            var enumerator = com.Track((KsAudio.IMMDeviceEnumerator)new KsAudio.MMDeviceEnumerator());
            if (enumerator.EnumAudioEndpoints(KsAudio.ERender, KsAudio.DeviceStateActive | KsAudio.DeviceStateUnplugged,
                    out var endpoints) != 0)
            {
                return false;
            }

            com.Track(endpoints);
            endpoints.GetCount(out var count);
            for (var i = 0; i < count; i++)
            {
                if (endpoints.Item(i, out var endpoint) == 0 && ContainerOf(com, com.Track(endpoint)) == container
                    && Send(com, enumerator, endpoint, reconnect))
                {
                    sent = true;
                }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
        }
        finally
        {
            com.ReleaseAll();
        }

        return sent;
    }

    private static Guid? ContainerOf(ComObjects com, KsAudio.IMMDevice endpoint)
    {
        if (endpoint.OpenPropertyStore(0, out var store) != 0)
        {
            return null;
        }

        com.Track(store);

        var key = KsAudio.ContainerIdKey;
        if (store.GetValue(ref key, out var value) != 0)
        {
            return null;
        }

        try
        {
            return value.Vt == 72 /* VT_CLSID */ && value.Pointer != 0 ? Marshal.PtrToStructure<Guid>(value.Pointer) : null;
        }
        finally
        {
            KsAudio.PropVariantClear(ref value);
        }
    }

    /// <summary>Uç → topoloji → bağlayıcı → karşı parça → filtre aygıtı → IKsControl → komut.</summary>
    private static bool Send(ComObjects com, KsAudio.IMMDeviceEnumerator enumerator, KsAudio.IMMDevice endpoint, bool reconnect)
    {
        var topologyId = typeof(KsAudio.IDeviceTopology).GUID;
        if (endpoint.Activate(ref topologyId, KsAudio.ClsctxAll, 0, out var topologyObject) != 0
            || ((KsAudio.IDeviceTopology)com.Track(topologyObject)).GetConnector(0, out var connector) != 0
            || com.Track(connector).GetConnectedTo(out var other) != 0
            || ((KsAudio.IPart)com.Track(other)).GetTopologyObject(out var filterTopology) != 0
            || com.Track(filterTopology).GetDeviceId(out var filterId) != 0
            || enumerator.GetDevice(filterId, out var filter) != 0)
        {
            return false;
        }

        com.Track(filter);
        var ksId = typeof(KsAudio.IKsControl).GUID;
        if (filter.Activate(ref ksId, KsAudio.ClsctxAll, 0, out var ksObject) != 0)
        {
            return false;
        }

        com.Track(ksObject);

        var property = new KsAudio.KSPROPERTY
        {
            Set = KsAudio.KsPropSetBtAudio,
            Id = reconnect ? KsAudio.OneShotReconnect : KsAudio.OneShotDisconnect,
            Flags = KsAudio.KsPropertyTypeGet,
        };
        return ((KsAudio.IKsControl)ksObject).KsProperty(ref property, Marshal.SizeOf<KsAudio.KSPROPERTY>(), 0, 0, out _) == 0;
    }

    /// <summary>
    /// Edinilen COM nesneleri: her edinim bir kez geri verilir (<see cref="Marshal.ReleaseComObject"/>). Final sürüm
    /// kullanılmaz: CLR aynı COM nesnesi için tek sarmalayıcı tutar; NAudio'nun ses servisi aynı nesneyi (ör. cihaz
    /// numaralandırıcısı) tutuyorsa onunkini koparırdı.
    /// </summary>
    private sealed class ComObjects
    {
        private readonly List<object> _objects = new();

        public T Track<T>(T value)
        {
            if (value is not null && Marshal.IsComObject(value))
            {
                _objects.Add(value);
            }

            return value;
        }

        public void ReleaseAll()
        {
            foreach (var value in _objects)
            {
                Marshal.ReleaseComObject(value);
            }

            _objects.Clear();
        }
    }
}
