using System.Runtime.InteropServices;

namespace Basics.Window;

/// <summary>
/// Hilfsklasse zur Steuerung nativer Windows-Fenstereigenschaften,
/// die über Silk.NET/GLFW nicht direkt zugänglich sind.
/// </summary>
internal static class NativeWindowHelper
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

    /// <summary>
    /// Deaktiviert (oder reaktiviert) die DWM-Übergangsanimation für ein Fenster.
    /// Verhindert den kurzen Blackscreen/Flacker-Effekt bei Fensterstil-Änderungen
    /// (z.B. beim Umschalten von WindowBorder), der durch die Standard-Animation
    /// des Windows-Fenstermanagers verursacht wird.
    /// </summary>
    /// <param name="hwnd">Der native Windows-Fensterhandle.</param>
    /// <param name="disable"><c>true</c>, um Animationen zu deaktivieren.</param>
    public static void DisableWindowAnimations(nint hwnd, bool disable)
    {
        if (hwnd == nint.Zero) return;
        int value = disable ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref value, sizeof(int));
    }
}