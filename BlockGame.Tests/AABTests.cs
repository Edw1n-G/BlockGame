using Basics.PhysicsSystem.Structs;

namespace BlockGame.Tests;

public class AabbTests
{
    private static AABB Box(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        => new AABB { MinX = minX, MinY = minY, MinZ = minZ, MaxX = maxX, MaxY = maxY, MaxZ = maxZ };

    // ---------- Clear overlap ----------

    [Fact]
    public void Intersects_OverlappingBoxes_ReturnsTrue()
    {
        var a = Box(0, 0, 0, 5, 5, 5);
        var b = Box(3, 3, 3, 8, 8, 8);

        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void Intersects_IdenticalBoxes_ReturnsTrue()
    {
        var a = Box(0, 0, 0, 5, 5, 5);
        var b = Box(0, 0, 0, 5, 5, 5);

        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void Intersects_OneBoxFullyInsideAnother_ReturnsTrue()
    {
        var outer = Box(0, 0, 0, 10, 10, 10);
        var inner = Box(2, 2, 2, 4, 4, 4);

        Assert.True(outer.Intersects(inner));
        Assert.True(inner.Intersects(outer)); // should be symmetric
    }

    // ---------- Clear separation, per axis ----------
    // Testing each axis independently matters: a bug where only one axis
    // check is wrong would still pass a "generic" separated-box test if the
    // other two axes happen to already report separation.

    [Fact]
    public void Intersects_SeparatedOnXAxis_ReturnsFalse()
    {
        var a = Box(0, 0, 0, 2, 2, 2);
        var b = Box(10, 0, 0, 12, 2, 2);

        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void Intersects_SeparatedOnYAxis_ReturnsFalse()
    {
        var a = Box(0, 0, 0, 2, 2, 2);
        var b = Box(0, 10, 0, 2, 12, 2);

        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void Intersects_SeparatedOnZAxis_ReturnsFalse()
    {
        var a = Box(0, 0, 0, 2, 2, 2);
        var b = Box(0, 0, 10, 2, 2, 12);

        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void Intersects_OverlappingOnTwoAxesButSeparatedOnThird_ReturnsFalse()
    {
        // Overlaps on X and Y, but clearly separated on Z.
        // Verifies all three axes are actually required (AND, not OR).
        var a = Box(0, 0, 0, 5, 5, 5);
        var b = Box(1, 1, 10, 4, 4, 15);

        Assert.False(a.Intersects(b));
    }

    // ---------- Touching boundaries (strict inequality behavior) ----------
    // This is the most important edge case in the whole class. The
    // implementation uses strict "<" / ">", meaning boxes that only touch
    // at a face/edge/corner do NOT count as intersecting. This is a real
    // design decision (common in physics engines to avoid "sticky" contact
    // states) and deserves an explicit test so it's never "fixed" by accident.

    [Fact]
    public void Intersects_BoxesTouchingExactlyAtFace_ReturnsFalse()
    {
        var a = Box(0, 0, 0, 5, 5, 5);
        var b = Box(5, 0, 0, 10, 5, 5); // a.MaxX == b.MinX exactly

        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void Intersects_BoxesTouchingExactlyAtCorner_ReturnsFalse()
    {
        var a = Box(0, 0, 0, 5, 5, 5);
        var b = Box(5, 5, 5, 10, 10, 10); // touching at a single corner point

        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void Intersects_BoxesOverlappingByTinyAmount_ReturnsTrue()
    {
        // Sanity check right on the other side of the boundary above -
        // confirms the strict inequality flips correctly just past the edge.
        var a = Box(0, 0, 0, 5, 5, 5);
        var b = Box(4.999f, 0, 0, 10, 5, 5);

        Assert.True(a.Intersects(b));
    }

    // ---------- Negative coordinates ----------

    [Fact]
    public void Intersects_BothBoxesInNegativeSpace_WorksCorrectly()
    {
        var a = Box(-10, -10, -10, -5, -5, -5);
        var b = Box(-7, -7, -7, -3, -3, -3);

        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void Intersects_BoxesStraddlingOrigin_ReturnsTrue()
    {
        var a = Box(-2, -2, -2, 2, 2, 2);
        var b = Box(-1, -1, -1, 1, 1, 1);

        Assert.True(a.Intersects(b));
    }

    // ---------- Symmetry ----------
    // Intersects isn't guaranteed to be symmetric by the type system, so
    // worth confirming explicitly since physics code will likely call it
    // in both orders depending on which object initiates the check.

    [Theory]
    [InlineData(0, 0, 0, 5, 5, 5, 3, 3, 3, 8, 8, 8, true)]
    [InlineData(0, 0, 0, 2, 2, 2, 10, 10, 10, 12, 12, 12, false)]
    public void Intersects_IsSymmetric(
        float minXa, float minYa, float minZa, float maxXa, float maxYa, float maxZa,
        float minXb, float minYb, float minZb, float maxXb, float maxYb, float maxZb,
        bool expected)
    {
        var a = Box(minXa, minYa, minZa, maxXa, maxYa, maxZa);
        var b = Box(minXb, minYb, minZb, maxXb, maxYb, maxZb);

        Assert.Equal(expected, a.Intersects(b));
        Assert.Equal(expected, b.Intersects(a));
    }
}