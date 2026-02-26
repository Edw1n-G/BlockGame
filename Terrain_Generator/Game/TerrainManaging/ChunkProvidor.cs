using Basics.Utilities;
using Basics.Graphics;

namespace Basics.Game;

/// <summary>
/// Verwaltet den Chunk-Lebenszyklus: Laden von Disk (Placeholder), Generieren, Speichern.
/// Zentraler Speicher für alle geladenen Chunks.
/// </summary>
public class ChunkProvidor
{
    private readonly Dictionary<ChunkCoord, ChunkMesher> _loadedChunks = new();
    private readonly TerrainGenerator _terrainGenerator;

    public ChunkProvidor(TerrainGenerator terrainGenerator)
    {
        _terrainGenerator = terrainGenerator;
    }

    /// <summary>
    /// Fordert einen Chunk an. Prüft zuerst ob er schon geladen ist,
    /// dann ob er von der Festplatte geladen werden kann,
    /// und generiert ihn ansonsten neu.
    /// </summary>
    public void RequestChunk(ChunkCoord coord)
    {
        // Bereits geladen? → nichts tun
        if (_loadedChunks.ContainsKey(coord))
            return;

        // Versuch von Festplatte zu laden (Placeholder)
        if (TryLoadFromDisk(coord, out ChunkMesher? loadedChunk))
        {
            _loadedChunks[coord] = loadedChunk!;
            return;
        }

        // Neu generieren
        ChunkMesher newChunk = _terrainGenerator.GenerateChunk(coord);
        _loadedChunks[coord] = newChunk;
    }

    /// <summary>
    /// Entfernt einen Chunk aus dem Speicher und gibt seine Ressourcen frei.
    /// </summary>
    public void UnloadChunk(ChunkCoord coord)
    {
        if (_loadedChunks.TryGetValue(coord, out ChunkMesher? chunk))
        {
            // TODO: Chunk auf Festplatte speichern bevor er entladen wird
            // SaveToDisk(coord, chunk);

            chunk.Dispose();
            _loadedChunks.Remove(coord);
        }
    }

    /// <summary>
    /// Gibt alle aktuell geladenen Chunks zurück (für den Renderer).
    /// </summary>
    public IEnumerable<ChunkMesher> GetLoadedChunks()
    {
        return _loadedChunks.Values;
    }

    /// <summary>
    /// Prüft ob ein Chunk bereits geladen ist.
    /// </summary>
    public bool IsChunkLoaded(ChunkCoord coord)
    {
        return _loadedChunks.ContainsKey(coord);
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
        foreach (var chunk in _loadedChunks.Values)
        {
            chunk.Dispose();
        }
        _loadedChunks.Clear();
    }
}