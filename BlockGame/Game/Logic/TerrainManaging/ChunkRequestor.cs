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
    /// <summary>
    /// Der ChunkProvider für Chunk-Operationen.
    /// </summary>
    private readonly ChunkProvider _chunkProvider;
    
    /// <summary>
    /// Der Spieler-Charakter.
    /// </summary>
    private readonly PlayerCharacter _player;
    
    /// <summary>
    /// Globale Spiel-Einstellungen.
    /// </summary>
    private readonly GameSettings _settings;
    
    /// <summary>
    /// Render-Entfernung in Chunks.
    /// </summary>
    private int _renderDistance;
    
    /// <summary>
    /// Entfernung für Level-of-Detail Level 1.
    /// </summary>
    private int _lod1Distance;
    
    /// <summary>
    /// Entfernung für Level-of-Detail Level 2.
    /// </summary>
    private int _lod2Distance;
    
    /// <summary>
    /// Vertikale Render-Entfernung in Chunks.
    /// </summary>
    private int _verticalRenderDistance;
    
    /// <summary>
    /// Menge aller aktiven Chunks.
    /// </summary>
    private HashSet<ChunkCoord> _activeChunks = new();
    
    /// <summary>
    /// Lock-Objekt für Thread-Synchronisation.
    /// </summary>
    private readonly object _chunkLock = new();

    private ChunkCoord _lastPlayerChunk;
    private bool _isUpdating = false;

    /// <summary>
    /// Initialisiert einen neuen ChunkRequestor.
    /// </summary>
    /// <param name="player">Der Spieler-Charakter.</param>
    /// <param name="chunkProvider">Der ChunkProvider für Chunk-Operationen.</param>
    /// <param name="settings">Optionale Spiel-Einstellungen. Falls nicht angegeben, werden Standardwerte verwendet.</param>
    public ChunkRequestor(PlayerCharacter player, ChunkProvider chunkProvider, GameSettings? settings = null)
    {
        _player = player;
        _chunkProvider = chunkProvider;
        _settings = settings ?? chunkProvider.Settings;
        _renderDistance = _settings.RenderDistance;
        _lod1Distance = _settings.Lod1Distance;
        _lod2Distance = _settings.Lod2Distance;
        _verticalRenderDistance = _settings.VerticalRenderDistance;

        // Event abonnieren: wird gefeuert, wenn der Spieler einen neuen Chunk betritt
        _player.OnChunkChanged += OnPlayerChunkChanged;
    }

    /// <summary>
    /// Event-Handler: Wird aufgerufen, wenn der Spieler in einen neuen Chunk wechselt.
    /// Berechnet welche Chunks im Render-Radius liegen und fordert sie an.
    /// </summary>
    /// <param name="playerChunk">Die Chunk-Koordinaten, in die der Spieler gewechselt ist.</param>
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
            
            //TODO: dreifache for loop ersetzen durch z.B offsets oder nur ränder neu berechnen
            
            int renderDistance = _settings.RenderDistance;
            int verticalRenderDistance = _settings.VerticalRenderDistance;
            float renderDistanceSq = renderDistance * renderDistance;
            float verticalRenderDistanceSq = verticalRenderDistance * verticalRenderDistance;

            HashSet<ChunkCoord> newActiveChunks = new(renderDistance * renderDistance * verticalRenderDistance * 8);

            for (int x = -renderDistance; x <= renderDistance; x++)
            {
                for (int z = -renderDistance; z <= renderDistance; z++)
                {
                    for (int y = -verticalRenderDistance; y <= verticalRenderDistance; y++)
                    {
                        float distanceFactor = (x * x + z * z) / renderDistanceSq + (y * y) / verticalRenderDistanceSq;
                        if (distanceFactor > 1f) continue;

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

            foreach (var chunk in pendingRequests)
            {
                _chunkProvider.RequestChunk(chunk);
            }

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
    /// <param name="newActiveChunks">Die Menge der neuen aktiven Chunks.</param>
    private void UnloadDistantChunks(HashSet<ChunkCoord> newActiveChunks)
    {
        // Alle Chunks finden, die vorher aktiv waren aber jetzt nicht mehr
        foreach (ChunkCoord oldChunk in _activeChunks)
        {
            if (!newActiveChunks.Contains(oldChunk))
            {
                _chunkProvider.UnloadChunk(oldChunk);
            }
        }
    }
    
    /// <summary>
    /// Entlädt alle aktuell geladenen Chunks und leert die aktiven Chunks.
    /// </summary>
    public void UnloadAllChunks()
    {
        lock (_chunkLock)
        {
            foreach (var chunk in _chunkProvider.LoadedChunks.Keys.ToList())
            {
                _chunkProvider.UnloadChunk(chunk);
            }
            foreach (var chunk in _chunkProvider.Chunkdata.Keys.ToList())
            {
                _chunkProvider.UnloadChunk(chunk);
            }
            _activeChunks.Clear();
        }
    }
}