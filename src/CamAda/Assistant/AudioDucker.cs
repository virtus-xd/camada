using CamAda.Audio;
using CamAda.Core.Audio;

namespace CamAda.Assistant;

/// <summary>
/// Ada konuşurken öbür uygulamaların sesini kısar (müzik çalıyorsa Ada duyulsun diye): CamAda'nın kendi süreci hariç
/// uygulama ses oturumlarının düzeyi kaydedilip çarpılır, oturum bitince kaydedilen düzeyler geri konur.
/// </summary>
public sealed class AudioDucker(AudioService audio)
{
    private readonly List<AppVolumeRow> _saved = [];

    public void Duck(double factor)
    {
        if (_saved.Count > 0)
        {
            return; // zaten kısık
        }

        var self = Environment.ProcessId;
        foreach (var row in audio.Apps().Where(r => !r.ProcessIds.Contains(self)))
        {
            _saved.Add(row);
            audio.SetAppVolume(row, (float)(row.Volume * factor));
        }
    }

    public void Restore()
    {
        foreach (var row in _saved)
        {
            audio.SetAppVolume(row, row.Volume);
        }

        _saved.Clear();
    }
}
