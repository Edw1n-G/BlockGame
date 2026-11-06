using System.Runtime.InteropServices;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Basics.Game.Utilities;
using BlockGame.Benchmarking;

namespace BlockGame.Workloads;

public static class OutputChecks
{
    public static string Selection(IEnumerable<ChunkCoord> coordinates)
    {
        using var bytes=new MemoryStream(); using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))
            foreach(var c in coordinates.OrderBy(c=>c.X).ThenBy(c=>c.Y).ThenBy(c=>c.Z).ThenBy(c=>c.LodLevel))
            {writer.Write(c.X);writer.Write(c.Y);writer.Write(c.Z);writer.Write(c.LodLevel);}
        return EvidenceJson.Hash(bytes.ToArray());
    }
    public static string Mesh(BaseMesher mesh)=>EvidenceJson.Fingerprint(new {vertices=EvidenceJson.Hash(MeshWorkload.Vertices(mesh)),indices=EvidenceJson.Hash(MemoryMarshal.AsBytes(MeshWorkload.Indices(mesh)))});
    public static string Terrain(ChunkData chunk)
    {
        using var bytes=new MemoryStream();bytes.WriteByte(chunk.Blocks is null?(byte)0:(byte)1);
        if(chunk.Blocks is {} blocks)bytes.Write(MemoryMarshal.AsBytes(blocks.AsSpan()));
        return EvidenceJson.Hash(bytes.ToArray());
    }
    public static string Noise(float[] values)=>EvidenceJson.Hash(MemoryMarshal.AsBytes(values.AsSpan()));
}
