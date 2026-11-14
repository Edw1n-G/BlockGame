using Basics.Common;
using Basics.Player;
using Basics.Settings;
using Basics.Terrain.Chunks;

namespace Basics.Terrain.Selection;

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
    /// Menge aller aktiven Chunks.
    /// </summary>
    private readonly HashSet<ChunkCoord> _activeChunks = new();
    private readonly ChunkSelectionPlan _selectionPlan = new();
    private ChunkSelectionRegion? _selectedRegion;
    
    /// <summary>
    /// Lock-Objekt für Thread-Synchronisation.
    /// </summary>
    private readonly object _chunkLock = new();

    private ChunkCoord _lastPlayerChunk;
    private bool _isUpdating = false;
    public bool IsUpdating { get { lock (_chunkLock) return _isUpdating; } }
    private volatile bool _stopping;
    private Task? _updateTask;
    private int _resetVersion;
    private int _updateVersion;
    public Exception? LastUpdateFailure { get; private set; }

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
            if (_stopping) return;
            _lastPlayerChunk = playerChunk;
            _updateVersion++;
            if (_isUpdating) return;
            _isUpdating = true;
            _updateTask = Task.Run(ProcessChunkUpdates);
        }
    }

    private void ProcessChunkUpdates()
    {
        try
        {
            while (true)
            {
                ChunkCoord targetChunk;
                int resetVersion, updateVersion, horizontal, vertical;
                ChunkSelectionRegion? previous;
                lock (_chunkLock)
                {
                    if (_stopping) { _isUpdating = false; return; }
                    targetChunk = _lastPlayerChunk;
                    resetVersion = _resetVersion; updateVersion = _updateVersion;
                    horizontal = _settings.RenderDistance; vertical = _settings.VerticalRenderDistance;
                    previous = _selectedRegion;
                }
                bool Superseded() => _stopping || Volatile.Read(ref _updateVersion) != updateVersion || Volatile.Read(ref _resetVersion) != resetVersion;
                bool completed = false;
                int applied = 0;
                try
                {
                    if (_selectionPlan.TryBuild(previous,targetChunk,horizontal,vertical,Superseded))
                    {
                        foreach (var coord in _selectionPlan.Added)
                        {
                            if (Superseded()) break;
                            // Never hold the requestor lock across a potentially blocking provider call.
                            try { _chunkProvider.RequestChunk(coord); }
                            catch { _chunkProvider.UnloadChunk(coord); throw; }
                            lock (_chunkLock)
                            {
                                if (Superseded()) { _chunkProvider.UnloadChunk(coord); break; }
                                _activeChunks.Add(coord); applied++;
                            }
                        }
                        lock (_chunkLock)
                        {
                            if (!Superseded() && applied == _selectionPlan.Added.Count)
                            {
                                foreach (var coord in _selectionPlan.Removed)
                                {
                                    _chunkProvider.UnloadChunk(coord); _activeChunks.Remove(coord);
                                }
                                _selectedRegion = _selectionPlan.Target;
                                _chunkProvider.Metrics.SelectionCommitted();
                                completed = true; _isUpdating = false;
                            }
                        }
                    }
                }
                finally
                {
                    if (!completed)
                    {
                        _chunkProvider.Metrics.SelectionRolledBack();
                        // Keep the last committed region valid. Undo only this canceled plan's additions.
                        lock (_chunkLock)
                            for (int i = 0; i < applied; i++)
                                if (_activeChunks.Remove(_selectionPlan.Added[i])) _chunkProvider.UnloadChunk(_selectionPlan.Added[i]);
                    }
                }
                if (completed) return;
                lock (_chunkLock)
                {
                    // An explicit unload stays empty until a newer player/settings notification arrives.
                    if (_stopping || resetVersion != _resetVersion && updateVersion == _updateVersion)
                    {
                        _isUpdating = false; return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lock (_chunkLock) { LastUpdateFailure = ex; _isUpdating = false; }
            throw;
        }
    }

    /// <summary>Stops accepting updates and waits for the current provider user to exit.</summary>
    public bool Stop(TimeSpan? timeout = null)
    {
        Task? task;
        lock (_chunkLock)
        {
            _stopping = true;
            _player.OnChunkChanged -= OnPlayerChunkChanged;
            task = _updateTask;
        }
        try
        {
            if (task is null || task.Wait(timeout ?? TimeSpan.FromSeconds(10))) return true;
            Console.Error.WriteLine("[ChunkRequestor] Timed out; provider resources remain alive.");
            return false;
        }
        catch (AggregateException ex)
        {
            LastUpdateFailure = ex.Flatten();
            Console.Error.WriteLine($"[ChunkRequestor] Update failed: {LastUpdateFailure}");
            return true; // The faulted task has exited and no longer uses the provider.
        }
    }

    /// <summary>
    /// Entlädt alle aktuell geladenen Chunks und leert die aktiven Chunks.
    /// </summary>
    public void UnloadAllChunks()
    {
        lock (_chunkLock)
        {
            _resetVersion++;
            _selectedRegion = null;
            foreach (var chunk in _activeChunks)
                _chunkProvider.UnloadChunk(chunk);
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
