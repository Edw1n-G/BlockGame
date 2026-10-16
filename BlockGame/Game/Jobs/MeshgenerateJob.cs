using System;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Meshing;

namespace Basics.Game.Utilities;

public class MeshgenerateJob : IJob
{
    public ChunkCoord ChunkCoord;
    
    public MeshgenerateJob(ChunkCoord chunkCoord)
    {
        this.ChunkCoord = chunkCoord;
    }

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