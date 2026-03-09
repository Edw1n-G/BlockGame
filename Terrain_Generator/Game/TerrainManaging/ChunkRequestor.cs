using Basics.Game.Player;
using Basics.Utilities;

namespace Basics.Game.TerrainManaging;

/// <summary>
/// Berechnet basierend auf der Spielerposition welche Chunks geladen/entladen werden sollen.
/// Abonniert das OnChunkChanged Event des Players.
/// </summary>
public class ChunkRequestor
{
    private readonly ChunkProvider _chunkProvider;
    private readonly PlayerCharacter _player;
    private readonly ParallelOptions _parallelOptions;
    private int _renderDistance = GameSettings.RenderDistance;
    private int _lod1Distance = GameSettings.Lod1Distance;
    private int _lod2Distance = GameSettings.Lod2Distance;
    private int _verticalRenderDistance = GameSettings.VerticalRenderDistance;
    private HashSet<ChunkCoord> _activeChunks = new();
    private readonly object _chunkLock = new();

    public int RenderDistance
    {
        get => _renderDistance;
        set => _renderDistance = Math.Max(1, value);
    }

    public ChunkRequestor(PlayerCharacter player, ChunkProvider chunkProvider, int availableCores)
    {
        _player = player;
        _chunkProvider = chunkProvider;
        _parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, availableCores) };

        // Event abonnieren: wird gefeuert, wenn der Spieler einen neuen Chunk betritt
        _player.OnChunkChanged += OnPlayerChunkChanged;
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
    
    public void UnloadAllChunks()
    {
        lock (_chunkLock)
        {
            foreach (ChunkCoord chunk in _activeChunks)
            {
                _chunkProvider.UnloadChunk(chunk);
            }
            _activeChunks.Clear();
        }
    }
    
}