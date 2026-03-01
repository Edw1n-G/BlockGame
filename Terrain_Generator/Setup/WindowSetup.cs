using Silk.NET.Windowing; //Für die Fenstererstellung
using Silk.NET.Maths; //Für die Vector2D Klasse

namespace Basics.Setup;

public class WindowSetup
{
    public static IWindow Window { get; private set; } = null!;
    
    public static void CreateWindow()
    {
        WindowOptions options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Terrain Generator";
        
        //options.FramesPerSecond = 60.0;
        //options.UpdatesPerSecond = 60.0;
        options.VSync = true;
        
        Window = Silk.NET.Windowing.Window.Create(options);
    }

    public static void Run()
    {
        Window.Run();
    }
    
}