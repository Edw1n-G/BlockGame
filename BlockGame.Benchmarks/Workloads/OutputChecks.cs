using BlockGame.Benchmarks.Comparison;
using Basics.Common;
using Basics.Terrain.Meshing;
using Basics.Terrain;
using System.Runtime.InteropServices;

namespace BlockGame.Benchmarks.Workloads;

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
        // Compare logical terrain, independent of the storage representation of air.
        using var bytes=new MemoryStream();
        for (int x=0;x<16;x++) for(int y=0;y<16;y++) for(int z=0;z<16;z++)
        {
            ushort block=chunk.GetBlock(x,y,z);
            bytes.WriteByte((byte)block); bytes.WriteByte((byte)(block >> 8));
        }
        return EvidenceJson.Hash(bytes.ToArray());
    }
    public static string Noise(float[] values)=>EvidenceJson.Hash(MemoryMarshal.AsBytes(values.AsSpan()));
}
