using System;

namespace Basics.Game.Logic.TerrainManaging;

public interface IWorld
{
    ChunkProvider Provider { get; }
    bool ModifyBlock(int x, int y, int z, ushort blockId);
    ushort GetBlock(int x, int y, int z);
}

/// <summary>
/// Interface für Voxel-Interaktionen mit der Welt.
/// </summary>
public interface IWorld
{
    /// <summary>Der für diese Welt verwendete <see cref="ChunkProvider"/>.</summary>
    ChunkProvider Provider { get; }
    /// <summary>Ändert einen Block an der angegebenen Position.</summary>
    /// <param name="x">Die X-Koordinate in Weltkoordinaten.</param>
    /// <param name="y">Die Y-Koordinate in Weltkoordinaten.</param>
    /// <param name="z">Die Z-Koordinate in Weltkoordinaten.</param>
    /// <param name="blockId">Die neue Block-ID.</param>
    /// <returns><c>true</c>, wenn der Block erfolgreich geändert wurde, andernfalls <c>false</c>.</returns>
    bool ModifyBlock(int x, int y, int z, ushort blockId);
    /// <summary>Gibt die Block-ID an der angegebenen Position zurück.</summary>
    /// <param name="x">Die X-Koordinate in Weltkoordinaten.</param>
    /// <param name="y">Die Y-Koordinate in Weltkoordinaten.</param>
    /// <param name="z">Die Z-Koordinate in Weltkoordinaten.</param>
    /// <returns>Die Block-ID an der Position oder 0, wenn der Block nicht existiert.</returns>
    ushort GetBlock(int x, int y, int z);
}

/// <summary>
/// Verwaltet Voxel-Interaktionen mit der Welt.
/// Bietet sowohl eine Instanz-API als auch eine statische Fassade für Gameplay-Skripte.
/// </summary>
public class World : IWorld
{
    private readonly ChunkProvider _chunkProvider;

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="World"/>.
    /// </summary>
    /// <param name="provider">Der für diese Welt zu verwendende <see cref="ChunkProvider"/>.</param>
    /// <exception cref="ArgumentNullException">Wird ausgelöst, wenn <paramref name="provider"/> null ist.</exception>
    public World(ChunkProvider provider)
    {
        _chunkProvider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>Der für diese Welt verwendete <see cref="ChunkProvider"/>.</summary>
    public ChunkProvider Provider => _chunkProvider;

    /// <summary>Ändert einen Block an der angegebenen Position.</summary>
    /// <param name="x">Die X-Koordinate in Weltkoordinaten.</param>
    /// <param name="y">Die Y-Koordinate in Weltkoordinaten.</param>
    /// <param name="z">Die Z-Koordinate in Weltkoordinaten.</param>
    /// <param name="blockId">Die neue Block-ID.</param>
    /// <returns><c>true</c>, wenn der Block erfolgreich geändert wurde, andernfalls <c>false</c>.</returns>
    public bool SetBlock(int x, int y, int z, ushort blockId)
    {
        return _chunkProvider.ModifyBlock(x, y, z, blockId);
    }

    /// <summary>Gibt die Block-ID an der angegebenen Position zurück.</summary>
    /// <param name="x">Die X-Koordinate in Weltkoordinaten.</param>
    /// <param name="y">Die Y-Koordinate in Weltkoordinaten.</param>
    /// <param name="z">Die Z-Koordinate in Weltkoordinaten.</param>
    /// <returns>Die Block-ID an der Position oder 0, wenn der Block nicht existiert.</returns>
    public ushort GetBlockAt(int x, int y, int z)
    {
        return _chunkProvider.GetBlockAt(x, y, z);
    }

    /// <inheritdoc/>
    bool IWorld.ModifyBlock(int x, int y, int z, ushort blockId)
    {
        return SetBlock(x, y, z, blockId);
    }

    /// <inheritdoc/>
    ushort IWorld.GetBlock(int x, int y, int z)
    {
        return GetBlockAt(x, y, z);
    }

    // =========================================================================
    // --- Statische Fassade für Gameplay-Skripte & Ergonomie ---
    // =========================================================================

    private static World? _activeInstance;

    /// <summary>
    /// Gibt die aktuelle Instanz der Welt zurück.
    /// Nutzt den <see cref="EngineContext.Current"/> oder die statische Fallback-Instanz.
    /// </summary>
    /// <exception cref="InvalidOperationException">Wird ausgelöst, wenn die Welt nicht initialisiert wurde.</exception>
    public static World Current =>
        EngineContext.Current?.World ?? _activeInstance ?? throw new InvalidOperationException("World wurde nicht initialisiert!");

    /// <summary>
    /// Initialisiert die statische Fallback-Instanz der Welt.
    /// </summary>
    /// <param name="provider">Der für die Initialisierung zu verwendende <see cref="ChunkProvider"/>.</param>
    public static void Initialize(ChunkProvider provider)
    {
        _activeInstance = new World(provider);
    }

    /// <summary>
    /// Legt die aktive Instanz der Welt fest.
    /// </summary>
    /// <param name="world">Die festzulegende Instanz von <see cref="World"/>.</param>
    public static void SetActive(World world)
    {
        _activeInstance = world;
    }

    /// <summary>
    /// Ändert einen Block mithilfe der aktuellen Instanz.
    /// </summary>
    /// <param name="x">Die X-Koordinate in Weltkoordinaten.</param>
    /// <param name="y">Die Y-Koordinate in Weltkoordinaten.</param>
    /// <param name="z">Die Z-Koordinate in Weltkoordinaten.</param>
    /// <param name="blockId">Die neue Block-ID.</param>
    public static void ModifyBlock(int x, int y, int z, ushort blockId)
    {
        Current.SetBlock(x, y, z, blockId);
    }

    /// <summary>
    /// Gibt die Block-ID an der angegebenen Position mithilfe der aktuellen Instanz zurück.
    /// </summary>
    /// <param name="x">Die X-Koordinate in Weltkoordinaten.</param>
    /// <param name="y">Die Y-Koordinate in Weltkoordinaten.</param>
    /// <param name="z">Die Z-Koordinate in Weltkoordinaten.</param>
    /// <returns>Die Block-ID an der Position oder 0, wenn der Block nicht existiert.</returns>
    public static ushort GetBlock(int x, int y, int z)
    {
        return Current.GetBlockAt(x, y, z);
    }
}