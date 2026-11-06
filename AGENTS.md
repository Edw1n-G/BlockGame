# AI Agent Instructions for EdwinCraft

This document provides essential context and guidelines for AI coding agents working on the EdwinCraft (Voxel Rendering Engine) codebase.

## 🏗️ Architecture & Boundaries

- **Deferred State Machine**: The app's lifecycle is driven by `StateManager`. Heavy systems live inside discrete states (`IStates` like `Menu.cs`, `Game.cs`). Do not perform immediate state changes; use `StateChange` to schedule it for the end of the frame.
- **Global Static Façades**: Most subsystem interactions should use the centralized static APIs configured in `Game.Enter()`:
  - `World.ModifyBlock()` / `World.GetBlock()` for terrain changes.
  - `Physics.Raycast()` for DDA voxel raycasting.
  - `InputManager` for event bindings and state polling.
  - `GameSettings` for global configuration values.

## 🌍 Terrain Generation & Threading

- **Strict Threading Model**:
  - `TerrainGenerator` and CPU-side meshing (`Lod0Mesher`) run on `JobScheduler` worker threads. `ChunkRequestor` also uses a background task.
  - **IMPORTANT:** ALL OpenGL/GPU interactions (`UploadToGpu`, `gl.DrawElements`, etc.) MUST run on the main thread. Meshes are queued to `UploadQueue` and uploaded by `Renderer.Render()` with a roughly 5 ms frame budget.
- **Coordinate Systems**: The game strictly separates World Coordinates (`x, y, z`) and Chunk Grid Coordinates (`ChunkCoord`). A single chunk is 16x16x16 blocks. Use `ChunkData` helper methods (`WorldToChunkCoord`, `WorldToLocal`) to convert safely.
- **GC Pressure Mitigation**: We avoid allocations during meshing. Always use the object pools provided by `ChunkProvider` (`VramPool`, `VertexListPool`, `IndexListPool`) instead of instantiating new lists for vertex/index arrays.

## 🎨 Rendering Pipeline & Shader Quirks

- **No UV Data in Vertices**: We use `Texture2DArray` for block textures. UVs are computationally generated in `shader.vert` using `gl_VertexID % 4`. 
- **Vertex Layout**: Each vertex is 16 bytes: three position floats, a `ushort` texture layer, a byte AO level (0–3), and one padding byte. The shader maps AO levels to brightness.
- **Meshing & AO Flow**: Adding new block geometries? You must correctly split quads based on the AO brightness gradient (`CalcAoLevel()`) to prevent interpolation artifacts across the diagonal.

## ⌨️ Input & UI Patterns

- **Action-Based Input**: Do not map raw Silk.NET inputs manually. Bind C# Actions to keys in `InputManager` (e.g., `inputManager.SetActionBindings(Actions.ToggleDebugCamera, callback)`).
- **GUI**: We use Egui.NET via `GameEguiIntegration` and the package Silk integration. Draw UI in `_uiIntegration.Run(ctx => Draw(ctx))` during the active state's Render pass.

## Verification and performance work

- `ChunkProvider` delegates lifetime, mesh requests, block access, neighbors and resources to instance-owned collaborators. Keep these internals behind provider APIs. `ChunkSelection.Calculate` is pure selection; `ChunkRequestor` retains asynchronous movement/reset/event/stop ownership.
- Use PowerShell 7: `./verify-tests.ps1 -Repeats 20 -ParallelThreads 4` for repeated correctness; `./run-benchmarks.ps1 -Mode Smoke` for all generated Release benchmark cases. The measurement wrapper also runs Debug/Release correctness first. Failures, skipped tests and zero discovery stop execution.
- Actual CPU measurements use `./run-benchmarks.ps1 -Mode Measure -OutputDirectory out/benchmarks/<unique-run>`. Preserve raw bundles, workload versions, source/native/content identities and environment/load notes. Smoke/test durations are not performance baselines. Review repeated same-machine measurements before judging impact; automated Git-revision comparison is a separate milestone.
- See `BlockGame.Benchmarks/README.md` and `BENCHMARK_MILESTONE_DRAFT.md` for measured operation boundaries, limitations and current gate evidence. GPU calls remain on the main thread.
- Chunk requestor movement uses reusable column-interval differences. Preserve the invariant that its committed selection region describes its active set: canceled/failed plans roll back additions; explicit unload invalidates the region. Test rapid notifications, reset, resize, provider failure and coordinate wraparound when changing this path.
- `BENCHMARK_WORKER_TRIAL.md` records a completed selection optimization experiment and pipeline friction. Use multiple `-Case` patterns to select reference/candidate families together; distinguish fresh-set calls from warmed closed movement paths and include complete operation units when reporting results.

