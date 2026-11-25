namespace CamAda.Assistant.Audio;

/// <summary>Ada'nın mikrofonu: 16 kHz mono 16 bit örnekler verir.</summary>
public interface IAdaMic : IDisposable
{
    /// <summary>~100 ms'lik örnek parçası (ses iş parçacığında).</summary>
    event Action<short[]>? Samples;

    /// <summary>Mikrofon açılamadı ya da durdu (ses iş parçacığında).</summary>
    event Action<string>? Failed;

    /// <summary>Hoparlörde çalan ses mikrofondan çıkarılıyor mu (söz kesme yalnız bu durumda güvenli).</summary>
    bool EchoCancelled { get; }

    void Start();
}
