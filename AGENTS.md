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
- **GUI**: We use Egui.NET via `GameEguiIntegration` and the local Silk integration. Draw UI in `_uiIntegration.Run(ctx => Draw(ctx))` during the active state's Render pass.

