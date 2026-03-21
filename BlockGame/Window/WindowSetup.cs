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
        options.Title = "Game";
        
        //options.FramesPerSecond = 60.0;
        //options.UpdatesPerSecond = 60.0;
        options.VSync = true;
        
        Window = Silk.NET.Windowing.Window.Create(options);
        
        //Aktion -> Methode Mappen
        InputManager.SetActionBindings(Actions.Close, () => Window.Close());
        InputManager.SetActionBindings(Actions.Fullscreen, ToggleFullscreen);
        InputManager.SetActionBindings(Actions.Borderless, ToggleBorderless);
    }

    public static void Run()
    {
        Window.Run();
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
        // FIX 2: Hier das logische ODER (||) nutzen
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