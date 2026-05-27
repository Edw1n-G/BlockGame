namespace Basics.Game.Logic;

public static class GameSettings
{
    private static readonly Lock Lock = new(); // Lock-Objekt für Thread-Sicherheit vor dem bearbeiten der Einstellungen
    
    // Render Distance Einstellungen
    public static int RenderDistance { get; private set; } = 20;
    public static int Lod1Distance { get; private set; } = 15;
    public static int Lod2Distance { get; private set; } = 20;
    public static int VerticalRenderDistance { get; private set; } = 15;
    
    public static int MaxChunkMeshesInRam { get; private set; } = 200;
    
    // Movement Einstellungen
    public static float PlayerMoveSpeed { get; private set; } = 10f;
    public static float MouseSensitivity { get; private set; } = 0.1f;
    
    // Welt Generierung Einstellungen. Noise map detail settings noch nicht hier
    public static int Seed { get; private set; } = 1; // Standard-Seed, später vor dem Start des Spiels setztbar
    public static string Worldtype { get; private set; } = "KBE@CgL8EFQkVCRcJIQBvEgM8CQ0AB@CkG@BY0MUAwAA8EIEAhcJFgMAAKBCCgQIAACAPwwDzczMPgwCFwkbCRYDAABIwgr/BQAMAw@AEAMAhkJJQB7@BCSEAKVwPPQkNAAI@BJBiQLWDk0PRPsUTg9BA=="; // Standard-Worldtype, später vor dem Start des Spiels setztbar
    public static int MapSize { get; private set; } = 100;
    
    // Grafik einstellungen
    
    public static int Msaa { get; private set; } = 2;
    
    // Funktionen zum Anpassen der Werte
    public static void SetRenderDistance(int distance)
    {
        lock (Lock)
        {
            RenderDistance = Math.Max(1, distance);
        }
    }

    public static void SetLod1Distance(int distance)
    {
        lock (Lock)
        {
            Lod1Distance = Math.Max(2, distance);
        }
    }
    
    public static void SetLod2Distance(int distance)
    {
        lock (Lock)
        {
            Lod2Distance = Math.Max(3, distance);
        }
    }
    
    public static void SetMaxChunkMeshesInRam(int size)
    {
        lock (Lock)
        {
            MaxChunkMeshesInRam = Math.Max(1, size);
        }
    }
    
    public static void SetVerticalRenderDistance(int distance)
    {
        lock (Lock)
        {
            VerticalRenderDistance = Math.Max(1, distance);
        }
    }
    
    public static void SetPlayerMoveSpeed(float speed)
    {
        lock (Lock)
        {
            PlayerMoveSpeed = Math.Max(0.1f, speed);
        }
    }
    
    public static void SetMouseSensitivity(float sensitivity)
    {
        lock (Lock)
        {
            MouseSensitivity = Math.Max(0.01f, sensitivity);
        }
    }
    
    public static void SetSeed(int seed)
    {
        lock (Lock)
        {
            Seed = seed;
        }
    }
    
    public static void SetMapSize(int size)
    {
        lock (Lock)
        {
            MapSize = Math.Max(10, size);
        }
    }
}