using System.Numerics;
using Basics.Game.Utilities;
using Silk.NET.Maths;

namespace BlockGame.Tests;

public class MathHelperTests
{
    // ---------- DegreesToRadians ----------

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(180f, 3.14159265f)]
    [InlineData(360f, 6.28318531f)]
    [InlineData(90f, 1.57079633f)]
    [InlineData(-90f, -1.57079633f)]
    public void DegreesToRadians_KnownValues_ReturnsExpectedRadians(float degrees, float expectedRadians)
    {
        var result = MathHelper.DegreesToRadians(degrees);

        Assert.Equal(expectedRadians, result, precision: 5);
    }

    [Fact]
    public void DegreesToRadians_Zero_ReturnsZero()
    {
        var result = MathHelper.DegreesToRadians(0f);

        Assert.Equal(0f, result);
    }

    // ---------- ToNumerics ----------

    [Fact]
    public void ToNumerics_ConvertsComponentsCorrectly()
    {
        var input = new Vector3D<float>(1f, 2f, 3f);

        var result = MathHelper.ToNumerics(input);

        Assert.Equal(1f, result.X);
        Assert.Equal(2f, result.Y);
        Assert.Equal(3f, result.Z);
    }

    [Fact]
    public void ToNumerics_ZeroVector_ReturnsZeroVector()
    {
        var input = new Vector3D<float>(0f, 0f, 0f);

        var result = MathHelper.ToNumerics(input);

        Assert.Equal(Vector3.Zero, result);
    }

    [Fact]
    public void ToNumerics_NegativeValues_ConvertsCorrectly()
    {
        var input = new Vector3D<float>(-1f, -2f, -3f);

        var result = MathHelper.ToNumerics(input);

        Assert.Equal(-1f, result.X);
        Assert.Equal(-2f, result.Y);
        Assert.Equal(-3f, result.Z);
    }

    // ---------- ToGeneric ----------

    [Fact]
    public void ToGeneric_ConvertsComponentsCorrectly()
    {
        var input = new Vector3(1f, 2f, 3f);

        var result = MathHelper.ToGeneric(input);

        Assert.Equal(1f, result.X);
        Assert.Equal(2f, result.Y);
        Assert.Equal(3f, result.Z);
    }

    // ---------- Round-trip ----------

    [Theory]
    [InlineData(1f, 2f, 3f)]
    [InlineData(0f, 0f, 0f)]
    [InlineData(-5f, 10f, -15f)]
    public void ToGeneric_ThenToNumerics_RoundTripsCorrectly(float x, float y, float z)
    {
        var original = new Vector3(x, y, z);

        var converted = MathHelper.ToGeneric(original);
        var roundTripped = MathHelper.ToNumerics(converted);

        Assert.Equal(original, roundTripped);
    }
}