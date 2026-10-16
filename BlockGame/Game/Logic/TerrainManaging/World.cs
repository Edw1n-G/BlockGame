using System;

namespace Basics.Game.Logic.TerrainManaging;

public interface IWorld
{
    ChunkProvider Provider { get; }
    bool ModifyBlock(int x, int y, int z, ushort blockId);
    ushort GetBlock(int x, int y, int z);
}

/// <summary>
/// Verwaltet Voxel-Interaktionen. Kann als Instanz in einem EngineContext existieren
/// oder über die statische Fassade (für Gameplay-Skripte) angesprochen werden.
/// </summary>
public class World : IWorld
{
    private readonly ChunkProvider _chunkProvider;

    public World(ChunkProvider provider)
    {
        _chunkProvider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public ChunkProvider Provider => _chunkProvider;

    public bool SetBlock(int x, int y, int z, ushort blockId)
    {
        return _chunkProvider.ModifyBlock(x, y, z, blockId);
    }

    public ushort GetBlockAt(int x, int y, int z)
    {
        return _chunkProvider.GetBlockAt(x, y, z);
    }

    bool IWorld.ModifyBlock(int x, int y, int z, ushort blockId)
    {
        return SetBlock(x, y, z, blockId);
    }

    ushort IWorld.GetBlock(int x, int y, int z)
    {
        return GetBlockAt(x, y, z);
    }

    // =========================================================================
    // --- Statische Fassade für Gameplay-Skripte & Ergonomie ---
    // =========================================================================

    private static World? _activeInstance;

    public static World Current =>
        EngineContext.Current?.World ?? _activeInstance ?? throw new InvalidOperationException("World wurde nicht initialisiert!");

    public static void Initialize(ChunkProvider provider)
    {
        _activeInstance = new World(provider);
    }

    public static void SetActive(World world)
    {
        _activeInstance = world;
    }

    public static void ModifyBlock(int x, int y, int z, ushort blockId)
    {
        Current.SetBlock(x, y, z, blockId);
    }

    public static ushort GetBlock(int x, int y, int z)
    {
        return Current.GetBlockAt(x, y, z);
    }
}