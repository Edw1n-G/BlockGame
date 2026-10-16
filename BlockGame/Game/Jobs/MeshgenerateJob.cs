using System;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Meshing;

namespace Basics.Game.Utilities;

/// <summary>
/// Job zum Generieren des Meshes eines Chunks basierend auf seinem LOD-Level.
/// </summary>
public class MeshgenerateJob : IJob
{
    /// <summary>Die Koordinaten des zu meshenden Chunks.</summary>
    public ChunkCoord ChunkCoord;
    
    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="MeshgenerateJob"/>.
    /// </summary>
    /// <param name="chunkCoord">Die Koordinaten des zu meshenden Chunks.</param>
    public MeshgenerateJob(ChunkCoord chunkCoord)
    {
        this.ChunkCoord = chunkCoord;
    }

    /// <summary>
    /// Führt den Mesh-Generierungs-Job aus.
    /// Erstellt das passende Mesh-Objekt (Lod0/1/2Mesher) basierend auf dem LOD-Level des Chunks.
    /// </summary>
    /// <param name="context">Der <see cref="JobContext"/> mit den notwendigen Ressourcen.</param>
    public void Execute(JobContext context)
    {
        // Hole Block-Daten aus dem RAM der zuständigen ChunkProvider-Instanz
        if (!context.Provider.Chunkdata.TryGetValue(ChunkCoord, out var data))
        {
            // Chunk entladen oder nie dagewesen
            return;
        }
        
        BaseMesher newMesh;
        
        // Baue das Mesh mit dem injizierten ChunkProvider
        switch (ChunkCoord.LodLevel)
        {
            case 0:
                newMesh = new Lod0Mesher(ChunkCoord, data, context.Provider, context.BlockRegistry);
                break;
                    
            case 1:
                newMesh = new Lod1Mesher(ChunkCoord, data, context.Provider, context.BlockRegistry);
                break;
                    
            case 2:
                newMesh = new Lod2Mesher(ChunkCoord, data, context.Provider, context.BlockRegistry);
                break;
                    
            default:
                throw new Exception($"Ungültiges LOD-Level {ChunkCoord.LodLevel} für Chunk {ChunkCoord}");
        }  
        
        context.Provider.UploadQueue.Enqueue(newMesh);
        context.Provider.QueuedForMeshing.TryRemove(ChunkCoord, out _);
    }
}