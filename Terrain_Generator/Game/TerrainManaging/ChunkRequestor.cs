using System.Numerics;
using Basics.Game.TerrainManaging;
using Basics.Graphics;
using Basics.Utilities;
using Silk.NET.OpenGL;
//=============================================
// THIS CODE IS BASED ON AI - USE WITH CAUTION
//=============================================
// Modified to use parallel chunk generation
namespace Basics.Game;

/// <summary>
/// Berechnet basierend auf der Spielerposition welche Chunks geladen/entladen werden sollen.
/// Abonniert das OnChunkChanged Event der Kamera.
/// </summary>
public class ChunkRequestor
{
    private readonly ChunkProvider _chunkProvider;
    private readonly Camera _camera;
    private readonly ParallelOptions _parallelOptions;
    private int _renderDistance = 10; // Render-Distanz in Chunks
    private HashSet<ChunkCoord> _activeChunks = new();
    private List<ChunkCoord> _chunksToLoad = new();

    public int RenderDistance
    {
        get => _renderDistance;
        set => _renderDistance = Math.Max(1, value);
    }

    public ChunkRequestor(Camera camera, ChunkProvider chunkProvider, int availableCores)
    {
        _camera = camera;
        _chunkProvider = chunkProvider;
        _parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, availableCores) };

        // Event abonnieren: wird gefeuert, wenn der Spieler einen neuen Chunk betritt
        _camera.OnChunkChanged += OnPlayerChunkChanged;
    }

    /// <summary>
    /// Event-Handler: Wird aufgerufen, wenn der Spieler in einen neuen Chunk wechselt.
    /// Berechnet welche Chunks im Render-Radius liegen und fordert sie an.
    /// </summary>
    private void OnPlayerChunkChanged(ChunkCoord playerChunk)
    {
        HashSet<ChunkCoord> newActiveChunks = new();
        _chunksToLoad = new List<ChunkCoord>();

        // Alle Chunks im Render-Radius berechnen (kreisförmig auf der XZ-Ebene)
        for (int x = -_renderDistance; x <= _renderDistance; x++)
        {
            for (int z = -_renderDistance; z <= _renderDistance; z++)
            {
                // Kreisförmige Distanzprüfung statt quadratisch
                if (x * x + z * z > _renderDistance * _renderDistance)
                    continue;
                
                ChunkCoord coord = new ChunkCoord(playerChunk.X + x, 0, playerChunk.Z + z);
                _chunksToLoad.Add(coord);
            }
        }
        
        // Chunks parallel generieren lassen
        try
        {
            Parallel.For(0, _chunksToLoad.Count,_parallelOptions, i =>
            {
                ChunkCoord coord = _chunksToLoad[i];
                // Chunk anfordern (ChunkProvider prüft ob er schon geladen ist)
                _chunkProvider.RequestChunk(coord);
            });

            // Aktive Chunks sammeln (nach der parallelen Generierung)
            foreach (ChunkCoord coord in _chunksToLoad)
            {
                newActiveChunks.Add(coord);
            }

            // Chunks entladen die außerhalb des Render-Radius liegen
            UnloadDistantChunks(newActiveChunks);

            _activeChunks = newActiveChunks;
        }
        catch (AggregateException ex)
        {
            Console.WriteLine("Fehler bei der parallelen Chunk-Generierung: " + ex.Message);
            // Fallback: Chunks nacheinander generieren
            foreach (ChunkCoord coord in _chunksToLoad)
            {
                _chunkProvider.RequestChunk(coord);
                newActiveChunks.Add(coord);
            }
            
            UnloadDistantChunks(newActiveChunks);
            _activeChunks = newActiveChunks;
        }
    }

    /// <summary>
    /// Entlädt Chunks die nicht mehr im aktiven Set sind.
    /// </summary>
    private void UnloadDistantChunks(HashSet<ChunkCoord> newActiveChunks)
    {
        // Alle Chunks finden die vorher aktiv waren aber jetzt nicht mehr
        foreach (ChunkCoord oldChunk in _activeChunks)
        {
            if (!newActiveChunks.Contains(oldChunk))
            {
                // TODO: Hier später auch Chunk-Daten auf Festplatte speichern
                _chunkProvider.UnloadChunk(oldChunk);
            }
        }
    }
}