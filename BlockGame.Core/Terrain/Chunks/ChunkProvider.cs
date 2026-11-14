using Basics.Common;
using Basics.Engine;
using Basics.Jobs;
using Basics.Terrain.Meshing;
using Basics.Settings;
using System.Collections.Concurrent;

namespace Basics.Terrain.Chunks;

public readonly record struct MeshRequest(ChunkCoord Coord, long Generation);

/// <summary>
/// Verwaltet den Chunk-Lebenszyklus: Laden von Disk (Placeholder), Generieren, Speichern.
/// Zentraler Speicher für alle geladenen Chunks einer Engine-Instanz.
/// </summary>
public class ChunkProvider : IDisposable
{
    public Basics.Diagnostics.ChunkPipelineMetrics Metrics { get; } = new();
    /// <summary>
    /// Enthält alle Chunk-Daten für geladene Chunks.
    /// </summary>
    public ConcurrentDictionary<ChunkCoord, ChunkData> Chunkdata => _lifetime.Data;
    
    /// <summary>
    /// Chunks, die für das Meshing in die Warteschlange gestellt wurden.
    /// </summary>
    public ConcurrentDictionary<ChunkCoord, long> QueuedForMeshing => _meshRequests.Queued;
    private readonly MeshRequestCoordinator _meshRequests = new();
    private readonly ChunkBlockAccess _blockAccess;
    private readonly ChunkLifetimeStore _lifetime = new();
    
    /// <summary>
    /// Pool für Mesh-Puffer (VBO, EBO, VAO) für das Speichermanagement.
    /// </summary>
    public ConcurrentQueue<PooledMeshBuffer> VramPool => _resources.VramPool;
    
    /// <summary>
    /// Pool für Vertex-Listen zur Vermeidung von GC-Allocations.
    /// </summary>
    public ConcurrentQueue<List<byte>> VertexListPool => _resources.VertexListPool;
    
    /// <summary>
    /// Pool für Index-Listen zur Vermeidung von GC-Allocations.
    /// </summary>
    public ConcurrentQueue<List<uint>> IndexListPool => _resources.IndexListPool;
    
    /// <summary>
    /// Anfragen für Meshing, die noch bearbeitet werden müssen.
    /// </summary>
    public ConcurrentQueue<MeshRequest> PendingMeshRequests => _meshRequests.Pending;

    /// <summary>
    /// Meshes, die für den Upload zur GPU bereit sind.
    /// </summary>
    public ConcurrentQueue<BaseMesher> UploadQueue => _resources.UploadQueue;
    
    /// <summary>
    /// Alle aktuell auf der GPU geladenen Chunks.
    /// </summary>
    public ConcurrentDictionary<ChunkCoord, BaseMesher> LoadedChunks => _resources.LoadedChunks;
    
    /// <summary>
    /// Chunks, die zur Entladung von der GPU vorgesehen sind.
    /// </summary>
    public ConcurrentQueue<BaseMesher> UnloadQueue => _resources.UnloadQueue;
    
    /// <summary>
    /// Der Job-Scheduler für Hintergrundarbeiten.
    /// </summary>
    private readonly JobScheduler? _jobScheduler;
    
    /// <summary>
    /// Globale Spiel-Einstellungen.
    /// </summary>
    private readonly GameSettings _settings;

    /// <summary>
    /// Zugriff auf die Spiel-Einstellungen.
    /// </summary>
    public GameSettings Settings => _settings;
    
    /// <summary>
    /// Zugriff auf den Job-Scheduler.
    /// </summary>
    public JobScheduler? JobScheduler => _jobScheduler;

    private readonly ChunkMeshResources _resources = new();
    internal IChunkGpuMeshFactory GpuMeshFactory { get; }
    
    /// <summary>
    /// Initialisiert einen neuen ChunkProvider.
    /// </summary>
    /// <param name="gpuMeshFactory">Factory for chunk mesh uploads on the main thread.</param>
    /// <param name="jobScheduler">Optionaler Job-Scheduler für Hintergrundarbeiten.</param>
    /// <param name="settings">Optionale Spiel-Einstellungen. Falls nicht angegeben, werden Standardwerte verwendet.</param>
    public ChunkProvider(IChunkGpuMeshFactory gpuMeshFactory, JobScheduler? jobScheduler = null, GameSettings? settings = null)
    {
        GpuMeshFactory = gpuMeshFactory;
        _blockAccess = new ChunkBlockAccess(Chunkdata, RemeshChunk);
        _jobScheduler = jobScheduler;
        _settings = settings ?? EngineContext.Current?.Settings ?? GameSettings.Current;
    }

    /// <summary>
    /// Fordert einen Chunk an. Prüft zuerst, ob er schon geladen ist,
    /// dann ob er von der Festplatte geladen werden kann,
    /// und generiert ihn ansonsten neu.
    /// </summary>
    /// <param name="coord">Die Koordinaten desChunks.</param>
    public virtual void RequestChunk(ChunkCoord coord)
    {
        if (_jobScheduler is null) return;
        long? generation = BeginChunkRequest(coord);
        if (generation is null) return;
        Metrics.Requested();
        var job = new ChunkLoadOrGenerateJob(coord, generation.Value);
        if (!_jobScheduler.EnqueueLow(job)) CompleteChunkRequest(coord, generation.Value);
    }

    internal long? BeginChunkRequest(ChunkCoord coord) => _lifetime.Begin(coord, () => LoadedChunks.ContainsKey(coord));
    internal bool TryPublishChunkData(ChunkCoord coord, long generation, ChunkData data) => _lifetime.Publish(coord, generation, data);
    internal void CompleteChunkRequest(ChunkCoord coord, long generation) => _lifetime.Complete(coord, generation);

    /// <summary>
    /// Fordert Meshes aus der Upload-Warteschlange an und sendet sie an den Scheduler.
    /// Berücksichtigt die maximale Anzahl an Chunks im RAM.
    /// </summary>
    public void RequestMeshes()
    {
        _meshRequests.Dispatch(_settings.MaxChunkMeshesInRam, UploadQueue, _jobScheduler, this);
    }

    /// <summary>
    /// Prüft, ob alle 26 Nachbarn (3x3x3) eines Chunks geladen sind.
    /// </summary>
    /// <param name="c">Der zu prüfende Chunk.</param>
    /// <returns><c>true</c>, wenn alle Nachbarn geladen sind, andernfalls <c>false</c>.</returns>
    /// <summary>
    /// Ändert einen Block an einer Weltposition und löst Re-Meshing aus.
    /// Gibt true zurück wenn der Block erfolgreich geändert wurde.
    /// </summary>
    /// <param name="worldX">X-Koordinate im Weltkoordinatensystem.</param>
    /// <param name="worldY">Y-Koordinate im Weltkoordinatensystem.</param>
    /// <param name="worldZ">Z-Koordinate im Weltkoordinatensystem.</param>
    /// <param name="newBlockId">Die neue Block-ID.</param>
    /// <returns><c>true</c>, wenn der Block erfolgreich geändert wurde, andernfalls <c>false</c>.</returns>
    public bool ModifyBlock(int worldX, int worldY, int worldZ, ushort newBlockId) =>
        _blockAccess.ModifyBlock(worldX, worldY, worldZ, newBlockId);

    /// <summary>
    /// Schickt einen Chunk zum Re-Meshing. Das alte Mesh wird erst entladen, wenn das neue fertig ist.
    /// </summary>
    /// <param name="coord">Die Koordinaten des zu re-meschenden Chunks.</param>
    public void RemeshChunk(ChunkCoord coord)
    {
        if (!Chunkdata.ContainsKey(coord)) return;
        if (!ChunkNeighbors.HasAll(Chunkdata, coord)) return;

        QueueMesh(coord);
    }

    internal void QueueMesh(ChunkCoord coord) => _meshRequests.Queue(coord);

    internal bool CompleteMeshRequest(MeshRequest request) => _meshRequests.Complete(request);

    /// <summary>Cheap early check; acceptance must still be checked after GPU upload.</summary>
    public bool IsUploadCurrent(BaseMesher mesh)
    {
        lock (_lifetime.Gate)
            return mesh.SourceChunkData is not null &&
                   Chunkdata.TryGetValue(mesh.ChunkPosition, out var current) &&
                   ReferenceEquals(current, mesh.SourceChunkData);
    }

    /// <summary>Installs only a mesh built from the current chunk lifetime.</summary>
    public bool AcceptUpload(BaseMesher mesh)
    {
        lock (_lifetime.Gate)
        {
            if (mesh.SourceChunkData is null ||
                !Chunkdata.TryGetValue(mesh.ChunkPosition, out var current) ||
                !ReferenceEquals(current, mesh.SourceChunkData))
            {
                mesh.Dispose();
                return false;
            }

            if (LoadedChunks.TryGetValue(mesh.ChunkPosition, out var oldMesh))
                UnloadQueue.Enqueue(oldMesh);
            LoadedChunks[mesh.ChunkPosition] = mesh;
            return true;
        }
    }

    /// <summary>
    /// Gibt die ChunkData an einer Weltposition zurück oder null, wenn nicht geladen.
    /// </summary>
    /// <param name="coord">Die Koordinaten des Chunks.</param>
    /// <returns>Die Chunk-Daten oder <c>null</c>, wenn der Chunk nicht geladen ist.</returns>
    public ChunkData? GetChunkData(ChunkCoord coord)
    {
        Chunkdata.TryGetValue(coord, out ChunkData? data);
        return data;
    }

    /// <summary>
    /// Gibt die Block-ID an einer Weltposition zurück (0 = Luft/nicht geladen).
    /// </summary>
    /// <param name="worldX">X-Koordinate im Weltkoordinatensystem.</param>
    /// <param name="worldY">Y-Koordinate im Weltkoordinatensystem.</param>
    /// <param name="worldZ">Z-Koordinate im Weltkoordinatensystem.</param>
    /// <returns>Die Block-ID oder 0, wenn der Block nicht geladen ist.</returns>
    public ushort GetBlockAt(int worldX, int worldY, int worldZ) => _blockAccess.GetBlockAt(worldX, worldY, worldZ);

    /// <summary>
    /// Entfernt einen Chunk aus dem Speicher und gibt seine Ressourcen frei.
    /// </summary>
    /// <param name="coord">Die Koordinaten des zu entladenden Chunks.</param>
    public void UnloadChunk(ChunkCoord coord)
    {
        lock (_lifetime.Gate)
        {
            if (LoadedChunks.TryRemove(coord, out BaseMesher? chunk))
                UnloadQueue.Enqueue(chunk);

            Chunkdata.TryRemove(coord, out _);
            _lifetime.Cancel(coord);
            // Unload drops whichever request currently owns this coordinate.
            QueuedForMeshing.TryRemove(coord, out _);
        }
    }

    /// <summary>
    /// Gibt alle aktuell geladenen Chunks zurück (für den Renderer).
    /// </summary>
    /// <returns>Ein Enumerabler über alle geladenen Chunks.</returns>
    public IEnumerable<BaseMesher> GetLoadedChunks()
    {
        return LoadedChunks.Values;
    }

    /// <summary>
    /// Prüft ob ein Chunk bereits geladen ist.
    /// </summary>
    /// <param name="coord">Die Koordinaten des zu prüfenden Chunks.</param>
    /// <returns><c>true</c>, wenn der Chunk geladen ist, andernfalls <c>false</c>.</returns>
    public bool IsChunkLoaded(ChunkCoord coord)
    {
        return LoadedChunks.ContainsKey(coord);
    }

    /// <summary>
    /// Gibt alle Ressourcen frei und leert alle Caches und Queues.
    /// </summary>
    public void Dispose()
    {
        _resources.Dispose();
        Chunkdata.Clear();
        lock (_lifetime.Gate) _lifetime.ClearPending();
        QueuedForMeshing.Clear();
        while (PendingMeshRequests.TryDequeue(out _)) { }
    }


    // =========================================================================
    // --- Statische Fassade für Ergonomie und Abwärtskompatibilität ---
    // =========================================================================

    private static ChunkProvider? _fallbackInstance;
    
    /// <summary>
    /// Zugriff auf den aktuellen ChunkProvider über eine globale Fassade.
    /// Fallback-Instanz, falls kein EngineContext verfügbar ist.
    /// </summary>
    public static ChunkProvider Current
    {
        get => EngineContext.Current?.ChunkProvider ?? (_fallbackInstance ??= new ChunkProvider(NullChunkGpuMeshFactory.Instance));
        set => _fallbackInstance = value;
    }
}
