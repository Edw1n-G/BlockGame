namespace Basics.Game;

public static class GameSettings
{
    private static readonly object _lock = new(); // Lock-Objekt für Thread-Sicherheit vor dem bearbeiten der Einstellungen
    
    // Render Distance Einstellungen
    public static int RenderDistance { get; private set; } = 30;
    public static int Lod1Distance { get; private set; } = 15;
    public static int Lod2Distance { get; private set; } = 20;
    public static int VerticalRenderDistance { get; private set; } = 10;
    
    // Movement Einstellungen
    public static float PlayerMoveSpeed { get; private set; } = 5f;
    public static float MouseSensitivity { get; private set; } = 0.1f;
    
    // Welt Generierung Einstellungen. Noise map detail settings noch nicht hier
    public static int Seed { get; private set; } = 0; // Standard-Seed, später vor dem Start des Spiels setztbar
    public static int MapSize { get; private set; } = 100;
    
    
    
    // Funktionen zum Anpassen der Werte
    public static void SetRenderDistance(int distance)
    {
        lock (_lock)
        {
            RenderDistance = Math.Max(1, distance);
        }
    }

    public static void SetLod1Distance(int distance)
    {
        lock (_lock)
        {
            Lod1Distance = Math.Max(2, distance);
        }
    }
    
    public static void SetLod2Distance(int distance)
    {
        lock (_lock)
        {
            Lod2Distance = Math.Max(3, distance);
        }
    }
    
    public static void SetVerticalRenderDistance(int distance)
    {
        lock (_lock)
        {
            VerticalRenderDistance = Math.Max(1, distance);
        }
    }
    
    public static void SetPlayerMoveSpeed(float speed)
    {
        lock (_lock)
        {
            PlayerMoveSpeed = Math.Max(0.1f, speed);
        }
    }
    
    public static void SetMouseSensitivity(float sensitivity)
    {
        lock (_lock)
        {
            MouseSensitivity = Math.Max(0.01f, sensitivity);
        }
    }
    
    public static void SetSeed(int seed)
    {
        lock (_lock)
        {
            Seed = seed;
        }
    }
    
    public static void SetMapSize(int size)
    {
        lock (_lock)
        {
            MapSize = Math.Max(10, size);
        }
    }
}