using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CamAda.Core.Assistant;

namespace CamAda.Assistant;

/// <summary>Bu ayın tahmini harcaması: %LOCALAPPDATA%\CamAda\ada-kullanim.json (Ada ayarları panelinde görünür).</summary>
public sealed class UsageFile(string directory)
{
    private readonly string _path = Path.Combine(directory, "ada-kullanim.json");

    /// <summary>Bu ayın kaydı; bu ay hiç oturum yoksa null.</summary>
    public UsageMonth? Current =>
        Load() is { } month && month.Month == DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture) ? month : null;

    public void Add(DateOnly today, decimal usd, int sessions, int searches)
    {
        var next = UsageLedger.Add(Load(), today, usd, sessions, searches);
        try
        {
            Directory.CreateDirectory(directory);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(next));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Ada kullanımı yazılamadı: {ex.Message}");
        }
    }

    private UsageMonth? Load()
    {
        try
        {
            return File.Exists(_path) ? JsonSerializer.Deserialize<UsageMonth>(File.ReadAllText(_path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
