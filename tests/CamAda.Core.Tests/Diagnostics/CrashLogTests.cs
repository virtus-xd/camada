using CamAda.Core.Diagnostics;

namespace CamAda.Core.Tests.Diagnostics;

public class CrashLogTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("camada-").FullName;
    private string LogPath => Path.Combine(_dir, "crash.log");
    private string OldPath => Path.Combine(_dir, "crash.old.log");
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 15, 0, TimeSpan.FromHours(3));

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Append_writes_timestamp_and_exception_message()
    {
        CrashLog.Append(_dir, new InvalidOperationException("ada patladı"), Now);
        var text = File.ReadAllText(LogPath);
        Assert.Contains("2026-11-24 10:15:00", text);
        Assert.Contains("ada patladı", text);
    }

    [Fact]
    public void Two_appends_leave_two_entries()
    {
        CrashLog.Append(_dir, new Exception("birinci"), Now);
        CrashLog.Append(_dir, new Exception("ikinci"), Now.AddMinutes(1));
        var text = File.ReadAllText(LogPath);
        Assert.Contains("birinci", text);
        Assert.Contains("ikinci", text);
        Assert.Contains("2026-11-24 10:16:00", text);
    }

    [Fact]
    public void Oversized_log_rotates_to_old_file()
    {
        File.WriteAllText(LogPath, new string('x', (int)CrashLog.MaxBytes + 10));
        CrashLog.Append(_dir, new Exception("yeni kayıt"), Now);
        Assert.True(File.Exists(OldPath));
        Assert.StartsWith("xxx", File.ReadAllText(OldPath));
        var current = File.ReadAllText(LogPath);
        Assert.Contains("yeni kayıt", current);
        Assert.DoesNotContain("xxx", current);
    }
}
