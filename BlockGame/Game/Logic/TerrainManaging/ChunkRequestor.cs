using Basics.Game.Logic.Player;
using Basics.Game.Player;
using Basics.Game.Utilities;

namespace Basics.Game.Logic.TerrainManaging;

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

    private ChunkCoord _lastPlayerChunk;
    private bool _isUpdating = false;

    public int RenderDistance
    {
        get => _renderDistance;
        set => _renderDistance = Math.Max(1, value);
    }

    public ChunkRequestor(PlayerCharacter player, ChunkProvider chunkProvider, int generationCores)
    {
        _player = player;
        _chunkProvider = chunkProvider;
        _parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, generationCores) };

        // Event abonnieren: wird gefeuert, wenn der Spieler einen neuen Chunk betritt
        _player.OnChunkChanged += OnPlayerChunkChanged;
    }

    /// <summary>
    /// Event-Handler: Wird aufgerufen, wenn der Spieler in einen neuen Chunk wechselt.
    /// Berechnet welche Chunks im Render-Radius liegen und fordert sie an.
    /// TODO: die methode auf maximal eine instanz? limitieren damit kein update gefragt wird während eins schon läuft
    /// </summary>
    private void OnPlayerChunkChanged(ChunkCoord playerChunk)
    {
        lock (_chunkLock)
        {
            _lastPlayerChunk = playerChunk;
            if (_isUpdating) return;
            _isUpdating = true;
        }

        Task.Run(() =>
        {
            ProcessChunkUpdates();
        });
    }

    private void ProcessChunkUpdates()
    {
        while (true)
        {
            ChunkCoord targetChunk;
            lock (_chunkLock)
            {
                targetChunk = _lastPlayerChunk;
            }

            HashSet<ChunkCoord> newActiveChunks = new(GameSettings.RenderDistance * GameSettings.RenderDistance * GameSettings.VerticalRenderDistance * 8);

            for (int x = -GameSettings.RenderDistance; x <= GameSettings.RenderDistance; x++)
            {
                for (int z = -GameSettings.RenderDistance; z <= GameSettings.RenderDistance; z++)
                {
                    if (x * x + z * z > GameSettings.RenderDistance * GameSettings.RenderDistance) continue;
                    for (int y = -GameSettings.VerticalRenderDistance; y <= GameSettings.VerticalRenderDistance; y++)
                    {
                        ChunkCoord coord = new ChunkCoord(targetChunk.X + x, targetChunk.Y + y, targetChunk.Z + z, 0);
                        newActiveChunks.Add(coord);
                    }
                }
            }

            HashSet<ChunkCoord> pendingRequests = new();
            lock (_chunkLock)
            {
                foreach (var chunk in newActiveChunks)
                {
                    if (!_activeChunks.Contains(chunk))
                    {
                        pendingRequests.Add(chunk);
                    }
                }
            }

            Parallel.ForEach(pendingRequests, _parallelOptions, chunk =>
            {
                _chunkProvider.RequestChunk(chunk);
            });

            lock (_chunkLock)
            {
                if (_lastPlayerChunk != targetChunk)
                {
                    continue;
                }

                UnloadDistantChunks(newActiveChunks);
                _activeChunks = newActiveChunks;
                _isUpdating = false;
                break;
            }
        }
    }

    /// <summary>
    /// Entlädt Chunks, die nicht mehr im aktiven Set sind.
    /// </summary>
    private void UnloadDistantChunks(HashSet<ChunkCoord> newActiveChunks)
    {
        // Alle Chunks finden, die vorher aktiv waren aber jetzt nicht mehr
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
            foreach (var chunk in ChunkProvider.LoadedChunks.Keys.ToList())
            {
                _chunkProvider.UnloadChunk(chunk);
            }
            foreach (var chunk in ChunkProvider.Chunkdata.Keys.ToList())
            {
                _chunkProvider.UnloadChunk(chunk);
            }
            _activeChunks.Clear();
        }
    }
}