using Basics.Game.Utilities;

namespace BlockGame.Tests;

public class ChunkCoordTests
{
    // ---------- Construction ----------

    [Fact]
    public void Constructor_SetsFieldsCorrectly()
    {
        ChunkCoord coord = new ChunkCoord(1, 2, 3, 4);

        Assert.Equal((short)1, coord.X);
        Assert.Equal((short)2, coord.Y);
        Assert.Equal((short)3, coord.Z);
        Assert.Equal((byte)4, coord.LodLevel);
    }

    [Fact]
    public void Constructor_NegativeValues_StoredCorrectly()
    {
        ChunkCoord coord = new ChunkCoord(-1, -2, -3, 0);

        Assert.Equal((short)-1, coord.X);
        Assert.Equal((short)-2, coord.Y);
        Assert.Equal((short)-3, coord.Z);
    }

    [Fact]
    public void Constructor_MinAndMaxShortValues_StoredExactly()
    {
        ChunkCoord coord = new ChunkCoord(short.MaxValue, short.MinValue, 0, 0);

        Assert.Equal(short.MaxValue, coord.X);
        Assert.Equal(short.MinValue, coord.Y);
    }

    [Theory]
    [InlineData(32768, -32768)]   // one above short.MaxValue -> wraps to short.MinValue
    [InlineData(-32769, 32767)]   // one below short.MinValue -> wraps to short.MaxValue
    public void Constructor_ValueOutsideShortRange_WrapsAsSignedShort(int input, short expected)
    {
        // Documents the actual (int -> short) cast behavior of the constructor.
        // World-size validation / wrap-around logic is expected to live
        // elsewhere and prevent this from ever happening in practice.
        ChunkCoord coord = new ChunkCoord(input, 0, 0, 0);

        Assert.Equal(expected, coord.X);
    }

    // ---------- Equals ----------

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        ChunkCoord a = new ChunkCoord(1, 2, 3, 0);
        ChunkCoord b = new ChunkCoord(1, 2, 3, 0);

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentX_ReturnsFalse()
    {
        ChunkCoord a = new ChunkCoord(1, 2, 3, 0);
        ChunkCoord b = new ChunkCoord(99, 2, 3, 0);

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentY_ReturnsFalse()
    {
        ChunkCoord a = new ChunkCoord(1, 2, 3, 0);
        ChunkCoord b = new ChunkCoord(1, 99, 3, 0);

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentZ_ReturnsFalse()
    {
        ChunkCoord a = new ChunkCoord(1, 2, 3, 0);
        ChunkCoord b = new ChunkCoord(1, 2, 99, 0);

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentLodLevel_ReturnsFalse()
    {
        ChunkCoord a = new ChunkCoord(1, 2, 3, 0);
        ChunkCoord b = new ChunkCoord(1, 2, 3, 5);

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_ObjectOverload_WithBoxedEqualChunkCoord_ReturnsTrue()
    {
        ChunkCoord a = new ChunkCoord(1, 2, 3, 0);
        object b = new ChunkCoord(1, 2, 3, 0);

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_ObjectOverload_WithDifferentType_ReturnsFalse()
    {
        ChunkCoord a = new ChunkCoord(1, 2, 3, 0);

        Assert.False(a.Equals("not a ChunkCoord"));
    }

    [Fact]
    public void Equals_ObjectOverload_WithNull_ReturnsFalse()
    {
        ChunkCoord a = new ChunkCoord(1, 2, 3, 0);

        Assert.False(a.Equals(null));
    }

    // ---------- GetHashCode ----------
    // We intentionally do NOT recompute HashCode.Combine(...) here, since that
    // would just duplicate the implementation. Instead we verify the contract
    // that actually matters for Dictionary/HashSet correctness.

    [Fact]
    public void GetHashCode_EqualObjects_ProduceSameHashCode()
    {
        ChunkCoord a = new ChunkCoord(5, 10, 15, 2);
        ChunkCoord b = new ChunkCoord(5, 10, 15, 2);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Theory]
    [InlineData(6, 10, 15, 2)] // X differs
    [InlineData(5, 11, 15, 2)] // Y differs
    [InlineData(5, 10, 16, 2)] // Z differs
    [InlineData(5, 10, 15, 3)] // LodLevel differs
    public void GetHashCode_DifferentObjects_ProduceDifferentHashCode(int x, int y, int z, byte lod)
    {
        // Not a hard guarantee for hashing in general, but confirms every
        // field actually participates in the hash (catches a forgotten field).
        ChunkCoord a = new ChunkCoord(5, 10, 15, 2);
        ChunkCoord b = new ChunkCoord(x, y, z, lod);

        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    // ---------- Operators ----------

    [Fact]
    public void EqualityOperator_SameValues_ReturnsTrue()
    {
        ChunkCoord a = new ChunkCoord(1, 2, 3, 0);
        ChunkCoord b = new ChunkCoord(1, 2, 3, 0);

        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void EqualityOperator_DifferentValues_ReturnsFalse()
    {
        ChunkCoord a = new ChunkCoord(1, 2, 3, 0);
        ChunkCoord b = new ChunkCoord(1, 2, 3, 1);

        Assert.False(a == b);
        Assert.True(a != b);
    }

    // ---------- ToString ----------

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        ChunkCoord coord = new ChunkCoord(1, -2, 3, 0);

        Assert.Equal("(1, -2, 3)", coord.ToString());
    }

    [Fact]
    public void ToString_DoesNotIncludeLodLevel()
    {
        // Documents current behavior: LodLevel is intentionally excluded
        // from ToString, even though it IS part of Equals/GetHashCode.
        ChunkCoord coord = new ChunkCoord(1, 2, 3, 99);

        Assert.DoesNotContain("99", coord.ToString());
    }

    // ---------- Collection usage ----------
    // The struct's doc comment states it's meant for use as a Dictionary key,
    // so this is worth verifying directly rather than trusting Equals/GetHashCode
    // tests alone to guarantee correct collection behavior.

    [Fact]
    public void UsedAsDictionaryKey_SeparateEqualInstance_RetrievesSameValue()
    {
        var dict = new Dictionary<ChunkCoord, string>();
        ChunkCoord key1 = new ChunkCoord(1, 2, 3, 0);
        ChunkCoord key2 = new ChunkCoord(1, 2, 3, 0);

        dict[key1] = "chunk data";

        Assert.True(dict.TryGetValue(key2, out var value));
        Assert.Equal("chunk data", value);
    }

    [Fact]
    public void UsedInHashSet_DuplicateCoordinatesAreNotAddedTwice()
    {
        var set = new HashSet<ChunkCoord>
        {
            new ChunkCoord(1, 2, 3, 0),
            new ChunkCoord(1, 2, 3, 0), // duplicate, same values
            new ChunkCoord(4, 5, 6, 0)
        };

        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void UsedAsDictionaryKey_DifferentLodLevel_IsTreatedAsDifferentKey()
    {
        // Same X/Y/Z but different LodLevel -> must be a distinct key,
        // since LodLevel is part of Equals/GetHashCode.
        var dict = new Dictionary<ChunkCoord, string>();
        ChunkCoord lowDetail = new ChunkCoord(1, 2, 3, 0);
        ChunkCoord highDetail = new ChunkCoord(1, 2, 3, 1);

        dict[lowDetail] = "low";
        dict[highDetail] = "high";

        Assert.Equal(2, dict.Count);
        Assert.Equal("low", dict[lowDetail]);
        Assert.Equal("high", dict[highDetail]);
    }
}