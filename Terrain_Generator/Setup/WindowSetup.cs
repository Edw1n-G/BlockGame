using Silk.NET.Windowing; //Für die Fenstererstellung
using Silk.NET.Maths; //Für die Vector2D Klasse

namespace Basics.Setup;

public class WindowSetup
{
    public static IWindow window { get; private set; }
    
    public static void CreateWindow()
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Terrain Generator";
        
        //options.FramesPerSecond = 60.0;
        //options.UpdatesPerSecond = 60.0;
        options.VSync = true;
        
        window = Window.Create(options);
    }

    public static void Run()
    {
        window.Run();
    }
    
}