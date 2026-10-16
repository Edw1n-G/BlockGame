using System;
using System.Diagnostics;
using Basics.Configurations;
using Basics.Game;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Generation;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Basics.Game.PhysicsSystem;
using Basics.Game.Utilities;
using Silk.NET.Maths;

namespace BlockGame.Tests;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("=== EDWINCRAFT ENGINE ISOLATION & BENCH TEST ===");
        Console.WriteLine("=================================================\n");

        int failed = 0;

        try
        {
            TestIsolatedSessionsSideBySide();
            Console.WriteLine("[PASS] TestIsolatedSessionsSideBySide passed.\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] TestIsolatedSessionsSideBySide failed: {ex}");
            Console.ResetColor();
            failed++;
        }

        try
        {
            TestScopedStaticFacades();
            Console.WriteLine("[PASS] TestScopedStaticFacades passed.\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] TestScopedStaticFacades failed: {ex}");
            Console.ResetColor();
            failed++;
        }

        try
        {
            TestFiftyBenchmarkIterationsLoop();
            Console.WriteLine("[PASS] TestFiftyBenchmarkIterationsLoop passed.\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] TestFiftyBenchmarkIterationsLoop failed: {ex}");
            Console.ResetColor();
            failed++;
        }

        if (failed == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(">>> ALL TESTS PASSED SUCCESSFULLY! <<<");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($">>> {failed} TESTS FAILED! <<<");
            Console.ResetColor();
            return 1;
        }
    }

    /// <summary>
    /// Test 1: Run two isolated engine sessions in the same process concurrently.
    /// Verifies that ChunkProvider, World, Physics, GameSettings, and pools are completely isolated.
    /// </summary>
    static void TestIsolatedSessionsSideBySide()
    {
        Console.WriteLine("Running TestIsolatedSessionsSideBySide...");

        var settingsA = new GameSettings { Seed = 101, RenderDistance = 12, MapSize = 50 };
        var settingsB = new GameSettings { Seed = 999, RenderDistance = 24, MapSize = 200 };

        using var sessionA = new EngineSession(settingsA);
        using var sessionB = new EngineSession(settingsB);

        // 1. Verify settings isolation
        Assert(sessionA.Settings.Seed == 101, "Session A seed must be 101");
        Assert(sessionB.Settings.Seed == 999, "Session B seed must be 999");
        Assert(sessionA.Settings.RenderDistance == 12, "Session A render distance must be 12");
        Assert(sessionB.Settings.RenderDistance == 24, "Session B render distance must be 24");

        // 2. Generate chunks in both sessions at coord (0, 0, 0, 0)
        var coord = new ChunkCoord(0, 0, 0, 0);
        var chunkA = sessionA.TerrainGenerator.GenerateChunk(coord);
        var chunkB = sessionB.TerrainGenerator.GenerateChunk(coord);

        sessionA.ChunkProvider.Chunkdata[coord] = chunkA;
        sessionB.ChunkProvider.Chunkdata[coord] = chunkB;

        // 3. World block modification isolation
        // In session A, set block at (5, 5, 5) to 42
        bool modifiedA = sessionA.World.SetBlock(5, 5, 5, 42);
        Assert(modifiedA, "Session A block modification should succeed");
        Assert(sessionA.World.GetBlockAt(5, 5, 5) == 42, "Session A should read modified block 42");

        // Verify Session B was NOT affected at all
        ushort blockInB = sessionB.World.GetBlockAt(5, 5, 5);
        Assert(blockInB != 42, $"Session B must not be affected by Session A! Was {blockInB}");

        // Modify in session B to 99
        bool modifiedB = sessionB.World.SetBlock(5, 5, 5, 99);
        Assert(modifiedB, "Session B block modification should succeed");
        Assert(sessionB.World.GetBlockAt(5, 5, 5) == 99, "Session B should read modified block 99");
        Assert(sessionA.World.GetBlockAt(5, 5, 5) == 42, "Session A should still have block 42");

        // 4. Physics raycast isolation
        var rayStart = new Vector3D<float>(5.0f, 10.0f, 5.0f);
        var rayDir = new Vector3D<float>(0.0f, -1.0f, 0.0f);

        var hitA = sessionA.Physics.CastRay(rayStart, rayDir, 10.0f);
        var hitB = sessionB.Physics.CastRay(rayStart, rayDir, 10.0f);

        Assert(hitA.Hit, "Session A raycast should hit block at (5, 5, 5)");
        Assert(hitB.Hit, "Session B raycast should hit block at (5, 5, 5)");

        // 5. Memory pool isolation
        sessionA.ChunkProvider.VertexListPool.Enqueue(new List<byte> { 1, 2, 3 });
        Assert(sessionA.ChunkProvider.VertexListPool.Count == 1, "Session A should have 1 item in VertexListPool");
        Assert(sessionB.ChunkProvider.VertexListPool.Count == 0, "Session B VertexListPool must be empty");

        sessionB.ChunkProvider.IndexListPool.Enqueue(new List<uint> { 10, 20 });
        Assert(sessionA.ChunkProvider.IndexListPool.Count == 0, "Session A IndexListPool must be empty");
        Assert(sessionB.ChunkProvider.IndexListPool.Count == 1, "Session B should have 1 item in IndexListPool");
    }

    /// <summary>
    /// Test 2: Scoped static facades.
    /// Verifies that World.ModifyBlock, World.GetBlock, Physics.Raycast, GameSettings.Current
    /// delegate to the active EngineContext when activated via using (session.Activate()).
    /// </summary>
    static void TestScopedStaticFacades()
    {
        Console.WriteLine("Running TestScopedStaticFacades...");

        var settingsA = new GameSettings { Seed = 555, RenderDistance = 8 };
        var settingsB = new GameSettings { Seed = 777, RenderDistance = 32 };

        using var sessionA = new EngineSession(settingsA);
        using var sessionB = new EngineSession(settingsB);

        var coord = new ChunkCoord(0, 0, 0, 0);
        sessionA.ChunkProvider.Chunkdata[coord] = sessionA.TerrainGenerator.GenerateChunk(coord);
        sessionB.ChunkProvider.Chunkdata[coord] = sessionB.TerrainGenerator.GenerateChunk(coord);

        // Scope A
        using (sessionA.Activate())
        {
            Assert(GameSettings.Current.Seed == 555, "Scoped GameSettings.Current should be 555 in Session A");
            Assert(GameSettings.Current.RenderDistance == 8, "Scoped RenderDistance should be 8 in Session A");

            World.ModifyBlock(2, 2, 2, 77);
            Assert(World.GetBlock(2, 2, 2) == 77, "Static facade World.GetBlock should return 77 in Session A");
            Assert(sessionA.World.GetBlockAt(2, 2, 2) == 77, "Session A instance should have block 77");
            Assert(sessionB.World.GetBlockAt(2, 2, 2) != 77, "Session B instance must not be modified");

            var hit = Physics.Raycast(new Vector3D<float>(2.0f, 10.0f, 2.0f), new Vector3D<float>(0, -1, 0), 10.0f);
            Assert(hit.Hit, "Physics.Raycast static facade should hit in Session A");
        }

        // Scope B
        using (sessionB.Activate())
        {
            Assert(GameSettings.Current.Seed == 777, "Scoped GameSettings.Current should be 777 in Session B");
            Assert(GameSettings.Current.RenderDistance == 32, "Scoped RenderDistance should be 32 in Session B");

            Assert(World.GetBlock(2, 2, 2) != 77, "Static facade World.GetBlock in Session B must not have Session A's block");
            World.ModifyBlock(2, 2, 2, 88);
            Assert(World.GetBlock(2, 2, 2) == 88, "Static facade World.GetBlock should return 88 in Session B");
            Assert(sessionB.World.GetBlockAt(2, 2, 2) == 88, "Session B instance should have block 88");
            Assert(sessionA.World.GetBlockAt(2, 2, 2) == 77, "Session A instance must still be 77");
        }
    }

    /// <summary>
    /// Test 3: 50 benchmark iterations in a loop without process restart.
    /// Verifies throughput and zero state leakage between iterations.
    /// </summary>
    static void TestFiftyBenchmarkIterationsLoop()
    {
        Console.WriteLine("Running TestFiftyBenchmarkIterationsLoop (50 iterations)...");

        var sw = Stopwatch.StartNew();
        const int iterations = 50;

        for (int i = 0; i < iterations; i++)
        {
            var settings = new GameSettings
            {
                Seed = i * 10 + 1,
                MapSize = 100,
                RenderDistance = 10
            };

            using var session = new EngineSession(settings);
            using (session.Activate())
            {
                // Verify pristine initial state: zero chunks, zero queued meshes, clean pools
                Assert(session.ChunkProvider.Chunkdata.Count == 0,
                    $"Iteration {i}: Initial Chunkdata must be empty, but had {session.ChunkProvider.Chunkdata.Count}");
                Assert(session.ChunkProvider.LoadedChunks.Count == 0,
                    $"Iteration {i}: Initial LoadedChunks must be empty");
                Assert(session.ChunkProvider.UploadQueue.Count == 0,
                    $"Iteration {i}: Initial UploadQueue must be empty");
                Assert(session.ChunkProvider.PendingMeshRequests.Count == 0,
                    $"Iteration {i}: Initial PendingMeshRequests must be empty");

                // Generate and store several chunks
                for (int cx = -1; cx <= 1; cx++)
                {
                    for (int cz = -1; cz <= 1; cz++)
                    {
                        var c = new ChunkCoord(cx, 0, cz, 0);
                        var chunkData = session.TerrainGenerator.GenerateChunk(c);
                        session.ChunkProvider.Chunkdata[c] = chunkData;
                    }
                }

                Assert(session.ChunkProvider.Chunkdata.Count == 9,
                    $"Iteration {i}: Should have 9 chunks generated");

                // Perform meshing on center chunk
                var centerCoord = new ChunkCoord(0, 0, 0, 0);
                var mesher = new Lod0Mesher(centerCoord, session.ChunkProvider.Chunkdata[centerCoord], session.ChunkProvider);

                // Modify blocks via World facade
                World.ModifyBlock(0, 5, 0, 1);
                Assert(World.GetBlock(0, 5, 0) == 1, $"Iteration {i}: Modified block should read 1");

                // Free mesher
                mesher.Dispose();
            }

            // session is disposed here: ChunkProvider.Dispose() clears everything
        }

        sw.Stop();
        Console.WriteLine($"Completed {iterations} benchmark iterations in {sw.ElapsedMilliseconds} ms (avg {sw.ElapsedMilliseconds / (double)iterations:F2} ms/iter).");
    }

    static void TestConcurrentSessionsWithDifferentRegistriesMeshCorrectly()
    {
        var settingsA = new GameSettings { Seed = 1 };
        var settingsB = new GameSettings { Seed = 2 };

        using var sessionA = new EngineSession(settingsA, startScheduler: true);
        using var sessionB = new EngineSession(settingsB, startScheduler: true);

        var coord = new ChunkCoord(0, 0, 0, 0);
        sessionA.ChunkProvider.Chunkdata[coord] = sessionA.TerrainGenerator.GenerateChunk(coord);
        sessionB.ChunkProvider.Chunkdata[coord] = sessionB.TerrainGenerator.GenerateChunk(coord);

        sessionA.ChunkProvider.PendingMeshRequests.Enqueue(coord);
        sessionB.ChunkProvider.PendingMeshRequests.Enqueue(coord);
        sessionA.ChunkProvider.RequestMeshes();
        sessionB.ChunkProvider.RequestMeshes();

        // give worker threads time to finish, then assert both UploadQueues
        // produced a mesh without throwing or cross-resolving the wrong registry
        Thread.Sleep(200);
        Assert(sessionA.ChunkProvider.UploadQueue.Count + sessionA.ChunkProvider.LoadedChunks.Count > 0,
            "Session A should have meshed via worker thread");
        Assert(sessionB.ChunkProvider.UploadQueue.Count + sessionB.ChunkProvider.LoadedChunks.Count > 0,
            "Session B should have meshed via worker thread");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }
}
