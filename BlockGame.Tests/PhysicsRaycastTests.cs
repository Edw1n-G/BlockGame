using Basics.Game.Utilities;
using Basics.PhysicsSystem.Structs;
using Silk.NET.Maths;

namespace BlockGame.Tests;

/// <summary>
/// Voxel raycast (Amanatides-Woo). The normal points back along the ray, i.e. it is the face that was entered.
/// </summary>
public class PhysicsRaycastTests : EngineTestBase
{
    private static Vector3D<float> V(float x, float y, float z) => new(x, y, z);
    private static Vector3D<int> I(int x, int y, int z) => new(x, y, z);

    private BlockResult Cast(Vector3D<float> start, Vector3D<float> dir, float maxDistance)
        => Session.Physics.CastRay(start, dir, maxDistance);

    [Fact]
    public void Ray_Downwards_HitsTheBlockBelow_WithAnUpwardsNormal()
    {
        PlaceBlock(5, 5, 5, Stone);

        BlockResult hit = Cast(V(5.5f, 10.5f, 5.5f), V(0, -1, 0), 10f);

        Assert.True(hit.Hit);
        Assert.Equal(I(5, 5, 5), hit.HitPosition);
        Assert.Equal(I(0, 1, 0), hit.HitNormal);
    }

    [Fact]
    public void Ray_Sideways_HitsTheFirstBlock_WithANormalFacingTheRay()
    {
        PlaceBlock(8, 5, 5, Stone);
        PlaceBlock(9, 5, 5, Stone); // behind the first one

        BlockResult hit = Cast(V(2.5f, 5.5f, 5.5f), V(1, 0, 0), 20f);

        Assert.True(hit.Hit);
        Assert.Equal(I(8, 5, 5), hit.HitPosition);
        Assert.Equal(I(-1, 0, 0), hit.HitNormal);
    }

    [Fact]
    public void Ray_AlongZ_ReportsAZNormal()
    {
        PlaceBlock(5, 5, 9, Stone);

        BlockResult hit = Cast(V(5.5f, 5.5f, 2.5f), V(0, 0, 1), 20f);

        Assert.True(hit.Hit);
        Assert.Equal(I(5, 5, 9), hit.HitPosition);
        Assert.Equal(I(0, 0, -1), hit.HitNormal);
    }

    [Fact]
    public void Ray_InNegativeDirection_HitsTheBlock_WithAPositiveNormal()
    {
        PlaceBlock(-3, 5, 5, Stone); // lives in chunk (-1, 0, 0)

        BlockResult hit = Cast(V(2.5f, 5.5f, 5.5f), V(-1, 0, 0), 20f);

        Assert.True(hit.Hit);
        Assert.Equal(I(-3, 5, 5), hit.HitPosition);
        Assert.Equal(I(1, 0, 0), hit.HitNormal);
    }

    [Fact]
    public void Ray_CrossingAChunkBorder_FindsTheBlockInTheNeighbourChunk()
    {
        PlaceBlock(20, 5, 5, Stone); // chunk (1, 0, 0)

        BlockResult hit = Cast(V(2.5f, 5.5f, 5.5f), V(1, 0, 0), 30f);

        Assert.True(hit.Hit);
        Assert.Equal(I(20, 5, 5), hit.HitPosition);
    }

    [Fact]
    public void Ray_DiagonalDirection_IsNormalisedBeforeUse()
    {
        PlaceBlock(8, 8, 5, Stone);

        // The direction vector is not unit length; the hit must not depend on its magnitude.
        BlockResult hit = Cast(V(5.5f, 5.5f, 5.5f), V(30, 30, 0), 10f);

        Assert.True(hit.Hit);
    }

    [Fact]
    public void Ray_BeyondMaxDistance_Misses()
    {
        PlaceBlock(5, 5, 5, Stone);

        BlockResult hit = Cast(V(5.5f, 10.5f, 5.5f), V(0, -1, 0), 2f);

        Assert.False(hit.Hit);
    }

    [Fact]
    public void Ray_ThroughAnEmptyWorld_Misses()
    {
        BlockResult hit = Cast(V(0.5f, 0.5f, 0.5f), V(1, 0, 0), 50f);

        Assert.False(hit.Hit);
    }

    [Fact]
    public void Ray_ThroughLoadedButEmptyChunks_Misses()
    {
        LoadEmptyCube(1);

        BlockResult hit = Cast(V(0.5f, 0.5f, 0.5f), V(0, 1, 0), 20f);

        Assert.False(hit.Hit);
    }

    [Fact]
    public void Ray_StartingInsideASolidBlock_HitsItImmediately()
    {
        PlaceBlock(5, 5, 5, Stone);

        BlockResult hit = Cast(V(5.5f, 5.5f, 5.5f), V(1, 0, 0), 10f);

        Assert.True(hit.Hit);
        Assert.Equal(I(5, 5, 5), hit.HitPosition);
        Assert.Equal(I(0, 0, 0), hit.HitNormal); // no face was entered
    }

    [Fact]
    public void Ray_IgnoresBlocksThatHaveBeenRemoved()
    {
        AddChunk(new ChunkCoord(0, 0, 0, 0));
        Session.World.SetBlock(5, 5, 5, Stone);
        Assert.True(Cast(V(5.5f, 10.5f, 5.5f), V(0, -1, 0), 10f).Hit);

        Session.World.SetBlock(5, 5, 5, 0);

        Assert.False(Cast(V(5.5f, 10.5f, 5.5f), V(0, -1, 0), 10f).Hit);
    }

    [Fact]
    public void RaycasterAndPhysics_AgreeOnTheResult()
    {
        PlaceBlock(5, 5, 5, Stone);
        var start = V(5.5f, 10.5f, 5.5f);
        var down = V(0, -1, 0);

        BlockResult viaPhysics = Session.Physics.CastRay(start, down, 10f);
        BlockResult viaRaycaster = Session.Raycaster.CastBlockRay(start, down, 10f);

        Assert.Equal(viaRaycaster.Hit, viaPhysics.Hit);
        Assert.Equal(viaRaycaster.HitPosition, viaPhysics.HitPosition);
        Assert.Equal(viaRaycaster.HitNormal, viaPhysics.HitNormal);
    }
}
