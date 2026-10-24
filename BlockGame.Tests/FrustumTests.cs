using System.Numerics;
using Basics.Game;
using Basics.Game.Graphics;
using Basics.Game.Utilities;
using Plane = Basics.Game.Graphics.Plane;

namespace BlockGame.Tests;

/// <summary>
/// Safety net for frustum culling, written so that the culling *algorithm* can be replaced later.
///
/// The tests fall into three groups with different strictness:
///
///  1. Contract tests (must hold for ANY correct algorithm): a chunk that contains a visible point must
///     never be culled, and a chunk that lies completely outside one frustum plane must always be culled.
///     A better algorithm may cull more of the chunks in between, never fewer than the second rule demands.
///
///  2. Scenario tests (hand-computed): fixed camera, fixed chunks, expected visible/culled. Every "visible"
///     case really intersects the frustum and every "culled" case is clearly outside, so an exact test
///     (not just the current plane/AABB one) gives the same answers.
///
///  3. Characterisation tests (pin down CURRENT behaviour; adjust them deliberately if you change it):
///     a chunk that merely touches a plane counts as visible, and an uninitialised frustum culls nothing.
///
/// Conventions: System.Numerics (row vectors, right handed, depth 0..1). Camera looks down -Z by default.
/// </summary>
public class FrustumTests
{
    private const float DegToRad = MathF.PI / 180f;

    // ---------- helpers ----------

    /// <summary>The same matrices the game builds in ShaderManager.Use, with configurable parameters.</summary>
    private static Frustum Build(Camera cam, float fovDeg = 60f, float aspect = 1f, float near = 0.1f, float far = 190f)
    {
        Matrix4x4 view = cam.GetViewMatrix();
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(fovDeg * DegToRad, aspect, near, far);
        return cam.CreateFrustum(view, proj);
    }

    /// <summary>Camera in the middle of chunk (0,0,0), looking down -Z.</summary>
    private static Camera StandardCamera() => new(new Vector3(8f, 8f, 8f));

    private static ChunkCoord ChunkAt(Vector3 p, int lod)
    {
        float size = 16 << lod;
        return new ChunkCoord(
            (int)MathF.Floor(p.X / size),
            (int)MathF.Floor(p.Y / size),
            (int)MathF.Floor(p.Z / size),
            (byte)lod);
    }

    private static void AssertVisibility(Frustum frustum, ChunkCoord chunk, bool expectedVisible, string because)
    {
        bool actual = frustum.IsInFrustum(chunk);
        string message = $"chunk {chunk} (lod {chunk.LodLevel}) {because}: expected {(expectedVisible ? "visible" : "culled")}";
        if (expectedVisible)
        {
            Assert.True(actual, message);
        }
        else
        {
            Assert.False(actual, message);
        }
    }

    private static float Rand(Random rng, float lo, float hi) => lo + rng.NextSingle() * (hi - lo);

    /// <summary>Random position and a random viewing direction that is not (nearly) parallel to world-up.</summary>
    private static Camera RandomCamera(Random rng, float positionRange)
    {
        var cam = new Camera(new Vector3(
            Rand(rng, -positionRange, positionRange),
            Rand(rng, -positionRange, positionRange),
            Rand(rng, -positionRange, positionRange)));

        Vector3 dir;
        do
        {
            dir = new Vector3(Rand(rng, -1f, 1f), Rand(rng, -1f, 1f), Rand(rng, -1f, 1f));
        } while (dir.LengthSquared() < 0.1f || MathF.Abs(Vector3.Normalize(dir).Y) > 0.95f);

        cam.Front = Vector3.Normalize(dir);
        return cam;
    }

    // =====================================================================
    // Plane
    // =====================================================================

    [Fact]
    public void Plane_Constructor_PrecomputesTheAbsoluteNormal()
    {
        var plane = new Plane(new Vector3(-1f, 2f, -3f), 4f);

        Assert.Equal(new Vector3(-1f, 2f, -3f), plane.Normal);
        Assert.Equal(new Vector3(1f, 2f, 3f), plane.AbsNormal);
        Assert.Equal(4f, plane.Distance);
    }

    [Fact]
    public void Plane_DistanceToPoint_IsTheSignedDotProductPlusOffset()
    {
        var plane = new Plane(Vector3.UnitY, -5f); // the plane y = 5, positive side above

        Assert.Equal(2f, plane.GetDistanceToPoint(new Vector3(3f, 7f, 9f)));
        Assert.Equal(0f, plane.GetDistanceToPoint(new Vector3(0f, 5f, 0f)));
        Assert.Equal(-4f, plane.GetDistanceToPoint(new Vector3(0f, 1f, 0f)));
    }

    // =====================================================================
    // Camera.CreateFrustum (plane extraction)
    // =====================================================================

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(90f, 0f)]
    [InlineData(-45f, 30f)]
    [InlineData(135f, -60f)]
    public void CreateFrustum_PlanesAreUnitLength_AndFaceInwards(float yawDelta, float pitchDelta)
    {
        var cam = StandardCamera();
        cam.Rotate(yawDelta, pitchDelta);

        Frustum f = Build(cam, fovDeg: 90f, aspect: 1f, near: 1f, far: 100f);

        foreach (Plane p in new[] { f.LeftFace, f.RightFace, f.BottomFace, f.TopFace, f.NearFace, f.FarFace })
        {
            Assert.Equal(1f, p.Normal.Length(), 4);
            Assert.Equal(Vector3.Abs(p.Normal), p.AbsNormal);
        }

        Assert.True(Vector3.Dot(f.NearFace.Normal, cam.Front) > 0.999f, "near plane normal should point along the view direction");
        Assert.True(Vector3.Dot(f.FarFace.Normal, cam.Front) < -0.999f, "far plane normal should point back at the camera");
        Assert.True(Vector3.Dot(f.LeftFace.Normal, cam.Right) > 0f, "left plane should point towards the right");
        Assert.True(Vector3.Dot(f.RightFace.Normal, cam.Right) < 0f, "right plane should point towards the left");
        Assert.True(Vector3.Dot(f.BottomFace.Normal, cam.Up) > 0f, "bottom plane should point up");
        Assert.True(Vector3.Dot(f.TopFace.Normal, cam.Up) < 0f, "top plane should point down");
    }

    [Fact]
    public void CreateFrustum_CameraLiesOnTheSidePlanes_AndBetweenNearAndFar()
    {
        var cam = StandardCamera();

        Frustum f = Build(cam, fovDeg: 90f, aspect: 1f, near: 1f, far: 100f);

        // The four side planes pass through the eye, the near plane is `near` in front of it,
        // and the far plane is `far` in front of it (positive = inside).
        Assert.Equal(0f, f.LeftFace.GetDistanceToPoint(cam.Position), 3);
        Assert.Equal(0f, f.RightFace.GetDistanceToPoint(cam.Position), 3);
        Assert.Equal(0f, f.BottomFace.GetDistanceToPoint(cam.Position), 3);
        Assert.Equal(0f, f.TopFace.GetDistanceToPoint(cam.Position), 3);
        Assert.Equal(-1f, f.NearFace.GetDistanceToPoint(cam.Position), 3);
        Assert.Equal(100f, f.FarFace.GetDistanceToPoint(cam.Position), 2);
    }

    [Fact]
    public void CreateFrustum_PointStraightAhead_IsInsideAllSixPlanes()
    {
        var cam = StandardCamera();
        Frustum f = Build(cam, fovDeg: 90f, aspect: 1f, near: 1f, far: 100f);
        Vector3 p = cam.Position + cam.Front * 50f;

        Assert.True(f.LeftFace.GetDistanceToPoint(p) > 0f);
        Assert.True(f.RightFace.GetDistanceToPoint(p) > 0f);
        Assert.True(f.BottomFace.GetDistanceToPoint(p) > 0f);
        Assert.True(f.TopFace.GetDistanceToPoint(p) > 0f);
        Assert.True(f.NearFace.GetDistanceToPoint(p) > 0f);
        Assert.True(f.FarFace.GetDistanceToPoint(p) > 0f);
    }

    [Fact]
    public void CreateFrustum_EachPlaneRejectsThePointsOutsideIt()
    {
        var cam = StandardCamera();
        Frustum f = Build(cam, fovDeg: 90f, aspect: 1f, near: 1f, far: 100f);
        Vector3 ahead = cam.Position + cam.Front * 10f;

        Assert.True(f.LeftFace.GetDistanceToPoint(ahead - cam.Right * 50f) < 0f);
        Assert.True(f.RightFace.GetDistanceToPoint(ahead + cam.Right * 50f) < 0f);
        Assert.True(f.BottomFace.GetDistanceToPoint(ahead - cam.Up * 50f) < 0f);
        Assert.True(f.TopFace.GetDistanceToPoint(ahead + cam.Up * 50f) < 0f);
        Assert.True(f.NearFace.GetDistanceToPoint(cam.Position - cam.Front * 5f) < 0f);
        Assert.True(f.FarFace.GetDistanceToPoint(cam.Position + cam.Front * 150f) < 0f);
    }

    // =====================================================================
    // Scenarios: camera at (8,8,8) looking down -Z, fov 60, aspect 1, near 0.1, far 190
    // (all expectations computed by hand; see the geometry notes in each group)
    // =====================================================================

    [Theory]
    [InlineData(0, 0, 0, true, "contains the camera")]
    [InlineData(0, 0, -1, true, "right in front of the camera")]
    [InlineData(0, 0, -3, true, "straight ahead")]
    [InlineData(0, 0, 1, false, "directly behind the camera")]
    [InlineData(0, 0, 3, false, "far behind the camera")]
    // sideways: at depth 56-72 the half-width of the view is roughly 32-41 units
    [InlineData(2, 0, -4, true, "straddles the right plane")]
    [InlineData(-2, 0, -4, true, "straddles the left plane")]
    [InlineData(4, 0, -4, false, "just outside the right plane")]
    [InlineData(-4, 0, -4, false, "just outside the left plane")]
    [InlineData(10, 0, -1, false, "far to the right")]
    [InlineData(-10, 0, -1, false, "far to the left")]
    // vertical: same half-height as half-width because the aspect is 1
    [InlineData(0, 3, -4, true, "straddles the top plane")]
    [InlineData(0, -3, -4, true, "straddles the bottom plane")]
    [InlineData(0, 5, -4, false, "just above the top plane")]
    [InlineData(0, -5, -4, false, "just below the bottom plane")]
    [InlineData(0, 10, -1, false, "far above")]
    [InlineData(0, -10, -1, false, "far below")]
    // far plane at depth 190: chunk z -192..-176 spans depth 184..200
    [InlineData(0, 0, -10, true, "well inside the far plane")]
    [InlineData(0, 0, -12, true, "straddles the far plane")]
    [InlineData(0, 0, -14, false, "beyond the far plane")]
    public void Scenario_StandardCamera(int cx, int cy, int cz, bool expectedVisible, string because)
    {
        Frustum f = Build(StandardCamera());

        AssertVisibility(f, new ChunkCoord(cx, cy, cz, 0), expectedVisible, because);
    }

    [Theory]
    [InlineData(2f, -4, 0, -4, true)]   // wider view: the chunk outside the left plane at aspect 1 comes into view
    [InlineData(2f, 0, 5, -4, false)]   // ... but the vertical extent is unchanged
    [InlineData(0.5f, -2, 0, -4, false)] // narrower view: a chunk that straddles the left plane at aspect 1 is gone
    [InlineData(0.5f, 0, 3, -4, true)]  // ... while the vertical extent is unchanged
    public void Scenario_AspectRatioOnlyChangesTheHorizontalExtent(float aspect, int cx, int cy, int cz, bool expectedVisible)
    {
        Frustum f = Build(StandardCamera(), aspect: aspect);

        AssertVisibility(f, new ChunkCoord(cx, cy, cz, 0), expectedVisible, $"with aspect {aspect}");
    }

    [Theory]
    [InlineData(30f, 0, 3, -4, false)]  // narrow fov culls the chunk that straddles the top plane at fov 60
    [InlineData(30f, 0, 0, -4, true)]
    [InlineData(120f, -4, 0, -4, true)] // wide fov sees the chunks that are outside at fov 60
    [InlineData(120f, 0, 5, -4, true)]
    [InlineData(120f, 0, 0, 3, false)]  // ... but never what is behind the camera
    public void Scenario_FieldOfViewWidensAndNarrowsTheFrustum(float fov, int cx, int cy, int cz, bool expectedVisible)
    {
        Frustum f = Build(StandardCamera(), fovDeg: fov);

        AssertVisibility(f, new ChunkCoord(cx, cy, cz, 0), expectedVisible, $"with fov {fov}");
    }

    [Theory]
    [InlineData(0.1f, 0, 0, -1, true)]   // depth 8..24
    [InlineData(50f, 0, 0, -1, false)]   // entirely nearer than the near plane
    [InlineData(50f, 0, 0, -3, true)]    // depth 40..56 straddles the near plane
    [InlineData(50f, 0, 0, -4, true)]    // depth 56..72 is past it
    public void Scenario_NearPlaneDistance(float near, int cx, int cy, int cz, bool expectedVisible)
    {
        Frustum f = Build(StandardCamera(), near: near);

        AssertVisibility(f, new ChunkCoord(cx, cy, cz, 0), expectedVisible, $"with near plane at {near}");
    }

    [Theory]
    [InlineData(-90f)] // default: looking down -Z
    [InlineData(0f)]   // +X
    [InlineData(90f)]  // +Z
    [InlineData(180f)] // -X
    public void Scenario_ChunksAheadAreVisibleAndChunksBehindAreCulled_ForEveryCompassDirection(float yawDegrees)
    {
        var cam = StandardCamera();
        cam.Rotate(yawDegrees - cam.Yaw, 0f);
        int dx = (int)MathF.Round(cam.Front.X);
        int dz = (int)MathF.Round(cam.Front.Z);
        Frustum f = Build(cam);

        AssertVisibility(f, new ChunkCoord(4 * dx, 0, 4 * dz, 0), true, $"4 chunks ahead at yaw {yawDegrees}");
        AssertVisibility(f, new ChunkCoord(-4 * dx, 0, -4 * dz, 0), false, $"4 chunks behind at yaw {yawDegrees}");
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(90f, 0f)]
    [InlineData(-45f, 30f)]
    [InlineData(135f, -60f)]
    [InlineData(200f, 15f)]
    public void Scenario_TheChunkInTheCentreOfTheViewIsVisible_AndTheOppositeOneIsCulled(float yawDelta, float pitchDelta)
    {
        var cam = StandardCamera();
        cam.Rotate(yawDelta, pitchDelta);
        Frustum f = Build(cam);

        AssertVisibility(f, ChunkAt(cam.Position + cam.Front * 64f, 0), true, "64 units straight ahead");
        AssertVisibility(f, ChunkAt(cam.Position - cam.Front * 64f, 0), false, "64 units straight behind");
    }

    // =====================================================================
    // Contract tests (must hold for any correct culling algorithm)
    // =====================================================================

    /// <summary>
    /// No false negatives: if a point is inside the view volume, the chunk that contains it must be
    /// visible, at every LOD. This is the property an optimised algorithm must never break.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EveryChunkContainingAPointInsideTheFrustum_IsVisible(int lod)
    {
        const float near = 0.1f;
        const float far = 500f;
        var rng = new Random(20261024);

        for (int pose = 0; pose < 60; pose++)
        {
            Camera cam = RandomCamera(rng, 500f);
            float fov = Rand(rng, 30f, 100f);
            float aspect = Rand(rng, 0.5f, 2.5f);
            Frustum f = Build(cam, fov, aspect, near, far);
            float tanY = MathF.Tan(fov * DegToRad / 2f);
            float tanX = tanY * aspect;

            for (int i = 0; i < 50; i++)
            {
                float depth = Rand(rng, 1f, far * 0.9f);
                float xn = Rand(rng, -0.9f, 0.9f);
                float yn = Rand(rng, -0.9f, 0.9f);
                Vector3 point = cam.Position
                                + cam.Right * (xn * depth * tanX)
                                + cam.Up * (yn * depth * tanY)
                                + cam.Front * depth;
                ChunkCoord chunk = ChunkAt(point, lod);

                Assert.True(f.IsInFrustum(chunk),
                    $"pose {pose}: the point {point} (depth {depth:F1}) is inside the frustum, so chunk {chunk} (lod {lod}) must be visible");
            }
        }
    }

    /// <summary>
    /// A chunk that is completely on the outside of a single frustum plane must be culled. The expectation is
    /// computed analytically for an unrotated camera (depth = camera.z - z), with a small safety margin, so it
    /// does not depend on how IsInFrustum is implemented.
    /// </summary>
    [Theory]
    [InlineData(60f, 1f, 0)]
    [InlineData(60f, 1f, 1)]
    [InlineData(60f, 1f, 2)]
    [InlineData(90f, 16f / 9f, 0)]
    [InlineData(30f, 0.5f, 0)]
    [InlineData(110f, 2.4f, 1)]
    public void ChunksCompletelyOutsideASinglePlane_AreAlwaysCulled(float fov, float aspect, int lod)
    {
        const float near = 0.5f;
        const float far = 190f;
        const double margin = 0.05;
        var cam = new Camera(new Vector3(13.5f, -4.25f, 21f));
        Frustum f = Build(cam, fov, aspect, near, far);
        double tanY = Math.Tan(fov * Math.PI / 360.0);
        double tanX = tanY * aspect;
        double size = 16 << lod;
        int range = 12 >> lod;
        int mustCull = 0;
        int visible = 0;

        for (int cx = -range; cx <= range; cx++)
        for (int cy = -range; cy <= range; cy++)
        for (int cz = -range; cz <= range; cz++)
        {
            double maxDepth = double.MinValue, minDepth = double.MaxValue;
            double maxLeft = double.MinValue, maxRight = double.MinValue;
            double maxBottom = double.MinValue, maxTop = double.MinValue;

            for (int corner = 0; corner < 8; corner++)
            {
                double dx = cx * size + (corner & 1) * size - cam.Position.X;
                double dy = cy * size + ((corner >> 1) & 1) * size - cam.Position.Y;
                double depth = cam.Position.Z - (cz * size + ((corner >> 2) & 1) * size);

                maxDepth = Math.Max(maxDepth, depth);
                minDepth = Math.Min(minDepth, depth);
                maxLeft = Math.Max(maxLeft, dx + tanX * depth);
                maxRight = Math.Max(maxRight, -dx + tanX * depth);
                maxBottom = Math.Max(maxBottom, dy + tanY * depth);
                maxTop = Math.Max(maxTop, -dy + tanY * depth);
            }

            var chunk = new ChunkCoord(cx, cy, cz, (byte)lod);
            bool outsideOnePlane = maxDepth < near - margin
                                   || minDepth > far + margin
                                   || maxLeft < -margin
                                   || maxRight < -margin
                                   || maxBottom < -margin
                                   || maxTop < -margin;

            if (outsideOnePlane)
            {
                mustCull++;
                Assert.False(f.IsInFrustum(chunk), $"chunk {chunk} (lod {lod}) is entirely outside a frustum plane and must be culled");
            }
            else if (f.IsInFrustum(chunk))
            {
                visible++;
            }
        }

        Assert.True(mustCull > 0, "the grid should contain chunks outside the frustum");
        Assert.True(visible > 0, "the grid should contain chunks inside the frustum");
    }

    /// <summary>
    /// A coarse chunk covers the space of its eight finer children, so it can never be culled while one of
    /// them is visible. This catches mistakes in the LOD scaling of the chunk bounds.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AChunkIsVisible_WheneverOneOfItsFinerChildrenIsVisible(int parentLod)
    {
        var rng = new Random(99 + parentLod);
        int range = 256 / (16 << parentLod);

        for (int pose = 0; pose < 10; pose++)
        {
            Camera cam = RandomCamera(rng, 200f);
            Frustum f = Build(cam, Rand(rng, 30f, 100f), Rand(rng, 0.5f, 2.5f), 0.1f, 300f);

            for (int x = -range; x <= range; x++)
            for (int y = -range; y <= range; y++)
            for (int z = -range; z <= range; z++)
            {
                bool anyChildVisible = false;
                for (int i = 0; i < 8 && !anyChildVisible; i++)
                {
                    var child = new ChunkCoord(2 * x + (i & 1), 2 * y + ((i >> 1) & 1), 2 * z + ((i >> 2) & 1), (byte)(parentLod - 1));
                    anyChildVisible = f.IsInFrustum(child);
                }

                if (anyChildVisible)
                {
                    var parent = new ChunkCoord(x, y, z, (byte)parentLod);
                    Assert.True(f.IsInFrustum(parent), $"pose {pose}: {parent} (lod {parentLod}) is culled although one of its lod {parentLod - 1} children is visible");
                }
            }
        }
    }

    [Fact]
    public void LodChunk_HasTwiceTheSizeAndTwiceTheOffset_OfTheSameCoordinateAtTheLevelBelow()
    {
        // Chunk (0,0,-2): at LOD 0 it spans z -32..-16, at LOD 1 it spans z -64..-32.
        // Camera at (8,8,8) looking down -Z, so the depths are 24..40 (LOD 0) and 40..72 (LOD 1).
        var lod0 = new ChunkCoord(0, 0, -2, 0);
        var lod1 = new ChunkCoord(0, 0, -2, 1);
        Frustum far45 = Build(StandardCamera(), far: 45f);
        Frustum far30 = Build(StandardCamera(), far: 30f);

        AssertVisibility(far45, lod0, true, "LOD 0, depth 24..40, far plane at 45");
        AssertVisibility(far45, lod1, true, "LOD 1, depth 40..72, reaches the far plane at 45");
        AssertVisibility(far30, lod0, true, "LOD 0, depth 24..40, straddles the far plane at 30");
        AssertVisibility(far30, lod1, false, "LOD 1 starts at depth 40, beyond the far plane at 30");
    }

    // =====================================================================
    // Synthetic frustums: six hand-placed planes, no camera maths involved.
    // Inside = distance >= 0 for all planes. This is a box 0..max on each axis.
    // =====================================================================

    private static Frustum BoxFrustum(float max) => new()
    {
        LeftFace = new Plane(Vector3.UnitX, 0f),
        RightFace = new Plane(-Vector3.UnitX, max),
        BottomFace = new Plane(Vector3.UnitY, 0f),
        TopFace = new Plane(-Vector3.UnitY, max),
        NearFace = new Plane(Vector3.UnitZ, 0f),
        FarFace = new Plane(-Vector3.UnitZ, max),
    };

    /// <summary>A plane that every nearby point is inside of.</summary>
    private static readonly Plane Open = new(Vector3.UnitY, 1_000_000f);

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 3, 3)]
    [InlineData(5, 5, 5)]
    public void BoxFrustum_ChunksFullyInside_AreVisible(int cx, int cy, int cz)
    {
        AssertVisibility(BoxFrustum(104f), new ChunkCoord(cx, cy, cz, 0), true, "inside the box");
    }

    [Theory]
    [InlineData(6, 0, 0, "straddles the right face")]
    [InlineData(0, 6, 0, "straddles the top face")]
    [InlineData(0, 0, 6, "straddles the far face")]
    public void BoxFrustum_ChunksStraddlingAFace_AreVisible(int cx, int cy, int cz, string because)
    {
        AssertVisibility(BoxFrustum(104f), new ChunkCoord(cx, cy, cz, 0), true, because);
    }

    [Theory]
    [InlineData(-2, 0, 0, "outside the left face")]
    [InlineData(7, 0, 0, "outside the right face")]
    [InlineData(0, -2, 0, "below the bottom face")]
    [InlineData(0, 7, 0, "above the top face")]
    [InlineData(0, 0, -2, "in front of the near face")]
    [InlineData(0, 0, 7, "behind the far face")]
    public void BoxFrustum_EachOfTheSixPlanesCullsOnItsOwn(int cx, int cy, int cz, string because)
    {
        // Guards against one plane being dropped from the test chain.
        AssertVisibility(BoxFrustum(104f), new ChunkCoord(cx, cy, cz, 0), false, because);
    }

    [Theory]
    [InlineData(-1, 0, 0, true, "touches the left face from outside")]
    [InlineData(6, 0, 0, true, "touches the right face from outside")]
    [InlineData(0, -1, 0, true, "touches the bottom face from outside")]
    [InlineData(0, 6, 0, true, "touches the top face from outside")]
    [InlineData(0, 0, -1, true, "touches the near face from outside")]
    [InlineData(0, 0, 6, true, "touches the far face from outside")]
    [InlineData(7, 0, 0, false, "one whole chunk beyond the right face")]
    public void BoxFrustum_ChunkJustTouchingAPlane_CountsAsVisible_CurrentBehaviour(int cx, int cy, int cz, bool expectedVisible, string because)
    {
        // Characterisation: the current test is "distance >= -radius", so touching is visible.
        // Changing this to a strict comparison is harmless (the chunk contributes no pixels) but
        // should be done on purpose; update these cases when you do.
        AssertVisibility(BoxFrustum(96f), new ChunkCoord(cx, cy, cz, 0), expectedVisible, because);
    }

    [Theory]
    [InlineData(0, 0, 0, 1, true, "LOD 1 chunk 0..32 inside")]
    [InlineData(2, 0, 0, 1, true, "LOD 1 chunk 64..96 touches the right face from inside")]
    [InlineData(3, 0, 0, 1, true, "LOD 1 chunk 96..128 touches the right face from outside")]
    [InlineData(4, 0, 0, 1, false, "LOD 1 chunk 128..160 is outside")]
    [InlineData(1, 0, 0, 2, true, "LOD 2 chunk 64..128 straddles the right face")]
    [InlineData(2, 0, 0, 2, false, "LOD 2 chunk 128..192 is outside")]
    [InlineData(-1, 0, 0, 2, true, "LOD 2 chunk -64..0 touches the left face")]
    [InlineData(-2, 0, 0, 2, false, "LOD 2 chunk -128..-64 is outside")]
    public void BoxFrustum_ChunkSizeAndOffsetScaleWithTheLodLevel(int cx, int cy, int cz, int lod, bool expectedVisible, string because)
    {
        AssertVisibility(BoxFrustum(96f), new ChunkCoord(cx, cy, cz, (byte)lod), expectedVisible, because);
    }

    [Theory]
    [InlineData(0, 1, 0, true)]   // centre distance -5.3, box radius 11.3: reaches across the plane
    [InlineData(1, 0, 0, true)]
    [InlineData(0, 2, 0, false)]  // centre distance -16.6 < -11.3
    [InlineData(0, 3, 0, false)]
    public void ObliquePlane_UsesTheAbsoluteNormalForTheBoxRadius(int cx, int cy, int cz, bool expectedVisible)
    {
        // Inside: (x - y) / sqrt(2) + 6 >= 0. The normal has a negative component, so using the signed
        // normal instead of AbsNormal for the radius would wrongly cull the (0,1,0) chunk.
        var frustum = new Frustum
        {
            LeftFace = new Plane(Vector3.Normalize(new Vector3(1f, -1f, 0f)), 6f),
            RightFace = Open,
            BottomFace = Open,
            TopFace = Open,
            NearFace = Open,
            FarFace = Open,
        };

        AssertVisibility(frustum, new ChunkCoord(cx, cy, cz, 0), expectedVisible, "against an oblique plane");
    }

    [Fact]
    public void DefaultFrustum_CullsNothing_CurrentBehaviour()
    {
        // An uninitialised frustum has zero-length planes. Currently that fails safe (everything is drawn).
        Frustum uninitialised = default;

        AssertVisibility(uninitialised, new ChunkCoord(0, 0, 0, 0), true, "origin");
        AssertVisibility(uninitialised, new ChunkCoord(-500, 300, 9000, 0), true, "far away");
    }
}
