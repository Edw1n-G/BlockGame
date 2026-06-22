using Basics.Game.Logic;
using Basics.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Basics.Window;

public class WindowSetup
{
    public static IWindow Window { get; private set; } = null!;
    
    public static void CreateWindow()
    {
        WindowOptions options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Samples = GameSettings.Msaa;
        options.Title = "Game";
        
        options.FramesPerSecond = GameSettings.FrameRate;
        options.UpdatesPerSecond = 60.0;
        options.VSync = GameSettings.VSync;
        
        Window = Silk.NET.Windowing.Window.Create(options);
        
        GameSettings.OnFpsChanged += OnFpsChanged;
        GameSettings.OnVSyncChanged += OnVSyncChanged;
        
        //Aktion -> Methode Mappen
        InputManager.SetActionBindings(Actions.Close, () => Window.Close());
        InputManager.SetActionBindings(Actions.Fullscreen, ToggleFullscreen);
        InputManager.SetActionBindings(Actions.Borderless, ToggleBorderless);
    }

    public static void Run()
    {
        Window.Run();
    }
    
    //Events die von GameSettings getriggert werden
    private static void OnFpsChanged(int newFps)
    {
        if (Window == null) return;
    
        // Das Limit greift in Silk.NET meist nur, wenn VSync aus ist
        Window.FramesPerSecond = newFps;
        Window.UpdatesPerSecond = newFps;
    }
    
    private static void OnVSyncChanged(bool VSync)
    {
        if (Window == null) return;
    
        Window.VSync = VSync;
    }
    
    //Funktionen die durch Tasten getriggert werden können.
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