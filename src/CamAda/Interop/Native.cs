using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>ManagedShell'in sağlamadığı küçük Win32 çağrıları.</summary>
internal static partial class Native
{
    /// <summary>Explorer her başladığında tüm üst düzey pencerelere yayınlanan mesajın kimliği.</summary>
    public static readonly int TaskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RegisterWindowMessage(string message);
}
