using Basics.Game.Logic;
using Basics.Input;
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
        
        //Aktion -> Methode Mappen
        InputManager.SetActionBindings(Actions.Close, () => Window.Close());
        InputManager.SetActionBindings(Actions.Fullscreen, ToggleFullscreen);
        InputManager.SetActionBindings(Actions.Borderless, ToggleBorderless);
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
    
    /// <summary>
    /// Schaltet zwischen Vollbild- und Fenstermodus um.
    /// </summary>
    private static void ToggleFullscreen()
    {   
        if (Window.WindowState == WindowState.Fullscreen)
        {
            // Raus aus Vollbild → Zurück zum normalen Fenster
            Window.WindowState = WindowState.Normal;
            Window.WindowBorder = WindowBorder.Resizable;
        }
        else
        {
            // Bevor wir in den Fullscreen gehen, setzen wir das Fenster in 
            // einen sauberen Grundzustand. So merkt sich das System keine "falschen"
            // Borderless-Eigenschaften, die später zu Glitches führen.
            Window.WindowState = WindowState.Normal;
            Window.WindowBorder = WindowBorder.Resizable;
        
            // Jetzt sicher in den Vollbildmodus wechseln
            Window.WindowState = WindowState.Fullscreen;
        }
    }

    /// <summary>
    /// Schaltet zwischen Borderless-Windowed- und regulärem Fenstermodus um.
    /// </summary>
    private static void ToggleBorderless()
    {
        
        if (Window.WindowBorder == WindowBorder.Hidden || Window.WindowState == WindowState.Fullscreen)
        {
            // Zurück zum normalen kleinen Fenster MIT Rahmen
            Window.WindowState = WindowState.Normal;
            Window.WindowBorder = WindowBorder.Resizable;
        }
        else
        {
            // Rein in den Borderless-Windowed Modus
            Window.WindowState = WindowState.Normal;   // Zuerst ent-maximieren
            Window.WindowBorder = WindowBorder.Hidden; // Dann Rahmen ausblenden
            Window.WindowState = WindowState.Maximized;// Dann über den Bildschirm strecken
        }
    }
}