using System.Text.Json;

namespace CamAda.Core.Safety;

/// <summary>Güvenlik durumunu %LOCALAPPDATA%\CamAda\taskbar-state.json içinde saklar.</summary>
public sealed class TaskbarSafetyStore
{
    /// <summary>ABS_ALWAYSONTOP: otomatik gizleme kapalı.</summary>
    public const int DefaultTaskbarState = 2;

    private readonly string _filePath;

    public TaskbarSafetyStore(string filePath) => _filePath = filePath;

    /// <summary>Dosya yoksa null. Bozuksa güvenli tarafta kalır: geri getirilmesi gereken bir durum döner.</summary>
    public TaskbarSafetyState? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TaskbarSafetyState>(File.ReadAllText(_filePath))
                   ?? FailSafe();
        }
        catch (JsonException)
        {
            return FailSafe();
        }
    }

    /// <summary>Önce geçici dosyaya yazar, sonra yerine taşır; yarım yazılmış dosya kalmaz.</summary>
    public void Save(TaskbarSafetyState state)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = _filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state));
        File.Move(temp, _filePath, overwrite: true);
    }

    private static TaskbarSafetyState FailSafe() => new(true, false, DefaultTaskbarState);
}
