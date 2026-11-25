#if DEBUG
using System.IO;
using System.Text.Json.Nodes;
using CamAda.Core.Assistant;
using CamAda.Panels;
using CamAda.QuickSettings;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>
    /// Hafıza dosyası yazılamazken (dosyanın yerinde bir klasör) "hatirla" ve "unut" ne diyor; hafıza değişmeden kalmalı.
    /// Kullanıcının hafızasına dokunmaz (geçici klasör).
    /// </summary>
    private static async Task MemoryWriteAsync()
    {
        var dir = Path.Combine(Path.GetTempPath(), "camada-hafiza-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "ada-hafiza.bin");
        try
        {
            Directory.CreateDirectory(path); // dosyanın yerinde klasör: her kayıt başarısız olur
            var memory = new MemoryFile(dir, TimeProvider.System);
            var context = new AdaToolContext(null, null, null, null, null, new AppIndex(), new SystemQuickLauncher(), PowerActions.Create(),
                () => [], memory, new CurrentInfoClient(() => null, () => ""), () => Settings(), () => { }, _ => { });
            var runner = new AdaToolRunner(context, TimeProvider.System);
            async Task Call(string name, string json)
            {
                var result = await runner.RunAsync(new ToolCall(null, name, JsonNode.Parse(json)!.AsObject()));
                Log($"{name} {json} → {result}; hafızada {memory.Snapshot().Count} madde");
            }

            Log("ada-hafiza-sina: hafıza dosyası yazılamıyor");
            await Call(AdaTools.Hatirla, """{"konu":"şehir","bilgi":"İzmir"}""");
            await Call(AdaTools.Unut, """{"konu":"şehir"}""");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
#endif
