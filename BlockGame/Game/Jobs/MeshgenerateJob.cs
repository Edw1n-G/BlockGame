using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Meshing;

namespace Basics.Game.Utilities;

public class MeshgenerateJob : IJob
{
    public ChunkCoord ChunkCoord;
    
    public MeshgenerateJob(ChunkCoord ChunkCoord)
    {
        this.ChunkCoord = ChunkCoord;
    }

    public void Execute(JobContext context)
    {
        //Hole Block-Daten aus dem RAM
        if (!ChunkProvider.Chunkdata.TryGetValue(ChunkCoord, out var data))
        {
            // Chunk entladen oder nie dagewesen
            return;
        }
        
        BaseMesher newMesh;
        
        //Baue das Mesh
        switch (ChunkCoord.LodLevel)
        {
            case 0:
                newMesh = new Lod0Mesher(ChunkCoord, data);
                break;
                    
            case 1:
                newMesh = new Lod1Mesher(ChunkCoord, data);
                break;
                    
            case 2:
                newMesh = new Lod2Mesher(ChunkCoord, data);
                break;
                    
            default:
                throw new Exception($"Ungültiges LOD-Level {ChunkCoord.LodLevel} für Chunk {ChunkCoord}");
        }  
        
        context.Provider.UploadQueue.Enqueue(newMesh);
        ChunkProvider.QueuedForMeshing.TryRemove(ChunkCoord, out _);
    }
}