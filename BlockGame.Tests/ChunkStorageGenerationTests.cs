using Basics.Common;
using Basics.Terrain;
using Basics.Terrain.Generation;
using BlockGame.Benchmarks.Workloads;

namespace BlockGame.Tests;

public class ChunkStorageGenerationTests
{
    [Fact]
    public void SolidDetectionIgnoresHaloButFindsEveryInteriorVoxel()
    {
        var density = new float[16*17*16];
        for(int z=0;z<16;z++) for(int x=0;x<16;x++)
            density[x+16*16+z*16*17]=1;
        Assert.False(TerrainGenerator.HasSolidVoxel(density));
        for(int z=0;z<16;z++) for(int y=0;y<16;y++) for(int x=0;x<16;x++)
        {
            int index=x+y*16+z*16*17;
            density[index]=float.Epsilon;
            Assert.True(TerrainGenerator.HasSolidVoxel(density));
            density[index]=0;
        }
        Array.Fill(density,float.NaN);
        Assert.False(TerrainGenerator.HasSolidVoxel(density));
        Array.Fill(density,-1f);
        Assert.False(TerrainGenerator.HasSolidVoxel(density));
    }

    [Fact]
    public void GeneratedAirRetainsNoArrayAndAllocatesOnlyWhenEdited()
    {
        using var env = new WorkloadEnvironment();
        var coord = new ChunkCoord(0,64,0,0);
        var air = env.Generator.GenerateChunk(coord);
        Assert.Equal(coord,air.Coord);
        Assert.Null(air.Blocks);
        Assert.False(air.IsDirty);
        air.SetBlock(15,15,15,0);
        Assert.Null(air.Blocks);
        air.SetBlock(15,15,15,ushort.MaxValue);
        Assert.Equal(4096,air.Blocks!.Length);
        Assert.Equal(ushort.MaxValue,air.GetBlock(15,15,15));
        Assert.Equal((ushort)0,air.GetBlock(0,0,0));
        Assert.Null(env.Generator.GenerateChunk(coord).Blocks);
    }

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
