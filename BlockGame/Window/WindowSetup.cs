using Basics.Game.Input;
using Basics.Game.Logic;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Basics.Window;

/// <summary>
/// Verwaltung und Konfiguration des Hauptfensters.
/// Stellt Methoden zum Erstellen, Konfigurieren und Steuern des Windows bereit.
/// </summary>
public class WindowSetup
{
    /// <summary>
    /// Das aktuell verwendete Window-Objekt.
    /// </summary>
    public static IWindow Window { get; private set; } = null!;
    
    /// <summary>
    /// Erstellt und initialisiert das Hauptfenster mit den aktuellen Einstellungen.
    /// </summary>
    public static void CreateWindow()
    {
        WindowOptions options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Samples = GameSettings.Current.Msaa;
        options.Title = "Game";
        
        options.FramesPerSecond = GameSettings.Current.FrameRate;
        options.UpdatesPerSecond = 60.0;
        options.VSync = GameSettings.Current.VSync;
        
        Window = Silk.NET.Windowing.Window.Create(options);
        
        GameSettings.Current.OnFpsChanged += OnFpsChanged;
        GameSettings.Current.OnVSyncChanged += OnVSyncChanged;
    }
    
    /// <summary>
    /// Bindet fensterweite Aktionen an den InputManager. Muss aufgerufen werden,
    /// NACHDEM der InputManager mit einem echten IInputContext initialisiert wurde
    /// (also erst in StateManager.onLoad, nicht in CreateWindow).
    /// </summary>
    public static void BindWindowActions(InputManager inputManager)
    {
        inputManager.SetActionBindings(Actions.Close, () => Window.Close());
        inputManager.SetActionBindings(Actions.Fullscreen, ToggleFullscreen);
        inputManager.SetActionBindings(Actions.Borderless, ToggleBorderless);
    }

    
    /// <summary>
    /// Führt die Haupt-Loop des Windows aus.
    /// </summary>
    public static void Run()
    {
        Window.Run();
    }
    
    /// <summary>
    /// Event-Handler für FPS-Änderungen.
    /// Aktualisiert die Frame-Rate des Windows.
    /// </summary>
    /// <param name="newFps">Die neue Frame-Rate in FPS.</param>
    private static void OnFpsChanged(int newFps)
    {
        if (Window == null) return;
    
        // Das Limit greift in Silk.NET meist nur, wenn VSync aus ist
        Window.FramesPerSecond = newFps;
        Window.UpdatesPerSecond = newFps;
    }
    
    /// <summary>
    /// Event-Handler für VSync-Änderungen.
    /// Aktualisiert den VSync-Modus des Windows.
    /// </summary>
    /// <param name="VSync">Der neue VSync-Status.</param>
    private static void OnVSyncChanged(bool VSync)
    {
        if (Window == null) return;
    
        Window.VSync = VSync;
    }
    
    private static nint GetNativeHandle()
    {
        if (Window.Native?.Win32 is (var hWnd, _, _))
            return hWnd;
        return nint.Zero;
    }
    
    /// <summary>
    /// Schaltet zwischen Vollbild- und Fenstermodus um.
    /// </summary>
    private static void ToggleFullscreen()
    {   
        var hwnd = GetNativeHandle();
        NativeWindowHelper.DisableWindowAnimations(hwnd, true);

        if (Window.WindowState == WindowState.Fullscreen)
        {
            Window.WindowState = WindowState.Normal;
            Window.WindowBorder = WindowBorder.Resizable;
        }
        else
        {
            Window.WindowState = WindowState.Fullscreen;
        }

        NativeWindowHelper.DisableWindowAnimations(hwnd, false);
    }

    /// <summary>
    /// Schaltet zwischen Borderless-Windowed- und regulärem Fenstermodus um.
    /// </summary>
    private static void ToggleBorderless()
    {
        var hwnd = GetNativeHandle();
        NativeWindowHelper.DisableWindowAnimations(hwnd, true);

        if (Window.WindowBorder == WindowBorder.Hidden || Window.WindowState == WindowState.Fullscreen)
        {
            Window.WindowState = WindowState.Normal;
            Window.WindowBorder = WindowBorder.Resizable;
        }
        else
        {
            Window.WindowBorder = WindowBorder.Hidden;
            Window.WindowState = WindowState.Maximized;
        }

        NativeWindowHelper.DisableWindowAnimations(hwnd, false);
    }
}