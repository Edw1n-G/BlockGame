using System.Collections.Concurrent;
using Silk.NET.OpenGL;
using Basics.Game;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Basics.Game.Utilities;

namespace Basics.Game.Logic.TerrainManaging;

/// <summary>
/// Verwaltet den Chunk-Lebenszyklus: Laden von Disk (Placeholder), Generieren, Speichern.
/// Zentraler Speicher für alle geladenen Chunks einer Engine-Instanz.
/// </summary>
public class ChunkProvider : IDisposable
{
    /// <summary>
    /// Enthält alle Chunk-Daten für geladene Chunks.
    /// </summary>
    public ConcurrentDictionary<ChunkCoord, ChunkData> Chunkdata { get; } = new();
    
    /// <summary>
    /// Chunks, die für das Meshing in die Warteschlange gestellt wurden.
    /// </summary>
    public ConcurrentDictionary<ChunkCoord, byte> QueuedForMeshing { get; } = new();
    
    /// <summary>
    /// Pool für Mesh-Puffer (VBO, EBO, VAO) für das Speichermanagement.
    /// </summary>
    public ConcurrentQueue<PooledMeshBuffer> VramPool { get; } = new();
    
    /// <summary>
    /// Pool für Vertex-Listen zur Vermeidung von GC-Allocations.
    /// </summary>
    public ConcurrentQueue<List<byte>> VertexListPool { get; } = new();
    
    /// <summary>
    /// Pool für Index-Listen zur Vermeidung von GC-Allocations.
    /// </summary>
    public ConcurrentQueue<List<uint>> IndexListPool { get; } = new();
    
    /// <summary>
    /// Anfragen für Meshing, die noch bearbeitet werden müssen.
    /// </summary>
    public ConcurrentQueue<ChunkCoord> PendingMeshRequests { get; } = new();

    /// <summary>
    /// Meshes, die für den Upload zur GPU bereit sind.
    /// </summary>
    public ConcurrentQueue<BaseMesher> UploadQueue { get; } = new();
    
    /// <summary>
    /// Alle aktuell auf der GPU geladenen Chunks.
    /// </summary>
    public ConcurrentDictionary<ChunkCoord, BaseMesher> LoadedChunks { get; } = new();
    
    /// <summary>
    /// Chunks, die zur Entladung von der GPU vorgesehen sind.
    /// </summary>
    public ConcurrentQueue<BaseMesher> UnloadQueue { get; } = new();
    
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

    private GL? _gl;
    
    /// <summary>
    /// Initialisiert einen neuen ChunkProvider.
    /// </summary>
    /// <param name="jobScheduler">Optionaler Job-Scheduler für Hintergrundarbeiten.</param>
    /// <param name="settings">Optionale Spiel-Einstellungen. Falls nicht angegeben, werden Standardwerte verwendet.</param>
    public ChunkProvider(JobScheduler? jobScheduler = null, GameSettings? settings = null)
    {
        _jobScheduler = jobScheduler;
        _settings = settings ?? EngineContext.Current?.Settings ?? GameSettings.Current;
    }

    /// <summary>
    /// Bindet den OpenGL-Kontext an den Provider für GPU-Operationen.
    /// </summary>
    /// <param name="gl">Der OpenGL-Kontext.</param>
    public void AttachGraphicsContext(GL gl)
    {
        _gl = gl;
    }

    /// <summary>
    /// Fordert einen Chunk an. Prüft zuerst, ob er schon geladen ist,
    /// dann ob er von der Festplatte geladen werden kann,
    /// und generiert ihn ansonsten neu.
    /// </summary>
    /// <param name="coord">Die Koordinaten desChunks.</param>
    public void RequestChunk(ChunkCoord coord)
    {
        // Bereits geladen oder Daten bereits generiert? → nichts tun
        if (LoadedChunks.ContainsKey(coord))
            return;
        
        if (Chunkdata.ContainsKey(coord))
            return;

        _jobScheduler?.EnqueueLow(new ChunkLoadorGenerateJob(coord));
    }
    
    /// <summary>
    /// Fordert Meshes aus der Upload-Warteschlange an und sendet sie an den Scheduler.
    /// Berücksichtigt die maximale Anzahl an Chunks im RAM.
    /// </summary>
    public void RequestMeshes()
    {
        int maxMeshesInRam = _settings.MaxChunkMeshesInRam;

        while (UploadQueue.Count < maxMeshesInRam && 
               PendingMeshRequests.TryDequeue(out ChunkCoord coord))
        {
            _jobScheduler?.EnqueueLow(new MeshgenerateJob(coord));
        }
    }

    /// <summary>
    /// Prüft, ob alle 26 Nachbarn (3x3x3) eines Chunks geladen sind.
    /// </summary>
    /// <param name="c">Der zu prüfende Chunk.</param>
    /// <returns><c>true</c>, wenn alle Nachbarn geladen sind, andernfalls <c>false</c>.</returns>
    private bool HasAllNeighbors(ChunkCoord c)
    {
        // 6 direkte Nachbarn
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y, c.Z - 1, c.LodLevel))) return false;

        // 12 Kanten-Nachbarn
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X, c.Y - 1, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z, c.LodLevel))) return false;

        // 8 Eck-Nachbarn
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X + 1, c.Y - 1, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y + 1, c.Z - 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z + 1, c.LodLevel))) return false;
        if (!Chunkdata.ContainsKey(new ChunkCoord(c.X - 1, c.Y - 1, c.Z - 1, c.LodLevel))) return false;

        return true;
    }

    /// <summary>
    /// Ändert einen Block an einer Weltposition und löst Re-Meshing aus.
    /// Gibt true zurück wenn der Block erfolgreich geändert wurde.
    /// </summary>
    /// <param name="worldX">X-Koordinate im Weltkoordinatensystem.</param>
    /// <param name="worldY">Y-Koordinate im Weltkoordinatensystem.</param>
    /// <param name="worldZ">Z-Koordinate im Weltkoordinatensystem.</param>
    /// <param name="newBlockId">Die neue Block-ID.</param>
    /// <returns><c>true</c>, wenn der Block erfolgreich geändert wurde, andernfalls <c>false</c>.</returns>
    public bool ModifyBlock(int worldX, int worldY, int worldZ, ushort newBlockId)
    {
        ChunkCoord chunkCoord = ChunkData.WorldToChunkCoord(worldX, worldY, worldZ);
        var (localX, localY, localZ) = ChunkData.WorldToLocal(worldX, worldY, worldZ);

        if (!Chunkdata.TryGetValue(chunkCoord, out ChunkData? chunkData))
            return false;

        chunkData.SetBlock(localX, localY, localZ, newBlockId);
        RemeshChunk(chunkCoord);

        if (localX == 0)  RemeshChunk(new ChunkCoord(chunkCoord.X - 1, chunkCoord.Y, chunkCoord.Z, chunkCoord.LodLevel));
        if (localX == 15) RemeshChunk(new ChunkCoord(chunkCoord.X + 1, chunkCoord.Y, chunkCoord.Z, chunkCoord.LodLevel));
        if (localY == 0)  RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y - 1, chunkCoord.Z, chunkCoord.LodLevel));
        if (localY == 15) RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y + 1, chunkCoord.Z, chunkCoord.LodLevel));
        if (localZ == 0)  RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y, chunkCoord.Z - 1, chunkCoord.LodLevel));
        if (localZ == 15) RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y, chunkCoord.Z + 1, chunkCoord.LodLevel));

        return true;
    }

    /// <summary>
    /// Schickt einen Chunk zum Re-Meshing. Das alte Mesh wird erst entladen, wenn das neue fertig ist.
    /// </summary>
    /// <param name="coord">Die Koordinaten des zu re-meschenden Chunks.</param>
    public void RemeshChunk(ChunkCoord coord)
    {
        if (!Chunkdata.ContainsKey(coord)) return;
        if (!HasAllNeighbors(coord)) return;

        if (QueuedForMeshing.TryAdd(coord, 1))
        {
            PendingMeshRequests.Enqueue(coord);
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
    public ushort GetBlockAt(int worldX, int worldY, int worldZ)
    {
        ChunkCoord chunkCoord = ChunkData.WorldToChunkCoord(worldX, worldY, worldZ);
        if (!Chunkdata.TryGetValue(chunkCoord, out ChunkData? chunkData))
            return 0;
        var (localX, localY, localZ) = ChunkData.WorldToLocal(worldX, worldY, worldZ);
        return chunkData.GetBlock(localX, localY, localZ);
    }

    /// <summary>
    /// Entfernt einen Chunk aus dem Speicher und gibt seine Ressourcen frei.
    /// </summary>
    /// <param name="coord">Die Koordinaten des zu entladenden Chunks.</param>
    public void UnloadChunk(ChunkCoord coord)
    {
        if (LoadedChunks.TryRemove(coord, out BaseMesher? chunk))
        {
            UnloadQueue.Enqueue(chunk);
        }
        
        Chunkdata.TryRemove(coord, out _);
        QueuedForMeshing.TryRemove(coord , out _);
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
        foreach (BaseMesher chunk in LoadedChunks.Values)
        {
            chunk.Dispose();
        }
        LoadedChunks.Clear();
        Chunkdata.Clear();
        QueuedForMeshing.Clear();

        while (PendingMeshRequests.TryDequeue(out _)) { }
        while (UploadQueue.TryDequeue(out var mesher)) { mesher.Dispose(); }
        while (UnloadQueue.TryDequeue(out var mesher)) { mesher.Dispose(); }

        while (VramPool.TryDequeue(out var pooled))
        {
            if (_gl != null)
            {
                _gl.DeleteVertexArray(pooled.Vao);
                _gl.DeleteBuffer(pooled.Vbo);
                _gl.DeleteBuffer(pooled.Ebo);
            }
        }

        while (VertexListPool.TryDequeue(out _)) { }
        while (IndexListPool.TryDequeue(out _)) { }
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
        get => EngineContext.Current?.ChunkProvider ?? (_fallbackInstance ??= new ChunkProvider());
        set => _fallbackInstance = value;
    }
}