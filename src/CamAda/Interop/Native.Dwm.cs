using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>DWM canlı pencere önizlemeleri (küçük resimler).</summary>
internal static partial class Native
{
    public const int DWM_TNP_RECTDESTINATION = 0x1;
    public const int DWM_TNP_OPACITY = 0x4;
    public const int DWM_TNP_VISIBLE = 0x8;
    public const int DWM_TNP_SOURCECLIENTAREAONLY = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    public struct ThumbRect : IEquatable<ThumbRect>
    {
        public int Left, Top, Right, Bottom;

        public readonly bool Equals(ThumbRect other) =>
            Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

        public override readonly bool Equals(object? obj) => obj is ThumbRect other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ThumbSize
    {
        public int X, Y;
    }

    /// <summary>DWM_THUMBNAIL_PROPERTIES; BOOL alanlar int olarak (blittable).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DwmThumbnailProperties
    {
        public int Flags;
        public ThumbRect Destination;
        public ThumbRect Source;
        public byte Opacity;
        public int Visible;
        public int SourceClientAreaOnly;
    }

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmUnregisterThumbnail(nint thumbnail);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmUpdateThumbnailProperties(nint thumbnail, in DwmThumbnailProperties properties);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmQueryThumbnailSourceSize(nint thumbnail, out ThumbSize size);
}
