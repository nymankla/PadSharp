using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace PadSharp;

/// <summary>
/// Puts the screenshot window behind other windows. Only Windows supports this safely (a covered
/// window keeps drawing there); on macOS and Linux it does nothing.
/// </summary>
internal static partial class WindowZOrder
{
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    public static void SendToBack(Window window)
    {
        if (!OperatingSystem.IsWindows() || window.TryGetPlatformHandle() is not { } handle)
        {
            return;
        }

        _ = SetWindowPos(handle.Handle, new IntPtr(1), 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    [SupportedOSPlatform("windows")]
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
