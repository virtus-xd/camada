using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace CamAda.Assistant.Audio;

/// <summary>
/// Windows'un yankı engelleyicisiyle mikrofon (Voice Capture DSP, <c>CLSID_CWMAudioAEC</c>): DSP mikrofonu kendisi açar
/// (kaynak modu), hoparlöre giden sesi referans alıp mikrofondan çıkarır ve 16 kHz mono 16 bit verir. Ada hoparlörden
/// konuşurken kullanıcı araya girebilsin diye. DSP'ye tampon vermek için eski COM köprüsü "Typelib export" hatası verdi;
/// tampon kaynak üretimli COM ile, çıktı yapısı elle bellekte verilir (Plan 5 denemesi).
/// </summary>
internal sealed partial class EchoCancelledMic(int captureIndex, int renderIndex) : IAdaMic
{
    private static readonly Guid ClsidAec = new("745057c7-f353-4f2d-a7ee-58434477730e");
    private static readonly Guid AecProps = new("6f52c567-0360-4bd2-9617-ccbf1421c939");
    private static readonly Guid MediaTypeAudio = new("73647561-0000-0010-8000-00AA00389B71");
    private static readonly Guid SubtypePcm = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid FormatWaveFormatEx = new("05589f81-c356-11ce-bf01-00aa0055595a");
    private const int OutputIncomplete = 0x01000000;
    private const int ChunkSamples = 1600; // 100 ms
    private static readonly TimeSpan FirstDataTimeout = TimeSpan.FromSeconds(1);

    private Thread? _thread;
    private volatile bool _stop;

    public event Action<short[]>? Samples;

    public event Action<string>? Failed;

    public bool EchoCancelled => true;

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Ada echo canceller" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public void Dispose()
    {
        _stop = true;
        _thread?.Join(2000);
    }

    private void Run()
    {
        object? instance = null;
        IMediaObject? dmo = null;
        MediaBuffer? buffer = null;
        var bufferPointer = IntPtr.Zero;
        var outputs = IntPtr.Zero;
        try
        {
#if DEBUG
            if (Environment.GetEnvironmentVariable("CAMADA_ADA_NO_AEC") == "1")
            {
                throw new COMException("deneme: yankı engelleyici kapalı sayıldı");
            }
#endif
            instance = Activator.CreateInstance(Type.GetTypeFromCLSID(ClsidAec, throwOnError: true)!)!;
            dmo = (IMediaObject)instance;
            var props = (IPropertyStore)instance;
            SetInt(props, 2, 0);                                            // SYSTEM_MODE = SINGLE_CHANNEL_AEC
            SetBool(props, 3, true);                                        // DMO_SOURCE_MODE: mikrofonu DSP açar
            SetInt(props, 4, (renderIndex << 16) | (captureIndex & 0xFFFF)); // DEVICE_INDEXES
            SetOutputFormat(dmo);
            Check(dmo.AllocateStreamingResources(), "AllocateStreamingResources");

            buffer = new MediaBuffer(32000);
            var unknown = new StrategyBasedComWrappers().GetOrCreateComInterfaceForObject(buffer, CreateComInterfaceFlags.None);
            var iid = typeof(IMediaBuffer).GUID;
            Check(Marshal.QueryInterface(unknown, in iid, out bufferPointer), "QueryInterface(IMediaBuffer)");
            Marshal.Release(unknown);
            outputs = Marshal.AllocHGlobal(Marshal.SizeOf<DmoOutputDataBuffer>());

            var pending = new List<short>(ChunkSamples * 2);
            var started = DateTime.UtcNow;
            var anyData = false;
            while (!_stop)
            {
                int status;
                do
                {
                    buffer.Length = 0;
                    Marshal.StructureToPtr(new DmoOutputDataBuffer { Buffer = bufferPointer }, outputs, false);
                    var hr = dmo.ProcessOutput(0, 1, outputs, out _);
                    status = Marshal.PtrToStructure<DmoOutputDataBuffer>(outputs).Status;
                    if (hr == 1)
                    {
                        break; // S_FALSE: şimdilik veri yok
                    }

                    Check(hr, "ProcessOutput");
                    if (buffer.Length > 1)
                    {
                        anyData = true;
                        pending.AddRange(buffer.ToSamples());
                        while (pending.Count >= ChunkSamples)
                        {
                            Samples?.Invoke(pending.GetRange(0, ChunkSamples).ToArray());
                            pending.RemoveRange(0, ChunkSamples);
                        }
                    }
                }
                while ((status & OutputIncomplete) != 0 && !_stop);

                if (!anyData && DateTime.UtcNow - started > FirstDataTimeout)
                {
                    throw new COMException("ses gelmiyor");
                }

                Thread.Sleep(10);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException or InvalidOperationException)
        {
            if (!_stop)
            {
                Failed?.Invoke(ex.Message);
            }
        }
        finally
        {
            try
            {
                dmo?.FreeStreamingResources();
            }
            catch (COMException)
            {
            }

            if (instance is not null)
            {
                Marshal.ReleaseComObject(instance);
            }

            if (outputs != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(outputs);
            }

            if (bufferPointer != IntPtr.Zero)
            {
                Marshal.Release(bufferPointer);
            }

            buffer?.Dispose();
        }
    }

    private static void SetOutputFormat(IMediaObject dmo)
    {
        var format = Marshal.AllocCoTaskMem(18);
        try
        {
            Marshal.WriteInt16(format, 0, 1);        // WAVE_FORMAT_PCM
            Marshal.WriteInt16(format, 2, 1);        // kanal
            Marshal.WriteInt32(format, 4, 16000);    // örnekleme
            Marshal.WriteInt32(format, 8, 32000);    // bayt/sn
            Marshal.WriteInt16(format, 12, 2);       // blok
            Marshal.WriteInt16(format, 14, 16);      // bit
            Marshal.WriteInt16(format, 16, 0);       // cbSize
            var type = new DmoMediaType
            {
                MajorType = MediaTypeAudio,
                SubType = SubtypePcm,
                FixedSizeSamples = 1,
                SampleSize = 2,
                FormatType = FormatWaveFormatEx,
                FormatSize = 18,
                Format = format,
            };
            Check(dmo.SetOutputType(0, ref type, 0), "SetOutputType");
        }
        finally
        {
            Marshal.FreeCoTaskMem(format);
        }
    }

    private static void Check(int hr, string what)
    {
        if (hr < 0)
        {
            throw new COMException($"{what} başarısız (0x{hr:X8})", hr);
        }
    }

    private static void SetInt(IPropertyStore store, int pid, int value)
    {
        var key = new PropertyKey { FormatId = AecProps, PropertyId = pid };
        var variant = new PropVariant { VarType = 3, IntValue = value }; // VT_I4
        Check(store.SetValue(ref key, ref variant), $"SetValue({pid})");
    }

    private static void SetBool(IPropertyStore store, int pid, bool value)
    {
        var key = new PropertyKey { FormatId = AecProps, PropertyId = pid };
        var variant = new PropVariant { VarType = 11, IntValue = value ? -1 : 0 }; // VT_BOOL
        Check(store.SetValue(ref key, ref variant), $"SetValue({pid})");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DmoMediaType
    {
        public Guid MajorType;
        public Guid SubType;
        public int FixedSizeSamples;
        public int TemporalCompression;
        public int SampleSize;
        public Guid FormatType;
        public IntPtr Unknown;
        public int FormatSize;
        public IntPtr Format;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DmoOutputDataBuffer
    {
        public IntPtr Buffer; // IMediaBuffer*
        public int Status;
        public long Timestamp;
        public long TimeLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public int PropertyId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        public short VarType;

        [FieldOffset(8)]
        public int IntValue;
    }

    [ComImport, Guid("d8ad0f58-5494-4102-97c5-ec798e59bcf4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMediaObject
    {
        [PreserveSig] int GetStreamCount(out int inputStreams, out int outputStreams);
        [PreserveSig] int GetInputStreamInfo(int index, out int flags);
        [PreserveSig] int GetOutputStreamInfo(int index, out int flags);
        [PreserveSig] int GetInputType(int index, int typeIndex, IntPtr type);
        [PreserveSig] int GetOutputType(int index, int typeIndex, IntPtr type);
        [PreserveSig] int SetInputType(int index, IntPtr type, int flags);
        [PreserveSig] int SetOutputType(int index, ref DmoMediaType type, int flags);
        [PreserveSig] int GetInputCurrentType(int index, IntPtr type);
        [PreserveSig] int GetOutputCurrentType(int index, IntPtr type);
        [PreserveSig] int GetInputSizeInfo(int index, out int size, out int maxLookahead, out int alignment);
        [PreserveSig] int GetOutputSizeInfo(int index, out int size, out int alignment);
        [PreserveSig] int GetInputMaxLatency(int index, out long latency);
        [PreserveSig] int SetInputMaxLatency(int index, long latency);
        [PreserveSig] int Flush();
        [PreserveSig] int Discontinuity(int index);
        [PreserveSig] int AllocateStreamingResources();
        [PreserveSig] int FreeStreamingResources();
        [PreserveSig] int GetInputStatus(int index, out int flags);
        [PreserveSig] int ProcessInput(int index, IntPtr buffer, int flags, long time, long timeLength);
        [PreserveSig] int ProcessOutput(int flags, int count, IntPtr buffers, out int status);
        [PreserveSig] int Lock(int lockFlag);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetAt(int index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [GeneratedComInterface]
    [Guid("59eff8b9-938c-4a26-82f2-95cb84cdc837")]
    internal partial interface IMediaBuffer
    {
        [PreserveSig] int SetLength(int length);
        [PreserveSig] int GetMaxLength(out int maxLength);
        [PreserveSig] int GetBufferAndLength(IntPtr buffer, IntPtr length);
    }

    [GeneratedComClass]
    internal sealed partial class MediaBuffer : IMediaBuffer, IDisposable
    {
        private readonly IntPtr _data;
        private readonly int _max;

        public MediaBuffer(int max)
        {
            _max = max;
            _data = Marshal.AllocCoTaskMem(max);
        }

        public int Length { get; set; }

        public int SetLength(int length)
        {
            if (length < 0 || length > _max)
            {
                return unchecked((int)0x80070057); // E_INVALIDARG
            }

            Length = length;
            return 0;
        }

        public int GetMaxLength(out int maxLength)
        {
            maxLength = _max;
            return 0;
        }

        public int GetBufferAndLength(IntPtr buffer, IntPtr length)
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.WriteIntPtr(buffer, _data);
            }

            if (length != IntPtr.Zero)
            {
                Marshal.WriteInt32(length, Length);
            }

            return 0;
        }

        public short[] ToSamples()
        {
            var samples = new short[Length / 2];
            Marshal.Copy(_data, samples, 0, samples.Length);
            return samples;
        }

        public void Dispose() => Marshal.FreeCoTaskMem(_data);
    }
}
