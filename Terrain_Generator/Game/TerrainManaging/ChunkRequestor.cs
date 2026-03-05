using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
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
    private int _renderDistance = 30; // Overall distanz wo Chunks angefragt werden
    private int _lod1Distance = 15; // Ab wann Lod1 angefragt wird
    private int _lod2Distance = 20; // Ab wann Lod2 angefragt wird
    private int _verticalRenderDistance = 10; // Vertikale Render-Distanz
    private HashSet<ChunkCoord> _activeChunks = new();
    private readonly object _chunkLock = new();

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
        Task.Run(() =>
        {
            
            HashSet<ChunkCoord> newActiveChunks = new();
            List<ChunkCoord> chunksToLoad = new();
            for (int x = -_renderDistance; x <= _renderDistance; x++)
            {
                for (int z = -_renderDistance; z <= _renderDistance; z++)
                {
                    if (x * x + z * z > _renderDistance * _renderDistance) continue;
                    for (int y = -_verticalRenderDistance; y <= _verticalRenderDistance; y++)
                    {
                        ChunkCoord coord = new ChunkCoord(playerChunk.X + x, playerChunk.Y + y, playerChunk.Z + z, 0);
                        chunksToLoad.Add(coord);
                        newActiveChunks.Add(coord);
                    }
                }
            }
            // Dieses Parallel.For blockiert jetzt nur diesen Task, nicht das ganze Spiel!
            // Es kann sich nun alle freien Kerne der CPU schnappen.
            Parallel.For(0, chunksToLoad.Count, _parallelOptions, i =>
            {
                _chunkProvider.RequestChunk(chunksToLoad[i]);
            });
            
            // Thread-safe austauschen
            lock (_chunkLock)
            {
                // Nein ich entlade nicht die neuen Chunks, der name ist nur blöd
                UnloadDistantChunks(newActiveChunks);
                _activeChunks = newActiveChunks;
            }
        });
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