using System;
using System.Collections.Generic;
using Basics.Game;
using Basics.Game.Configurations;
using Basics.Game.texture;
using Silk.NET.OpenGL;

namespace Basics.Configurations;

/// <summary>
/// Statische Fassade für Block- und Texturabfragen.
/// Delegiert alle Operationen an die aktive BlockRegistry des aktuellen EngineContexts
/// oder erstellt eine Standardinstanz, wenn kein Context verfügbar ist.
/// </summary>
public static class BlockLoader
{
    private static BlockRegistry? _defaultRegistry;

    /// <summary>
    /// Gibt die aktuelle BlockRegistry zurück.
    /// Wenn kein EngineContext vorhanden ist, wird eine Standardinstanz erstellt.
    /// </summary>
    public static BlockRegistry Current =>
        EngineContext.Current?.BlockRegistry ?? (_defaultRegistry ??= CreateDefaultRegistry());

    /// <summary>
    /// Erstellt eine neue BlockRegistry mit Headless-Initialisierung als Fallback.
    /// </summary>
    private static BlockRegistry CreateDefaultRegistry()
    {
        var registry = new BlockRegistry();
        if (System.IO.Directory.Exists(BlockRegistry.ResolveContentRoot("Content")))
        {
            try
            {
                registry.InitializeHeadless("Content");
            }
            catch
            {
                // Headless Fallback falls Content unvollständig ist
            }
        }
        return registry;
    }

    /// <summary>
    /// Index des oberen Face (Top) in der Layer-Array.
    /// </summary>
    public const int Top = BlockRegistry.Top;
    
    /// <summary>
    /// Index des unteren Face (Bottom) in der Layer-Array.
    /// </summary>
    public const int Bottom = BlockRegistry.Bottom;
    
    /// <summary>
    /// Index des vorderen Face (Front) in der Layer-Array.
    /// </summary>
    public const int Front = BlockRegistry.Front;
    
    /// <summary>
    /// Index des hinteren Face (Back) in der Layer-Array.
    /// </summary>
    public const int Back = BlockRegistry.Back;
    
    /// <summary>
    /// Index des linken Face (Left) in der Layer-Array.
    /// </summary>
    public const int Left = BlockRegistry.Left;
    
    /// <summary>
    /// Index des rechten Face (Right) in der Layer-Array.
    /// </summary>
    public const int Right = BlockRegistry.Right;

    /// <summary>
    /// Die Block-ID für Luft (Air).
    /// </summary>
    public static ushort Air => 0;

    /// <summary>
    /// Gibt die Terrain-Textur zurück, die alle Texturen als Texture-Array enthält.
    /// </summary>
    public static TextureArray TerrainTexture => Current.TerrainTexture;

    /// <summary>
    /// Initialisiert die BlockRegistry mit Texturen und Blockdefinitionen.
    /// </summary>
    /// <param name="gl">Die OpenGL-Instanz für die Textur-Erstellung oder null für headless-Betrieb.</param>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
    public static void Initialize(GL? gl, string contentRoot = "Content")
    {
        Current.Initialize(gl, contentRoot);
    }

    /// <summary>
    /// Initialisiert die BlockRegistry ohne GPU-Texturen (für headless-Betrieb).
    /// </summary>
    /// <param name="contentRoot">Der Pfad zum Content-Verzeichnis.</param>
    public static void InitializeHeadless(string contentRoot = "Content")
    {
        Current.InitializeHeadless(contentRoot);
    }

    /// <summary>
    /// Setzt die BlockRegistry auf den Ausgangszustand zurück.
    /// </summary>
    public static void Reset()
    {
        Current.Reset();
    }

    /// <summary>
    /// Gibt die Texture-ID (Layer-Index) für einen Block und ein Face zurück.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="faceIndex">Der Index des Faces (0-5).</param>
    /// <returns>Der Layer-Index der Textur für dieses Face.</returns>
    public static ushort Get(int blockId, int faceIndex)
    {
        return Current.Get(blockId, faceIndex);
    }

    /// <summary>
    /// Gibt die ID eines Blocks anhand seines Namens zurück.
    /// </summary>
    /// <param name="blockName">Der Name des Blocks (z. B. "core:stone").</param>
    /// <returns>Die ID des Blocks.</returns>
    /// <exception cref="KeyNotFoundException">Wird ausgelöst, wenn der Block nicht gefunden wird.</exception>
    public static ushort GetBlockId(string blockName)
    {
        return Current.GetBlockId(blockName);
    }

    /// <summary>
    /// Prüft, ob ein Block den angegebenen Tag hat.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="tag">Der Tag, der überprüft werden soll.</param>
    /// <returns><c>true</c>, wenn der Block den Tag hat; andernfalls <c>false</c>.</returns>
    public static bool HasTag(ushort blockId, string tag)
    {
        return Current.HasTag(blockId, tag);
    }

    /// <summary>
    /// Gibt alle Block-IDs zurück, die den angegebenen Tag haben.
    /// </summary>
    /// <param name="tag">Der Tag, nach dem gesucht werden soll.</param>
    /// <returns>Ein schreibgeschütztes Kollektion mit Block-IDs.</returns>
    public static IReadOnlyCollection<ushort> GetByTag(string tag)
    {
        return Current.GetByTag(tag);
    }

    /// <summary>
    /// Gibt den Namespaced-Namen eines Blocks anhand seiner ID zurück (Palette-Rücklookup).
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <returns>Der Namespaced-Name des Blocks (z. B. "core:stone").</returns>
    /// <exception cref="ArgumentOutOfRangeException">Wird ausgelöst, wenn die Block-ID nicht existiert.</exception>
    public static string GetName(ushort blockId)
    {
        return Current.GetName(blockId);
    }

    /// <summary>
    /// Versucht, den Namespaced-Namen eines Blocks anhand seiner ID zu ermitteln.
    /// </summary>
    /// <param name="blockId">Die ID des Blocks.</param>
    /// <param name="name">Der gefundene Name oder null.</param>
    /// <returns><c>true</c>, wenn die ID existiert, andernfalls <c>false</c>.</returns>
    public static bool TryGetName(ushort blockId, out string? name)
    {
        return Current.TryGetName(blockId, out name);
    }

    /// <summary>
    /// Exportiert die aktuelle Palette (ID → Namespaced-Name) als Snapshot.
    /// Für Serialisierung: Zusammen mit den Blockdaten speichern.
    /// </summary>
    /// <returns>Ein Array, in dem der Index die Block-ID und der Wert der Namespaced-Name ist.</returns>
    public static string[] ExportPalette()
    {
        return Current.ExportPalette();
    }

    /// <summary>
    /// Erstellt eine Remap-Tabelle, die Block-IDs einer gespeicherten Palette auf die
    /// IDs der aktuellen Registry abbildet. Unbekannte Namen werden auf Luft (0) abgebildet.
    /// </summary>
    /// <param name="previousPalette">Die gespeicherte Palette (Index = alte ID, Wert = Name).</param>
    /// <returns>Die Remap-Tabelle für <see cref="RemapBlockArray"/>.</returns>
    public static int[] CreateRemapTable(IReadOnlyList<string> previousPalette)
    {
        return Current.CreateRemapTable(previousPalette);
    }

    /// <summary>
    /// Mappt ein Block-ID-Array (z. B. <see cref="ChunkData.Blocks"/>) mit der Remap-Tabelle
    /// auf die IDs der aktuellen Registry. Arbeitet in-place ohne Allokation.
    /// </summary>
    /// <param name="blocks">Das Block-ID-Array, das in-place remappt wird.</param>
    /// <param name="remapTable">Die Remap-Tabelle von <see cref="CreateRemapTable"/>.</param>
    public static void RemapBlockArray(ushort[] blocks, int[] remapTable)
    {
        BlockRegistry.RemapBlockArray(blocks, remapTable);
    }
}