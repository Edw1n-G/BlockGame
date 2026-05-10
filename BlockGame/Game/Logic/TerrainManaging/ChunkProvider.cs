using System.Collections.Concurrent;
using System.Threading.Channels;
using Basics.Game.Logic.TerrainManaging.Generation;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Basics.Game.Utilities;

namespace Basics.Game.Logic.TerrainManaging;

/// <summary>
/// Verwaltet den Chunk-Lebenszyklus: Laden von Disk (Placeholder), Generieren, Speichern.
/// Zentraler Speicher für alle geladenen Chunks.
///
/// Für Multiplayer Client Server funktionalität muss generieren und meshen der chunks gesplitted werden
/// </summary>
public class ChunkProvider
{
    // Chunk Daten und Meshing Anfragen
    public static ConcurrentDictionary<ChunkCoord, ChunkData> Chunkdata = new();//Die Blockdaten
    private ConcurrentDictionary<ChunkCoord, byte> _queuedForMeshing = new();// Nur damit nicht mehrere Threads denselben Chunk meshen
    
    // Meshing Anfragen
    public Channel<ChunkCoord> MeshingQueue = Channel.CreateBounded<ChunkCoord>(new BoundedChannelOptions(200) { 
        FullMode = BoundedChannelFullMode.Wait, // Lässt den Thread schlafen, wenn voll!
        SingleReader = false, 
        SingleWriter = false 
    });
    
    // Buffer für Meshing
    public static readonly ConcurrentQueue<PooledMeshBuffer> VramPool = new();
    public static readonly ConcurrentQueue<List<byte>> VertexListPool = new();
    public static readonly ConcurrentQueue<List<uint>> IndexListPool = new();

    // fertig zum Upload bereite meshes 
    public Channel<BaseMesher> UploadQueue = Channel.CreateBounded<BaseMesher>(new BoundedChannelOptions(300) { 
        FullMode = BoundedChannelFullMode.Wait, 
        SingleReader = true, 
        SingleWriter = false 
    });
    
    //hochgeladene meshes
    public static readonly ConcurrentDictionary<ChunkCoord, BaseMesher> LoadedChunks = new();
    
    // meshes die entladen werden müssen
    public ConcurrentQueue<BaseMesher> UnloadQueue = new(); // Chunks die wieder aus der GPU rausmüssen
    private readonly TerrainGenerator _terrainGenerator;
    
    
    private bool _isRunning = true;
    
    public ChunkProvider(TerrainGenerator terrainGenerator, int meshingThreads)
    {
        _terrainGenerator = terrainGenerator;
        for (int i = 0; i < meshingThreads; i++)
        {
            // Threads machen die nicht aus dem Algemeinen Threadpool kommen
            Thread t = new Thread(MeshingWorkerLoop)
            {
                IsBackground = true,
                Name = $"MeshingThread_{i}"
            };
            t.Start();
        }
    }

    /// <summary>
    /// Fordert einen Chunk an. Prüft zuerst, ob er schon geladen ist,
    /// dann ob er von der Festplatte geladen werden kann,
    /// und generiert ihn ansonsten neu.
    /// </summary>
    public void RequestChunk(ChunkCoord coord)
    {
        // Bereits geladen oder Daten bereits generiert? → nichts tun
        if (LoadedChunks.ContainsKey(coord))
            return;
        
        if (Chunkdata.ContainsKey(coord))
            return;

        // Versuch von Festplatte zu laden (Placeholder)
        if (TryLoadFromDisk(coord, out BaseMesher? loadedChunk))
        {
            LoadedChunks.TryAdd(coord, loadedChunk!);
            return;
        }
        
        // Chunk generieren
        ushort[]? chunkBlocks = _terrainGenerator.GenerateChunk(coord);
        
        // Wenn der Chunkgenerator null zurückgibt ist der Chunk nur Luft oder
        // nicht in der Welt. Trotzdem speichern, damit Nachbar-Chunks gemesht werden können
        ChunkData chunkData;
        if (chunkBlocks == null)
        {
            chunkData = new ChunkData(coord); // Alles Luft
        }
        else
        {
            chunkData = new ChunkData(coord, chunkBlocks);
        }
        OnChunkDataGenerated(coord, chunkData);
    }
    
    public void OnChunkDataGenerated(ChunkCoord coord, ChunkData data)
    {
        // Daten im Dictionary ablegen
        Chunkdata.TryAdd(coord, data);

        // chunk selbst prüfen
        TryQueueForMeshing(coord);

        // nachbar Chunks prüfen
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y + 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y - 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y, coord.Z - 1, coord.LodLevel));
        // Diagonale
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y + 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y + 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y - 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X, coord.Y - 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y + 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y - 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y + 1, coord.Z, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y - 1, coord.Z, coord.LodLevel));
        // Ecken
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y + 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y + 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y - 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X + 1, coord.Y - 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y + 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y + 1, coord.Z - 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y - 1, coord.Z + 1, coord.LodLevel));
        TryQueueForMeshing(new ChunkCoord(coord.X - 1, coord.Y - 1, coord.Z - 1, coord.LodLevel));
    }
    
    private void TryQueueForMeshing(ChunkCoord coord)
    {
        //Selbstcheck um existenzkrisen zu vermeiden
        if (!Chunkdata.ContainsKey(coord)) return;

        //Selbcheck schon angefangen
        if (_queuedForMeshing.ContainsKey(coord)) return;

        //Sind direkte Nachbarn im Dictionary?
        if (!HasAllNeighbors(coord)) return;
        
        
        if (_queuedForMeshing.TryAdd(coord, 0))
        {
            //Meshing anfangen aber warten bis platz im channel ist
            MeshingQueue.Writer.WriteAsync(coord).AsTask().Wait();
        }
    }
    
    private void MeshingWorkerLoop()
    {
        // GetConsumingEnumerable() blockiert automatisch wenn nichts drinne ist und wird selbst aktiv wenn man add aufruft
        foreach (ChunkCoord coord in MeshingQueue.Reader.ReadAllAsync().ToBlockingEnumerable())
        {
            if (!_isRunning) break;

            if (Chunkdata.TryGetValue(coord, out ChunkData chunkData))
            {
                BaseMesher newMesh;
                
                switch (coord.LodLevel)
                {
                    case 0:
                        newMesh = new Lod0Mesher(coord, chunkData);
                        break;
                    
                    case 1:
                        newMesh = new Lod1Mesher(coord, chunkData);
                        break;
                    
                    case 2:
                        newMesh = new Lod2Mesher(coord, chunkData);
                        break;
                    
                    default:
                        throw new Exception($"Ungültiges LOD-Level {coord.LodLevel} für Chunk {coord}");
                }
                
                UploadQueue.Writer.WriteAsync(newMesh).AsTask().Wait();
                _queuedForMeshing.TryRemove(coord, out _);
            }
        }
    }

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
    public bool ModifyBlock(int worldX, int worldY, int worldZ, ushort newBlockId)
    {
        // Weltkoordinaten → ChunkCoord + lokale Koordinaten
        ChunkCoord chunkCoord = ChunkData.WorldToChunkCoord(worldX, worldY, worldZ);
        var (localX, localY, localZ) = ChunkData.WorldToLocal(worldX, worldY, worldZ);

        if (!Chunkdata.TryGetValue(chunkCoord, out ChunkData? chunkData))
            return false; // Chunk nicht geladen

        // Block ändern
        chunkData.SetBlock(localX, localY, localZ, newBlockId);

        // Diesen Chunk neu meshen
        RemeshChunk(chunkCoord);

        // Wenn der Block am Rand des Chunks liegt, müssen auch Nachbar-Chunks neu gemesht werden
        if (localX == 0)  RemeshChunk(new ChunkCoord(chunkCoord.X - 1, chunkCoord.Y, chunkCoord.Z, chunkCoord.LodLevel));
        if (localX == 15) RemeshChunk(new ChunkCoord(chunkCoord.X + 1, chunkCoord.Y, chunkCoord.Z, chunkCoord.LodLevel));
        if (localY == 0)  RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y - 1, chunkCoord.Z, chunkCoord.LodLevel));
        if (localY == 15) RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y + 1, chunkCoord.Z, chunkCoord.LodLevel));
        if (localZ == 0)  RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y, chunkCoord.Z - 1, chunkCoord.LodLevel));
        if (localZ == 15) RemeshChunk(new ChunkCoord(chunkCoord.X, chunkCoord.Y, chunkCoord.Z + 1, chunkCoord.LodLevel));

        return true;
    }

    /// <summary>
    /// Schickt einen Chunk zum Re-Meshing. Das alte Mesh wird erst entladen, wenn das neue fertig ist
    /// </summary>
    public void RemeshChunk(ChunkCoord coord)
    {
        if (!Chunkdata.ContainsKey(coord)) return;
        if (!HasAllNeighbors(coord)) return;

        // Erlaubt Re-Queue
        _queuedForMeshing.TryRemove(coord, out _);

        // Neu in die Meshing-Queue
        if (_queuedForMeshing.TryAdd(coord, 0))
        {
            MeshingQueue.Writer.WriteAsync(coord).AsTask().Wait();
        }
        
        //Altes Mesh wird im Renderer ersetzt
    }

    /// <summary>
    /// Gibt die ChunkData an einer Weltposition zurück oder null, wenn nicht geladen.
    /// </summary>
    public ChunkData? GetChunkData(ChunkCoord coord)
    {
        Chunkdata.TryGetValue(coord, out ChunkData? data);
        return data;
    }

    /// <summary>
    /// Gibt die Block-ID an einer Weltposition zurück (0 = Luft/nicht geladen).
    /// </summary>
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
    public void UnloadChunk(ChunkCoord coord)
    {
        if (LoadedChunks.TryRemove(coord, out BaseMesher? chunk))
        {
            // TODO: Chunk auf Festplatte speichern bevor er entladen wird
            // SaveToDisk(coord, chunk);
            
            UnloadQueue.Enqueue(chunk);
        }
        
        //Chunks könnten auch ohne Mesh entladen werden
        Chunkdata.TryRemove(coord, out _);
        _queuedForMeshing.TryRemove(coord, out _);
    }

    /// <summary>
    /// Gibt alle aktuell geladenen Chunks zurück (für den Renderer).
    /// </summary>
    public IEnumerable<BaseMesher> GetLoadedChunks()
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
    private bool TryLoadFromDisk(ChunkCoord coord, out BaseMesher? chunk)
    {
        // TODO: Implementierung für Chunk-Laden von der Festplatte
        chunk = null;
        return false;
    }

    /// <summary>
    /// Gibt alle Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        foreach (BaseMesher chunk in LoadedChunks.Values)
        {
            chunk.Dispose();
        }
        LoadedChunks.Clear();
    }
}