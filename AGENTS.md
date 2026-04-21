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
  - `TerrainGenerator` and CPU-side meshing (`Lod0Mesher`) run on background worker threads via `Channel<T>` and `Parallel.For`.
  - **IMPORTANT:** ALL OpenGL/GPU interactions (`UploadToGpu`, `gl.DrawElements`, etc.) MUST run on the main thread. Meshes are queued to `UploadQueue` and uploaded by `Renderer.Render()` time-capped to ~2ms per frame.
- **Coordinate Systems**: The game strictly separates World Coordinates (`x, y, z`) and Chunk Grid Coordinates (`ChunkCoord`). A single chunk is 32x32x32 blocks. Use `ChunkData` helper methods (`WorldToChunkCoord`, `WorldToLocal`) to convert safely.
- **GC Pressure Mitigation**: We avoid allocations during meshing. Always use the object pools provided by `ChunkProvider` (`VramPool`, `VertexListPool`, `IndexListPool`) instead of instantiating new lists for vertex/index arrays.

## 🎨 Rendering Pipeline & Shader Quirks

- **No UV Data in Vertices**: We use `Texture2DArray` for block textures. UVs are computationally generated in `shader.vert` using `gl_VertexID % 4`. 
- **Vertex Layout**: When editing meshing, remember the layout is strictly 5 floats: `aPos` (x,y,z), `aLayer` (float, texture index), and `brightness` (float, 0.4-1.0 for cheap per-vertex Ambient Occlusion).
- **Meshing & AO Flow**: Adding new block geometries? You must correctly split quads based on the AO brightness gradient (`CalcAoLevel()`) to prevent interpolation artifacts across the diagonal.

## ⌨️ Input & UI Patterns

- **Action-Based Input**: Do not map raw Silk.NET inputs manually. Bind C# Actions to keys in `InputManager` (e.g., `InputManager.SetActionBindings(Actions.ToogleDebugCamera, callback)`).
- **GUI**: We use Egui.NET via custom integrations (`SilkGlIntegration`). Draw calls for UI should be bundled into `_uiIntegration.Run(ctx => Draw(ctx))` during the active state's Render pass.

