using Basics.Game;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.PhysicsSystem;
using Basics.Game.Utilities;
using Silk.NET.Maths;

namespace BlockGame.Tests;

/// <summary>
/// Collection that xUnit runs on its own, never in parallel with other tests. The static facades
/// (<c>World.Current</c>, <c>Physics.Current</c>, <c>GameSettings.Current</c>, <c>EngineContext.SetGlobal</c>)
/// fall back to process-wide statics, so tests that touch them must not overlap with other work.
/// </summary>
[CollectionDefinition("StaticFacades", DisableParallelization = true)]
public class StaticFacadesCollection;

[Collection("StaticFacades")]
public class ScopedFacadeTests
{
    private static readonly ChunkCoord Origin = new(0, 0, 0, 0);

    private static EngineSession NewSession(int seed, int renderDistance = 8)
    {
        var session = new EngineSession(new GameSettings { Seed = seed, RenderDistance = renderDistance });
        session.ChunkProvider.Chunkdata[Origin] = new ChunkData(Origin);
        return session;
    }

    [Fact]
    public void Activate_RoutesGameSettingsCurrent()
    {
        using var a = NewSession(555, 8);
        using var b = NewSession(777, 32);

        using (a.Activate())
        {
            Assert.Equal(555, GameSettings.Current.Seed);
            Assert.Equal(8, GameSettings.Current.RenderDistance);
        }

        using (b.Activate())
        {
            Assert.Equal(777, GameSettings.Current.Seed);
            Assert.Equal(32, GameSettings.Current.RenderDistance);
        }
    }

    [Fact]
    public void Activate_RoutesWorldFacade_ToTheActiveSessionOnly()
    {
        using var a = NewSession(1);
        using var b = NewSession(2);
        ushort stone = a.BlockRegistry.GetBlockId("core:stone");
        ushort dirt = b.BlockRegistry.GetBlockId("core:dirt");

        using (a.Activate())
        {
            World.ModifyBlock(2, 2, 2, stone);
            Assert.Equal(stone, World.GetBlock(2, 2, 2));
        }

        Assert.Equal(stone, a.World.GetBlockAt(2, 2, 2));
        Assert.Equal((ushort)0, b.World.GetBlockAt(2, 2, 2));

        using (b.Activate())
        {
            Assert.Equal((ushort)0, World.GetBlock(2, 2, 2)); // must not see A's block
            World.ModifyBlock(2, 2, 2, dirt);
            Assert.Equal(dirt, World.GetBlock(2, 2, 2));
        }

        Assert.Equal(stone, a.World.GetBlockAt(2, 2, 2)); // still A's value
        Assert.Equal(dirt, b.World.GetBlockAt(2, 2, 2));
    }

    [Fact]
    public void Activate_RoutesPhysicsFacade()
    {
        using var a = NewSession(1);
        using var b = NewSession(1);
        a.World.SetBlock(2, 2, 2, a.BlockRegistry.GetBlockId("core:stone"));
        var start = new Vector3D<float>(2.5f, 10.5f, 2.5f);
        var down = new Vector3D<float>(0f, -1f, 0f);

        using (a.Activate())
        {
            Assert.True(Physics.Raycast(start, down, 10f).Hit);
        }

        using (b.Activate())
        {
            Assert.False(Physics.Raycast(start, down, 10f).Hit);
        }
    }

    [Fact]
    public void Activate_NestedScopes_RestoreThePreviousContext()
    {
        using var a = NewSession(1);
        using var b = NewSession(2);
        EngineContext? before = EngineContext.Current;

        using (a.Activate())
        {
            Assert.Same(a.Context, EngineContext.Current);

            using (b.Activate())
            {
                Assert.Same(b.Context, EngineContext.Current);
            }

            Assert.Same(a.Context, EngineContext.Current);
        }

        Assert.Same(before, EngineContext.Current);
    }

    [Fact]
    public async Task Activate_FlowsAcrossAwait()
    {
        using var a = NewSession(1);

        using (a.Activate())
        {
            await Task.Yield();
            await Task.Delay(1, TestContext.Current.CancellationToken);

            Assert.Same(a.Context, EngineContext.Current);
        }
    }

    [Fact]
    public async Task Activate_ConcurrentTasks_DoNotSeeEachOthersContext()
    {
        const int tasks = 8;

        int[] seeds = await Task.WhenAll(Enumerable.Range(1, tasks).Select(seed => Task.Run(async () =>
        {
            using var session = NewSession(seed);
            using (session.Activate())
            {
                await Task.Yield();
                return GameSettings.Current.Seed;
            }
        }, TestContext.Current.CancellationToken)));

        Assert.Equal(Enumerable.Range(1, tasks), seeds);
    }

    [Fact]
    public async Task SetGlobal_IsTheFallbackForThreadsWithoutAScope()
    {
        using var a = NewSession(42);
        try
        {
            EngineContext.SetGlobal(a.Context);

            EngineContext? seenOnWorker = await Task.Run(() => EngineContext.Current, TestContext.Current.CancellationToken);

            Assert.Same(a.Context, seenOnWorker);
        }
        finally
        {
            EngineContext.SetGlobal(null);
        }
    }

    [Fact]
    public void ScopedContext_WinsOverTheGlobalOne()
    {
        using var global = NewSession(1);
        using var scoped = NewSession(2);
        try
        {
            EngineContext.SetGlobal(global.Context);

            using (scoped.Activate())
            {
                Assert.Same(scoped.Context, EngineContext.Current);
            }

            Assert.Same(global.Context, EngineContext.Current);
        }
        finally
        {
            EngineContext.SetGlobal(null);
        }
    }

    [Fact]
    public void Dispose_UnregistersTheContext_FromCurrent()
    {
        var session = NewSession(1);
        try
        {
            EngineContext.Current = session.Context;

            session.Dispose();

            Assert.NotSame(session.Context, EngineContext.Current);
        }
        finally
        {
            EngineContext.Current = null;
        }
    }

    [Fact]
    public void Dispose_UnregistersTheContext_AsGlobal()
    {
        var session = NewSession(1);
        try
        {
            EngineContext.SetGlobal(session.Context);

            session.Dispose();

            Assert.NotSame(session.Context, EngineContext.Current);
        }
        finally
        {
            EngineContext.SetGlobal(null);
        }
    }
}
