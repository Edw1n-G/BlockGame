using Basics.Common;
using Basics.Terrain;
using BlockGame.Benchmarks.Workloads;

namespace BlockGame.Tests;

public class ChunkStorageGenerationTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(42, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    public void GeneratedStorageMatchesDensityOracleAndEditsStayIndependent(int seed, byte lod)
    {
        using var env = new WorkloadEnvironment(seed);
        int step = 1 << lod;
        foreach (int worldY in new[] { -1024, -928, -784, 0, 1024 })
        {
            var coord = new ChunkCoord(-1, worldY / (16 * step), 1, lod);
            int sx = coord.X * 16 * step, sy = coord.Y * 16 * step, sz = coord.Z * 16 * step;
            float[] noise = env.Noise.GetNoiseValues(sx, sy, sz, 16, 17, 16, step);
            var a = env.Generator.GenerateChunk(coord);
            var b = env.Generator.GenerateChunk(coord);
            for (int x=0;x<16;x++) for(int y=0;y<16;y++) for(int z=0;z<16;z++)
            {
                int index=x+y*16+z*16*17;
                float density=noise[index];
                bool top=noise[index+16]<=0;
                ushort expected=density<=0 ? (ushort)0 : env.Registry.GetBlockId(
                    top ? (sy+y*step>80 ? "core:snow" : "core:grass") :
                    density<=4*step ? "core:dirt" : "core:stone");
                Assert.Equal(expected,a.GetBlock(x,y,z));
                Assert.Equal(expected,b.GetBlockSafe(x,y,z));
                Assert.Equal(expected!=0,a.IsBlock(x,y,z));
            }
            ushort original=b.GetBlock(15,15,15);
            a.SetBlock(15,15,15,ushort.MaxValue);
            Assert.Equal(ushort.MaxValue,a.GetBlock(15,15,15));
            Assert.Equal(original,b.GetBlock(15,15,15));
            Assert.True(a.IsDirty);
        }
    }

    [Fact]
    public void TerrainFingerprintDescribesBlocksRatherThanAirRepresentation()
    {
        var coord=new ChunkCoord(0,0,0,0);
        var implicitAir=new ChunkData(coord);
        var explicitAir=new ChunkData(coord,new ushort[4096]);
        Assert.Equal(OutputChecks.Terrain(implicitAir),OutputChecks.Terrain(explicitAir));
        explicitAir.SetBlock(15,15,15,ushort.MaxValue);
        Assert.NotEqual(OutputChecks.Terrain(implicitAir),OutputChecks.Terrain(explicitAir));
    }
}
