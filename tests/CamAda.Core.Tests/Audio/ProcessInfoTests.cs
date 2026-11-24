using System.Diagnostics;
using System.Runtime.InteropServices;
using CamAda.Core.Audio;

namespace CamAda.Core.Tests.Audio;

public class ProcessInfoTests
{
    // Son inceleme, önemli 1: süreç bilgisi okunurken süreç kapanırsa ProcessName InvalidOperationException fırlatıyordu.
    [Fact]
    public void Exited_process_reads_as_null()
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { CreateNoWindow = true, UseShellExecute = false })!;
        process.WaitForExit();

        Assert.Null(ProcessInfoReader.Read(process));
    }

    [Fact]
    public void Idle_and_missing_ids_read_as_null()
    {
        Assert.Null(ProcessInfoReader.Read(0));
        Assert.Null(ProcessInfoReader.Read(-1));
    }

    [Fact]
    public void Own_process_has_exe_and_path()
    {
        var details = ProcessInfoReader.Read(Environment.ProcessId);

        Assert.NotNull(details);
        Assert.EndsWith(".exe", details.Exe, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(details.Path));
    }

    // Son inceleme, önemli 2: önbellek hiç budanmıyor, başarısız okumalar da saklanıyordu.
    [Fact]
    public void Failures_are_not_cached()
    {
        var reads = 0;
        var cache = new ProcessInfoCache(_ => ++reads == 1 ? null : new ProcessDetails("a.exe", "A", null));

        Assert.Null(cache.Get(7));
        Assert.Equal("a.exe", cache.Get(7)?.Exe);
        Assert.Equal(2, reads);
    }

    [Fact]
    public void Gone_processes_are_forgotten()
    {
        var name = "chrome.exe";
        var cache = new ProcessInfoCache(_ => new ProcessDetails(name, "", null));
        Assert.Equal("chrome.exe", cache.Get(7)?.Exe);

        cache.Retain([8]); // 7 artık oturumlar arasında yok
        name = "discord.exe"; // Windows 7'yi başka bir sürece verdi

        Assert.Equal("discord.exe", cache.Get(7)?.Exe);
    }

    [Fact]
    public void Known_processes_are_read_once()
    {
        var reads = 0;
        var cache = new ProcessInfoCache(_ => { reads++; return new ProcessDetails("a.exe", "", null); });

        cache.Get(7);
        cache.Retain([7]);
        cache.Get(7);

        Assert.Equal(1, reads);
    }

    // Son inceleme, küçük 2 (yükseltildi): Marshal.ThrowExceptionForHR bazı HRESULT'ları COMException dışı türlere çevirir.
    [Theory]
    [InlineData(unchecked((int)0x80070057))] // E_INVALIDARG → ArgumentException
    [InlineData(unchecked((int)0x80070005))] // E_ACCESSDENIED → UnauthorizedAccessException
    [InlineData(unchecked((int)0x88890004))] // AUDCLNT_E_DEVICE_INVALIDATED → COMException
    public void Core_audio_failures_are_recoverable(int hresult) =>
        Assert.True(CoreAudioErrors.IsRecoverable(Marshal.GetExceptionForHR(hresult)!));

    [Fact]
    public void Programming_errors_are_not_swallowed()
    {
        Assert.False(CoreAudioErrors.IsRecoverable(new InvalidOperationException()));
        Assert.False(CoreAudioErrors.IsRecoverable(new NullReferenceException()));
    }
}
