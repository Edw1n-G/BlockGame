using System;

namespace Basics.Game.Logic;

/// <summary>
/// Hält alle Konfigurationseinstellungen für eine Engine-Instanz.
/// Kann als isolierte Instanz pro EngineContext/EngineSession existieren
/// oder über GameSettings.Current angesprochen werden.
/// </summary>
public class GameSettings
{
    private readonly Lock _lock = new();

    // Render Distance Einstellungen
    public int RenderDistance { get; set; } = 20;
    public int Lod1Distance { get; set; } = 15;
    public int Lod2Distance { get; set; } = 20;
    public int VerticalRenderDistance { get; set; } = 15;
    
    // Chunk Gen Einstellungen
    public int MaxChunkMeshesInRam { get; set; } = 200;
    
    // Movement Einstellungen
    public float PlayerMoveSpeed { get; set; } = 10f;
    public float MouseSensitivity { get; set; } = 0.1f;
    
    // Welt Generierung Einstellungen
    public int Seed { get; set; } = 1;
    public string Worldtype { get; set; } = "KBE@CgL8EFQkVCRcJIQBvEgM8CQ0AB@CkG@BY0MUAwAA8EIEAhcJFgMAAKBCCgQIAACAPwwDzczMPgwCFwkbCRYDAABIwgr/BQAMAw@AEAMAhkJJQB7@BCSEAKVwPPQkNAAI@BJBiQLWDk0PRPsUTg9BA==";
    public int MapSize { get; set; } = 100;
    
    // Grafik einstellungen
    public int Msaa { get; set; } = 2;
    
    // Render einstellungen
    public int FrameRate { get; set; } = 120;
    public event Action<int>? OnFpsChanged;
    
    public bool VSync { get; set; } = true;
    public event Action<bool>? OnVSyncChanged;
    
    public bool LowLatencyRenderer { get; set; } = false;

    public GameSettings()
    {
    }

    public GameSettings Clone()
    {
        lock (_lock)
        {
            return new GameSettings
            {
                RenderDistance = this.RenderDistance,
                Lod1Distance = this.Lod1Distance,
                Lod2Distance = this.Lod2Distance,
                VerticalRenderDistance = this.VerticalRenderDistance,
                MaxChunkMeshesInRam = this.MaxChunkMeshesInRam,
                PlayerMoveSpeed = this.PlayerMoveSpeed,
                MouseSensitivity = this.MouseSensitivity,
                Seed = this.Seed,
                Worldtype = this.Worldtype,
                MapSize = this.MapSize,
                Msaa = this.Msaa,
                FrameRate = this.FrameRate,
                VSync = this.VSync,
                LowLatencyRenderer = this.LowLatencyRenderer
            };
        }
    }

    public void SetRenderDistance(int distance)
    {
        lock (_lock)
        {
            RenderDistance = Math.Max(1, distance);
        }
    }

    public void SetLod1Distance(int distance)
    {
        lock (_lock)
        {
            Lod1Distance = Math.Max(2, distance);
        }
    }

    public void SetLod2Distance(int distance)
    {
        lock (_lock)
        {
            Lod2Distance = Math.Max(3, distance);
        }
    }

    public void SetMaxChunkMeshesInRam(int size)
    {
        lock (_lock)
        {
            MaxChunkMeshesInRam = Math.Max(1, size);
        }
    }

    public void SetVerticalRenderDistance(int distance)
    {
        lock (_lock)
        {
            VerticalRenderDistance = Math.Max(1, distance);
        }
    }

    public void SetPlayerMoveSpeed(float speed)
    {
        lock (_lock)
        {
            PlayerMoveSpeed = Math.Max(0.1f, speed);
        }
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        lock (_lock)
        {
            MouseSensitivity = Math.Max(0.01f, sensitivity);
        }
    }

    public void SetSeed(int seed)
    {
        lock (_lock)
        {
            Seed = seed;
        }
    }

    public void SetMapSize(int size)
    {
        lock (_lock)
        {
            MapSize = Math.Max(10, size);
        }
    }

    public void SetMsaa(int aa)
    {
        lock (_lock)
        {
            Msaa = aa;
        }
    }

    public void SetFrameRate(int newFrameRate)
    {
        lock (_lock)
        {
            FrameRate = newFrameRate;
        }
        OnFpsChanged?.Invoke(newFrameRate);
    }

    public void SetVSync(bool vsync)
    {
        lock (_lock)
        {
            VSync = vsync;
        }
        OnVSyncChanged?.Invoke(vsync);
    }

    // =========================================================================
    // --- Statische Fassade: Delegiert an den aktiven Engine-Kontext ---
    // =========================================================================

    private static GameSettings _defaultInstance = new();

    public static GameSettings Current =>
        EngineContext.Current?.Settings ?? _defaultInstance;

    public static void SetGlobalDefault(GameSettings settings)
    {
        _defaultInstance = settings ?? new GameSettings();
    }
}