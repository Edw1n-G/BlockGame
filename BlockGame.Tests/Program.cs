using System;
using System.Diagnostics;
using Basics.Configurations;
using Basics.Game;
using Basics.Game.Content.Loading;
using Basics.Game.Content.Registries;
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

        try
        {
            TestPaletteIdsStableAcrossLoadOrders();
            Console.WriteLine("[PASS] TestPaletteIdsStableAcrossLoadOrders passed.\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] TestPaletteIdsStableAcrossLoadOrders failed: {ex}");
            Console.ResetColor();
            failed++;
        }

        try
        {
            TestPaletteRemapSurvivesLoadOrderChange();
            Console.WriteLine("[PASS] TestPaletteRemapSurvivesLoadOrderChange passed.\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] TestPaletteRemapSurvivesLoadOrderChange failed: {ex}");
            Console.ResetColor();
            failed++;
        }

        try
        {
            TestDuplicateBlockIdThrows();
            Console.WriteLine("[PASS] TestDuplicateBlockIdThrows passed.\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] TestDuplicateBlockIdThrows failed: {ex}");
            Console.ResetColor();
            failed++;
        }

        try
        {
            TestFailedBuildThrowsOriginalErrorOnRebuild();
            Console.WriteLine("[PASS] TestFailedBuildThrowsOriginalErrorOnRebuild passed.\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] TestFailedBuildThrowsOriginalErrorOnRebuild failed: {ex}");
            Console.ResetColor();
            failed++;
        }

        try
        {
            TestPaletteFreezesAfterBuild();
            Console.WriteLine("[PASS] TestPaletteFreezesAfterBuild passed.\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] TestPaletteFreezesAfterBuild failed: {ex}");
            Console.ResetColor();
            failed++;
        }

        try
        {
            TestDuplicateTextureNamesThrow();
            Console.WriteLine("[PASS] TestDuplicateTextureNamesThrow passed.\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] TestDuplicateTextureNamesThrow failed: {ex}");
            Console.ResetColor();
            failed++;
        }

        try
        {
            TestBlockJsonValidation();
            Console.WriteLine("[PASS] TestBlockJsonValidation passed.\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] TestBlockJsonValidation failed: {ex}");
            Console.ResetColor();
            failed++;
        }

        try
        {
            TestResourceIdParsing();
            Console.WriteLine("[PASS] TestResourceIdParsing passed.\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] TestResourceIdParsing failed: {ex}");
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

    /// <summary>
    /// Test 4: Palette-Stabilität über Load-Pfade und Entdeckungsreihenfolge hinweg.
    /// Verifies that the content path and the fallback path assign identical IDs for the same
    /// block names, and that a different enumeration order from the content source produces
    /// identical IDs (the loader sorts ordinal — die Entdeckungsreihenfolge darf die
    /// ID-Vergabe nicht beeinflussen).
    /// </summary>
    static void TestPaletteIdsStableAcrossLoadOrders()
    {
        Console.WriteLine("Running TestPaletteIdsStableAcrossLoadOrders...");

        string[] coreBlocks = { "core:air", "core:dirt", "core:grass", "core:snow", "core:stone" };

        // Registry mit echtem Content (FileSystem-Quelle: JSON-Definitionen werden geladen)
        var contentRegistry = ContentLoader.LoadBlocks(new FileSystemContentSource("Content"));

        // Zweite Registry aus demselben Content: IDs müssen identisch sein
        var contentRegistry2 = ContentLoader.LoadBlocks(new FileSystemContentSource("Content"));
        foreach (string name in coreBlocks)
        {
            Assert(contentRegistry.GetBlockId(name) == contentRegistry2.GetBlockId(name),
                $"Zwei Registries aus demselben Content müssen für '{name}' dieselbe ID vergeben");
        }

        // Registry ohne erreichbaren Content (Fallback-Definitionen)
        var fallbackRegistry = BlockRegistry.CreateFallback();

        // Beide Load-Pfade müssen für dieselben Namen dieselben IDs vergeben
        foreach (string name in coreBlocks)
        {
            Assert(contentRegistry.GetBlockId(name) == fallbackRegistry.GetBlockId(name),
                $"Block-ID für '{name}' muss in beiden Load-Pfaden identisch sein " +
                $"(content={contentRegistry.GetBlockId(name)}, fallback={fallbackRegistry.GetBlockId(name)})");
        }

        // Entdeckungsreihenfolge darf die IDs nicht beeinflussen: dieselben Blöcke bewusst
        // in umgekehrter ordinaler Reihenfolge aus einer In-Memory-Quelle ergeben dieselben IDs
        var reversedSource = new InMemoryContentSource();
        reversedSource.AddTexture("core:dirt", "dirt.png");
        reversedSource.AddTexture("core:grass", "grass.png");
        reversedSource.AddTexture("core:snow", "snow.png");
        reversedSource.AddTexture("core:stone", "stone.png");
        foreach (string name in new[] { "core:stone", "core:snow", "core:grass", "core:dirt" })
        {
            reversedSource.AddBlock(name, $"{{ \"textures\": {{ \"sides\": \"{name}\" }} }}", $"{name}.json");
        }

        var reversedRegistry = ContentLoader.LoadBlocks(reversedSource);
        foreach (string name in coreBlocks)
        {
            Assert(reversedRegistry.GetBlockId(name) == contentRegistry.GetBlockId(name),
                $"Block-ID für '{name}' darf nicht von der Entdeckungsreihenfolge abhängen");
        }

        // Palette-Rücklookup: Name → ID → Name
        foreach (string name in coreBlocks)
        {
            ushort id = contentRegistry.GetBlockId(name);
            Assert(contentRegistry.GetName(id) == name,
                $"GetName({id}) muss '{name}' zurückgeben, war '{contentRegistry.GetName(id)}'");
        }
    }

    /// <summary>
    /// Test 5: Palette-Remapping für gespeicherte Daten.
    /// Verifies that a saved palette from an older session (with different load order and an
    /// extension block) can be remapped onto the current registry, and that unknown blocks
    /// become air.
    /// </summary>
    static void TestPaletteRemapSurvivesLoadOrderChange()
    {
        Console.WriteLine("Running TestPaletteRemapSurvivesLoadOrderChange...");

        var registry = ContentLoader.LoadBlocks(new FileSystemContentSource("Content"));

        // Simulierte gespeicherte Palette einer älteren Sitzung: andere Reihenfolge
        // (stone vor snow) plus ein Mod-Block, den es aktuell nicht mehr gibt
        string[] savedPalette =
        {
            "core:air", "core:dirt", "core:grass", "core:stone", "core:snow", "oldmod:glowstone"
        };

        int[] remap = registry.CreateRemapTable(savedPalette);

        for (int i = 0; i < savedPalette.Length - 1; i++)
        {
            Assert(remap[i] == registry.GetBlockId(savedPalette[i]),
                $"Remap-Eintrag {i} ('{savedPalette[i]}') muss auf die aktuelle ID " +
                $"{registry.GetBlockId(savedPalette[i])} mappen, war {remap[i]}");
        }

        Assert(remap[5] == 0, "Unbekannte Blöcke ('oldmod:glowstone') müssen auf Luft (0) gemappt werden");

        // Chunk-Daten (alte IDs) auf die aktuelle Registry remappen
        ushort[] oldChunkBlocks = { 0, 1, 2, 3, 4, 5, 5, 0 };
        ushort[] expected = new ushort[oldChunkBlocks.Length];
        for (int i = 0; i < oldChunkBlocks.Length; i++)
        {
            expected[i] = oldChunkBlocks[i] < savedPalette.Length
                ? (ushort)remap[oldChunkBlocks[i]]
                : (ushort)0;
        }

        BlockRegistry.RemapBlockArray(oldChunkBlocks, remap);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert(oldChunkBlocks[i] == expected[i],
                $"Remappter Block an Position {i} muss {expected[i]} sein, war {oldChunkBlocks[i]}");
        }

        // Export: Index = ID, Wert = Name
        string[] exported = registry.ExportPalette();
        Assert(exported[registry.GetBlockId("core:stone")] == "core:stone",
            "ExportPalette muss am Index der Block-ID den Namen zurückgeben");
        Assert(exported[0] == "core:air", "ExportPalette[0] muss 'core:air' sein (hartes Invariant)");
    }

    /// <summary>
    /// Test 6: Doppelte Block-IDs.
    /// Verifies that two extensions providing the same block id make the palette load throw
    /// instead of silently merging, keeping the palette unique for the whole engine process.
    /// Verwendet eine In-Memory-Quelle — keine Dateien.
    /// </summary>
    static void TestDuplicateBlockIdThrows()
    {
        Console.WriteLine("Running TestDuplicateBlockIdThrows...");

        var source = new InMemoryContentSource();
        source.AddTexture("duptest:stone", "mod_a/Textures/stone.png");

        // Beide Erweiterungen liefern denselben Blocknamen → "duptest:stone" doppelt
        string json = "{ \"textures\": { \"sides\": \"duptest:stone\" } }";
        source.AddBlock("duptest:stone", json, "mod_a/Blocks/stone.json");
        source.AddBlock("duptest:stone", json, "mod_b/Blocks/stone.json");

        string? errorMessage = null;
        bool threw = false;
        try
        {
            ContentLoader.LoadBlocks(source);
        }
        catch (InvalidDataException ex)
        {
            threw = true;
            errorMessage = ex.Message;
        }

        Assert(threw, "Doppelte Blocknamen müssen eine InvalidDataException auslösen");
        Assert(errorMessage!.Contains("mod_a/Blocks/stone.json") && errorMessage.Contains("mod_b/Blocks/stone.json"),
            $"Fehlermeldung muss beide kollidierenden Dateien nennen: {errorMessage}");
    }

    /// <summary>
    /// Test 7: Fehlgeschlagener Build meldet den Originalfehler.
    /// Verifies that a failed load (unknown texture) reports the original descriptive error,
    /// and that rebuilding from the same source fails identically — jeder Build ist atomar,
    /// es gibt keinen halb-gefüllten Zustand, der einen Retry irreführend als
    /// "Doppelter Blockname" fehlschlagen lassen könnte.
    /// </summary>
    static void TestFailedBuildThrowsOriginalErrorOnRebuild()
    {
        Console.WriteLine("Running TestFailedBuildThrowsOriginalErrorOnRebuild...");

        var source = new InMemoryContentSource();
        source.AddBlock(
            "retrytest:broken",
            "{ \"textures\": { \"sides\": \"retrytest:doesnotexist\" } }",
            "retrytest/Blocks/broken.json");

        Exception? firstError = null;
        try
        {
            ContentLoader.LoadBlocks(source);
        }
        catch (Exception ex)
        {
            firstError = ex;
        }

        Assert(firstError is InvalidDataException,
            $"Der Build muss an der unbekannten Textur scheitern, war: {firstError}");
        Assert(firstError!.Message.Contains("Unbekannte Textur"),
            $"Originalfehler muss die unbekannte Textur nennen: {firstError.Message}");

        // Erneuter Build aus derselben Quelle: muss denselben Originalfehler melden —
        // jeder Build startet bei null, es gibt keinen Zustand, der ihn verfälschen könnte
        Exception? rebuildError = null;
        try
        {
            ContentLoader.LoadBlocks(source);
        }
        catch (Exception ex)
        {
            rebuildError = ex;
        }

        Assert(rebuildError is InvalidDataException,
            $"Erneuter Build muss denselben Content-Fehler melden, war: {rebuildError}");
        Assert(rebuildError!.Message == firstError.Message,
            $"Erneuter Build muss exakt denselben Originalfehler liefern:\n  erster: {firstError.Message}\n  retry:  {rebuildError.Message}");
        Assert(!rebuildError.Message.Contains("Doppelter"),
            "Erneuter Build darf nicht als Duplikat-Fehler fehlschlagen");
    }

    /// <summary>
    /// Test 8: Eingefrorene Palette nach dem Build.
    /// Verifies that the palette is frozen once the registry is built: der Builder lehnt
    /// weitere Blöcke mit einer klaren Fehlermeldung ab. Die Registry ist danach
    /// unveränderlich — es gibt keine Laufzeit-Registrierung mehr (früher Dispose-Wächter,
    /// jetzt strukturell garantiert, da der Builder die Palette nach dem Build einfriert).
    /// </summary>
    static void TestPaletteFreezesAfterBuild()
    {
        Console.WriteLine("Running TestPaletteFreezesAfterBuild...");

        var source = new InMemoryContentSource();
        // Core-Blöcke für die Validierung hinzufügen
        source.AddTexture("core:dirt", "dirt.png");
        source.AddTexture("core:grass", "grass.png");
        source.AddTexture("core:snow", "snow.png");
        source.AddTexture("core:stone", "stone.png");
        foreach (var name in new[] { "core:dirt", "core:grass", "core:snow", "core:stone" })
        {
            source.AddBlock(name, $"{{ \"textures\": {{ \"sides\": \"{name}\" }} }}", $"{name}.json");
        }

        // Test-Block für den Freeze-Test
        source.AddTexture("freezetest:stone", "stone.png");
        source.AddBlock(
            "freezetest:stone",
            "{ \"textures\": { \"sides\": \"freezetest:stone\" } }",
            "freezetest/stone.json");

        var registry = ContentLoader.LoadBlocks(source);
        Assert(registry.GetBlockId("freezetest:stone") > 0, "Registry muss nach dem Build funktionieren");

        // Zwei Builds aus denselben Eingaben ergeben dieselben IDs
        var source2 = new InMemoryContentSource();
        source2.AddTexture("core:dirt", "dirt.png");
        source2.AddTexture("core:grass", "grass.png");
        source2.AddTexture("core:snow", "snow.png");
        source2.AddTexture("core:stone", "stone.png");
        source2.AddTexture("freezetest:stone", "stone.png");
        foreach (var name in new[] { "core:dirt", "core:grass", "core:snow", "core:stone" })
        {
            source2.AddBlock(name, $"{{ \"textures\": {{ \"sides\": \"{name}\" }} }}", $"{name}.json");
        }
        source2.AddBlock(
            "freezetest:stone",
            "{ \"textures\": { \"sides\": \"freezetest:stone\" } }",
            "freezetest/stone.json");

        var registry2 = ContentLoader.LoadBlocks(source2);
        Assert(registry2.GetBlockId("freezetest:stone") == registry.GetBlockId("freezetest:stone"),
            "Zwei Builds aus denselben Eingaben müssen dieselbe ID vergeben");

        // Nach dem Build ist die Palette eingefroren: direkter Builder-Test
        var builder = new BlockRegistryBuilder()
            .UseTextures(registry.Textures)
            .ReserveAir();
        builder.AddBlock(
            ResourceId.Parse("freezetest:stone"),
            new BlockSpec { Faces = new BlockTextureFaces { Sides = "freezetest:stone" } },
            "stone.json");
        builder.Build();

        // Nach Build ist die Palette eingefroren
        AssertThrows<InvalidOperationException>(
            () => builder.AddBlock(
                ResourceId.Parse("freezetest:other"),
                new BlockSpec { Faces = new BlockTextureFaces { Sides = "freezetest:stone" } },
                "other.json"),
            "AddBlock nach Build muss an der eingefrorenen Palette scheitern");
    }

    /// <summary>
    /// Test 9: Doppelte Texturnamen.
    /// Verifies that two PNGs with the same name from different extensions make the texture
    /// load throw instead of silently assigning the last-sorted one.
    /// In-Memory-Quelle — keine Dateien.
    /// </summary>
    static void TestDuplicateTextureNamesThrow()
    {
        Console.WriteLine("Running TestDuplicateTextureNamesThrow...");

        var source = new InMemoryContentSource();
        source.AddTexture("duptex:stone", "pack_a/Textures/stone.png");
        source.AddTexture("duptex:stone", "pack_b/Textures/stone.png");

        string? errorMessage = null;
        bool threw = false;
        try
        {
            ContentLoader.LoadBlocks(source);
        }
        catch (InvalidDataException ex)
        {
            threw = true;
            errorMessage = ex.Message;
        }

        Assert(threw, "Doppelte Textur-IDs müssen eine InvalidDataException auslösen");
        Assert(errorMessage!.Contains("pack_a") && errorMessage.Contains("pack_b"),
            $"Fehlermeldung muss beide kollidierenden Texturen nennen: {errorMessage}");
    }

    /// <summary>
    /// Test 10: JSON-Validierung des BlockJsonParser — rein, mit String-Literalen.
    /// Verifies that a missing textures section and an unsupported format_version throw
    /// with descriptive errors naming the source file, and that format_version 1 parses.
    /// </summary>
    static void TestBlockJsonValidation()
    {
        Console.WriteLine("Running TestBlockJsonValidation...");

        // 1. Gültiges JSON inkl. format_version und Tags
        BlockSpec valid = BlockJsonParser.Parse(
            "{ \"format_version\": 1, \"name\": \"Stone\", \"tags\": [ \"Tag:Block\" ], " +
            "\"textures\": { \"top\": \"core:stone\", \"sides\": \"core:stone\" } }",
            "stone.json");
        Assert(valid.Tags.Count == 1 && valid.Tags[0] == "tag:block",
            "Tags müssen normalisiert (kleingeschrieben) werden");
        Assert(valid.Faces!.Top == "core:stone", "Faces.Top muss aus dem JSON gelesen werden");

        // 2. Fehlendes format_version → Default (unterstützt), damit alte Dateien funktionieren
        BlockSpec withoutVersion = BlockJsonParser.Parse(
            "{ \"textures\": { \"sides\": \"core:stone\" } }", "old.json");
        Assert(withoutVersion.Faces!.Sides == "core:stone",
            "JSON ohne format_version muss als Version 1 geladen werden");

        // 3. Nicht unterstütztes format_version
        AssertThrows<InvalidDataException>(
            () => BlockJsonParser.Parse("{ \"format_version\": 2, \"textures\": {} }", "future.json"),
            "format_version 2 muss abgelehnt werden");

        // 4. Fehlender textures-Abschnitt
        AssertThrows<InvalidDataException>(
            () => BlockJsonParser.Parse("{ \"format_version\": 1, \"name\": \"Broken\" }", "broken.json"),
            "Fehlender textures-Abschnitt muss abgelehnt werden");

        // 5. Ungültiges JSON
        AssertThrows<InvalidDataException>(
            () => BlockJsonParser.Parse("{ \"textures\": { \"sides\": ", "syntax.json"),
            "Ungültiges JSON muss eine InvalidDataException auslösen");

        // 6. Fehlermeldung nennt die Quelldatei
        string? message = null;
        try
        {
            BlockJsonParser.Parse("{ \"format_version\": 99, \"textures\": {} }", "version99.json");
        }
        catch (InvalidDataException ex)
        {
            message = ex.Message;
        }

        Assert(message!.Contains("version99.json"),
            $"Fehlermeldung muss die Quelldatei nennen: {message}");
    }

    /// <summary>
    /// Test 11: ResourceId-Validierung — Parsing, Normalisierung, Gleichheit.
    /// </summary>
    static void TestResourceIdParsing()
    {
        Console.WriteLine("Running TestResourceIdParsing...");

        ResourceId id = ResourceId.Parse("  CORE:Stone  ");
        Assert(id.FullName == "core:stone", "ResourceId muss normalisieren (Trim + Kleinbuchstaben)");
        Assert(id.Namespace == "core" && id.Name == "stone", "Namespace und Name müssen korrekt gesplittet werden");

        Assert(ResourceId.Parse("core:stone") == ResourceId.Parse("CORE:Stone"),
            "Gleichheit muss auf dem normalisierten Namen basieren");
        Assert(ResourceId.Parse("core:stone").GetHashCode() == ResourceId.Parse("CORE:Stone").GetHashCode(),
            "Gleiche IDs müssen denselben Hash haben");
        Assert(ResourceId.Parse("core:stone") != ResourceId.Parse("core:dirt"),
            "Verschiedene IDs dürfen nicht gleich sein");

        Assert(!ResourceId.TryParse("stoneWithoutNamespace", out _), "Fehlender Namespace muss abgelehnt werden");
        Assert(!ResourceId.TryParse(":stone", out _), "Leerer Namespace muss abgelehnt werden");
        Assert(!ResourceId.TryParse("core:", out _), "Leerer Name muss abgelehnt werden");
        Assert(!ResourceId.TryParse("core:stone:extra", out _), "Zweiter Doppelpunkt muss abgelehnt werden");
        Assert(!ResourceId.TryParse("", out _), "Leere Zeichenkette muss abgelehnt werden");

        // Ungültige Zeichen
        Assert(!ResourceId.TryParse("core:stone with space", out _), "Leerzeichen im Namen muss abgelehnt werden");
        Assert(!ResourceId.TryParse("core:stone/slash", out _), "Slash im Namen muss abgelehnt werden");

        AssertThrows<FormatException>(
            () => ResourceId.Parse("noNamespace", "some/path.json"),
            "Parse muss bei fehlendem Namespace werfen");

        // default(ResourceId) muss sicher sein
        ResourceId defaultId = default;
        Assert(defaultId.IsDefault, "default(ResourceId) muss IsDefault == true haben");
        Assert(defaultId.FullName == string.Empty, "default(ResourceId).FullName muss string.Empty sein");
        Assert(defaultId.GetHashCode() == 0, "default(ResourceId).GetHashCode() muss 0 sein");
        Assert(defaultId.ToString() == string.Empty, "default(ResourceId).ToString() muss string.Empty sein");
        AssertThrows<InvalidOperationException>(
            () => { var ns = defaultId.Namespace; },
            "default(ResourceId).Namespace muss InvalidOperationException werfen");
        AssertThrows<InvalidOperationException>(
            () => { var name = defaultId.Name; },
            "default(ResourceId).Name muss InvalidOperationException werfen");
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

    static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new Exception($"Assertion failed: {message} — falscher Fehlertyp: {ex.GetType().Name}: {ex.Message}");
        }

        throw new Exception($"Assertion failed: {message} — es wurde keine Exception geworfen");
    }
}

/// <summary>
/// In-Memory-<see cref="IContentSource"/> für Tests: keine Dateien, kein Fenster, keine GPU.
/// Die Einträge werden in der Reihenfolge der Aufrufe geliefert, dadurch lässt sich
/// gezielt testen, dass die ID-Vergabe unabhängig von der Entdeckungsreihenfolge ist.
/// </summary>
sealed class InMemoryContentSource : IContentSource
{
    private readonly List<ContentEntry> _entries = new();

    /// <summary>Fügt eine Textur hinzu (nur ID + Pfad — Texturen werden nie gelesen).</summary>
    public void AddTexture(string id, string sourcePath)
    {
        _entries.Add(new ContentEntry
        {
            Id = ResourceId.Parse(id),
            SourcePath = sourcePath,
            Kind = ContentKind.Texture,
            ReadText = () => string.Empty,
        });
    }

    /// <summary>Fügt einen Block als JSON-String hinzu.</summary>
    public void AddBlock(string id, string json, string sourcePath)
    {
        _entries.Add(new ContentEntry
        {
            Id = ResourceId.Parse(id),
            SourcePath = sourcePath,
            Kind = ContentKind.Block,
            ReadText = () => json,
        });
    }

    public IEnumerable<ContentEntry> Enumerate(ContentKind kind)
    {
        return _entries.Where(entry => entry.Kind == kind);
    }
}
