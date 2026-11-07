using BlockGame.Benchmarks.Workloads;
using Basics.Common;
using System.Buffers.Binary;
using System.Numerics;

namespace BlockGame.Tests;

public class WorkloadTests
{
    public static IEnumerable<object[]> MeshCases => WorkloadCatalog.MeshCases.Select(x => new object[] { x });
    public static IEnumerable<object[]> TerrainCases => WorkloadCatalog.TerrainCases.Select(x => new object[] { x });
    public static IEnumerable<object[]> SelectionCases => WorkloadCatalog.SelectionCases.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(MeshCases))]
    public void MeshFixturesHaveExpectedGeometryAndStablePools(string id)
    {
        using var env = new WorkloadEnvironment();
        using var fixture = new MeshWorkload(id, env.Registry);
        string before = fixture.Fingerprint();
        string? meshHash = null;
        for (int run = 0; run < 12; run++)
        {
            using (var mesh = fixture.Build())
            {
                var vertices = MeshWorkload.Vertices(mesh); var indices = MeshWorkload.Indices(mesh);
                int? faces = id switch { "null-air" or "allocated-air" or "occluded-solid" => 0,
                    "exposed-solid" => 1536, "floor" => 1024, "checkerboard" => 12288, _ => null };
                if (faces is { } count) { Assert.Equal(count * 4 * 16, vertices.Length); Assert.Equal(count * 6, indices.Length); }
                else Assert.True(indices.Length > 0, "Frozen terrain must produce geometry");
                Assert.Equal(0, vertices.Length % 64); Assert.Equal(vertices.Length / 64 * 6, indices.Length);
                foreach (uint index in indices) Assert.True(index < vertices.Length / 16);
                for (int i = 0; i < vertices.Length; i += 16)
                {
                    var p = Position(vertices, i / 16);
                    Assert.InRange(p.X, 0, 16); Assert.InRange(p.Y, 0, 16); Assert.InRange(p.Z, 0, 16);
                    Assert.InRange(vertices[i + 14], (byte)0, (byte)3);
                    Assert.Equal(0, vertices[i + 15]);
                }
                string hash = WorkloadEnvironment.Hash(vertices) + WorkloadEnvironment.Hash(System.Runtime.InteropServices.MemoryMarshal.AsBytes(indices));
                meshHash ??= hash; Assert.Equal(meshHash, hash);
            }
            fixture.ValidateRetainedState();
        }
        Assert.Equal(before, fixture.Fingerprint());
    }

    [Fact]
    public void IsolatedBlockHasOutwardTrianglesFaceMaterialsAndNoAo()
    {
        using var env = new WorkloadEnvironment();
        using var f = new MeshWorkload("allocated-air", env.Registry);
        ushort grass = env.Registry.GetBlockId("core:grass");
        f.Data.SetBlock(8, 8, 8, grass);
        using var m = f.Build(); var v = MeshWorkload.Vertices(m); var indices = MeshWorkload.Indices(m);
        Assert.Equal(24 * 16, v.Length); Assert.Equal(36, indices.Length);
        Vector3[] normals = [Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitX];
        for (int face = 0; face < 6; face++)
        {
            for (int i = 0; i < 4; i++)
            {
                int offset = (face * 4 + i) * 16;
                Assert.Equal(env.Registry.Get(grass, face), BinaryPrimitives.ReadUInt16LittleEndian(v.Slice(offset + 12, 2)));
                Assert.Equal(0, v[offset + 14]);
            }
            for (int tri = 0; tri < 2; tri++)
            {
                int start = face * 6 + tri * 3;
                var a = Position(v, (int)indices[start]); var b = Position(v, (int)indices[start + 1]); var c = Position(v, (int)indices[start + 2]);
                Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), normals[face]) > 0);
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TopAoAndDiagonalFollowKnownOccluders(bool leftFront)
    {
        using var env = new WorkloadEnvironment(); using var f = new MeshWorkload("allocated-air", env.Registry);
        ushort stone = env.Registry.GetBlockId("core:stone");
        f.Data.SetBlock(8, 8, 8, stone);
        f.Data.SetBlock(leftFront ? 7 : 9, 9, 8, stone);
        f.Data.SetBlock(8, 9, 9, stone);
        using var m = f.Build(); var v = MeshWorkload.Vertices(m); var indices = MeshWorkload.Indices(m);
        int quad = -1;
        for (int i = 0; i < v.Length / 16; i += 4)
            if (Position(v, i) == new Vector3(8, 9, 9) && Position(v, i + 2) == new Vector3(9, 9, 8)) { quad = i; break; }
        Assert.True(quad >= 0);
        byte[] ao = leftFront ? [3, 1, 0, 1] : [1, 3, 1, 0];
        for (int i = 0; i < 4; i++) Assert.Equal(ao[i], v[(quad + i) * 16 + 14]);
        uint[] expected = leftFront ? [1, 2, 3, 1, 3, 0] : [0, 1, 2, 0, 2, 3];
        for (int i = 0; i < 6; i++) Assert.Equal((uint)quad + expected[i], indices[quad / 4 * 6 + i]);
    }

    [Fact]
    public void BorrowedViewsCannotReadReturnedPoolBuffers()
    {
        using var env = new WorkloadEnvironment(); using var f = new MeshWorkload("floor", env.Registry);
        var mesh = f.Build(); mesh.Dispose(); mesh.Dispose();
        Assert.Throws<InvalidOperationException>(() => MeshWorkload.Summary(mesh)); f.ValidateRetainedState();
    }

    [Theory]
    [MemberData(nameof(TerrainCases))]
    public void TerrainFixtureClassificationIsRealAndRepeatable(string id)
    {
        var t = WorkloadCatalog.Terrain(id); using var env = new WorkloadEnvironment(t.Seed);
        var a = env.Generator.GenerateChunk(t.Coord); var b = env.Generator.GenerateChunk(t.Coord);
        Assert.Equal(a.Blocks, b.Blocks); Assert.Equal(t.Coord, a.Coord);
        int count = a.Blocks?.Count(x => x != 0) ?? 0;
        switch (t.Kind)
        {
            case "outside": Assert.Null(a.Blocks); break;
            case "air": Assert.NotNull(a.Blocks); Assert.Equal(0, count); break;
            case "solid": Assert.Equal(4096, count); break;
            case "surface": Assert.InRange(count, 1, 4095); Assert.Contains(env.Registry.GetBlockId("core:grass"), a.Blocks!); break;
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void NoiseGridHasExpectedShapeFiniteValuesAndRepeatability(int seed)
    {
        using var env = new WorkloadEnvironment(seed);
        var n = WorkloadCatalog.Noise($"grid-s{seed}");
        var a = env.Noise.GetNoiseValues(n.X, n.Y, n.Z, 16, 17, 16, 1);
        Assert.Equal(4352, a.Length); Assert.All(a, value => Assert.True(float.IsFinite(value)));
        Assert.True(a.Distinct().Count() > 1);
        Assert.Equal(a, env.Noise.GetNoiseValues(n.X, n.Y, n.Z, 16, 17, 16, 1));
    }

    [Theory]
    [MemberData(nameof(SelectionCases))]
    public void SelectionMatchesIndependentIntegerEllipsoid(string id)
    {
        var (c, h, v) = WorkloadCatalog.Selection(id); var expected = new HashSet<ChunkCoord>();
        // Integer inequality, independent of the floating-point implementation's normalization.
        for (int x = -h; x <= h; x++) for (int y = -v; y <= v; y++) for (int z = -h; z <= h; z++)
            if ((long)(x*x + z*z) * Math.Max(1, v*v) + (long)y*y * Math.Max(1, h*h) <= (long)Math.Max(1,h*h) * Math.Max(1,v*v))
                expected.Add(new ChunkCoord(c.X+x,c.Y+y,c.Z+z,0));
        Assert.True(expected.SetEquals(WorkloadCatalog.Select(c,h,v)));
    }

    private static Vector3 Position(ReadOnlySpan<byte> bytes, int vertex) => new(
        BitConverter.ToSingle(bytes.Slice(vertex * 16, 4)), BitConverter.ToSingle(bytes.Slice(vertex * 16 + 4, 4)), BitConverter.ToSingle(bytes.Slice(vertex * 16 + 8, 4)));
}
