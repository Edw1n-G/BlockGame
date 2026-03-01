using System.Collections.Concurrent;
using Basics.Utilities;


//=============================================
// THIS CODE IS BASED ON AI - USE WITH CAUTION
//=============================================
namespace Basics.Game.TerrainManaging;

/// <summary>
/// Verwaltet den Chunk-Lebenszyklus: Laden von Disk (Placeholder), Generieren, Speichern.
/// Zentraler Speicher für alle geladenen Chunks.
/// </summary>
public class ChunkProvider
{
    public readonly Dictionary<ChunkCoord, ChunkMesher> LoadedChunks = new();
    public ConcurrentQueue<ChunkMesher> UploadQueue = new();
    private readonly TerrainGenerator _terrainGenerator;

    public ChunkProvider(TerrainGenerator terrainGenerator)
    {
        _terrainGenerator = terrainGenerator;
    }

    /// <summary>
    /// Fordert einen Chunk an. Prüft zuerst, ob er schon geladen ist,
    /// dann ob er von der Festplatte geladen werden kann,
    /// und generiert ihn ansonsten neu.
    /// Ein Check um zu gucken, ob der Chunk von einem anderen Thread geladen wird fehlt
    /// </summary>
    public void RequestChunk(ChunkCoord coord)
    {
        // Bereits geladen? → nichts tun
        if (LoadedChunks.ContainsKey(coord))
            return;

        // Versuch von Festplatte zu laden (Placeholder)
        if (TryLoadFromDisk(coord, out ChunkMesher? loadedChunk))
        {
            LoadedChunks.TryAdd(coord, loadedChunk!);
            return;
        }

        // Neu generieren
        ChunkMesher newChunk = _terrainGenerator.GenerateChunk(coord);
        UploadQueue.Enqueue(newChunk);
    }

    /// <summary>
    /// Entfernt einen Chunk aus dem Speicher und gibt seine Ressourcen frei.
    /// </summary>
    public void UnloadChunk(ChunkCoord coord)
    {
        if (LoadedChunks.Remove(coord, out ChunkMesher? chunk))
        {
            // TODO: Chunk auf Festplatte speichern bevor er entladen wird
            // SaveToDisk(coord, chunk);

            chunk.Dispose();
        }
    }

    /// <summary>
    /// Gibt alle aktuell geladenen Chunks zurück (für den Renderer).
    /// </summary>
    public IEnumerable<ChunkMesher> GetLoadedChunks()
    {
        return LoadedChunks.Values;
    }

    /// <summary>
    /// Prüft ob ein Chunk bereits geladen ist.
    /// </summary>
    public bool IsChunkLoaded(ChunkCoord coord)
    {
        return LoadedChunks.ContainsKey(coord);
    }

    /// <summary>
    /// Placeholder: Versucht einen Chunk von der Festplatte zu laden.
    /// Gibt vorerst immer false zurück.
    /// </summary>
    private bool TryLoadFromDisk(ChunkCoord coord, out ChunkMesher? chunk)
    {
        // TODO: Implementierung für Chunk-Laden von der Festplatte
        // z.B. aus einer Datei wie "chunks/{coord.X}_{coord.Y}_{coord.Z}.chunk"
        chunk = null;
        return false;
    }

    /// <summary>
    /// Gibt alle Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        foreach (ChunkMesher chunk in LoadedChunks.Values)
        {
            chunk.Dispose();
        }
        LoadedChunks.Clear();
    }
}