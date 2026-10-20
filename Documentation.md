# EdwinCraft (BlockGame) Documentation

## 1. Overview

EdwinCraft (solution `Rendering.sln`, project `BlockGame`, root namespace `Basics`) is a voxel terrain engine/game written in C# on Silk.NET (OpenGL 4.6) with an Egui.NET UI layer. It streams cubic 16×16×16 chunks around the player, generates terrain from 3D FastNoise2 density fields on background threads, meshes chunks with per-vertex ambient occlusion, and lets the player break and place blocks via voxel raycasting.

| Topic | Value |
|---|---|
| Language / runtime | C#, `net10.0`, nullable + implicit usings enabled, unsafe code allowed |
| Windowing / input / GL | Silk.NET 2.23.0 (`Windowing`, `Input`, `OpenGL`) |
| UI | `Egui.Silk.NET` + `Egui.Silk.NET.OpenGL` 0.7.0 (with a custom GL integration, see §7) |
| Noise | FastNoise2 via native `FastNoise.dll` (P/Invoke wrapper `FastNoise2.cs`) |
| Images | StbImageSharp (textures), SixLabors.ImageSharp (debug noise export), System.Drawing.Common |
| Chunk size | 16×16×16 blocks (`ChunkData.ChunkSize`) |
| World bounds | X/Z limited by `GameSettings.MapSize` (in chunks, default 100); Y unbounded |
| Chunk model | "Cubic chunks": chunks stack in Y as well as X/Z |
| Platform notes | Bundled native DLLs (`FastNoise.dll`, `NodeEditorIpc.dll`) are Windows binaries. DPI scaling code (`GetDpiForWindow`) is Windows-only but guarded by a Win32 handle check. |

## 2. Quick start

Requirements: .NET 10 SDK, a GPU/driver with OpenGL 4.6, the native DLLs next to the executable (the csproj copies them).

```
dotnet restore BlockGame/BlockGame.csproj
dotnet run --project BlockGame/BlockGame.csproj
```

The working directory must allow `Content/` to be found. `BlockRegistry.ResolveContentRoot` tries `./Content`, `<AppBase>/Content`, `<cwd>/BlockGame/Content`, `<cwd>/../BlockGame/Content`. The csproj also copies `Content/**/*.json` and `*.png` and the shaders to the output directory. Shaders are loaded from `Game/Graphics/Shader/` relative to the working directory.

Tests / benchmarks (a console `Exe`, not a test framework):

```
dotnet run --project BlockGame.Tests/BlockGame.Tests.csproj
```

### Controls (defaults from `InputManager`)

| Input | Action |
|---|---|
| W / A / S / D | Move (horizontal, relative to look direction) |
| Space / Left Shift | Up / Down |
| Mouse | Look (mouse is locked/raw when the game starts) |
| Left / Right mouse | Destroy block / place block (reach 5 blocks) |
| F | Toggle mouse lock (needed to use Egui windows in game) |
| F1 | Toggle debug free camera |
| F11 / F12 | Fullscreen / borderless fullscreen |
| Esc | Close window |

Placing always places `core:dirt`. There is no block selection or inventory.

## 3. Repository layout

```
Rendering.sln
README.md, Documentation.md, AGENTS.md
BlockGame/                         main project (Exe)
├─ Programm.cs                     entry point (class "Programm") → StateManager.Run()
├─ SilkIntegration.cs              Egui ↔ Silk.NET input/platform base class
├─ SilkGlIntegration.cs            Egui OpenGL renderer (custom)
├─ FastNoise.dll, NodeEditorIpc.dll  native libs (copied to output)
├─ EngineStates/                   StateManager, IStates, Menu, Game
├─ Window/WindowSetup.cs           window creation, fullscreen/borderless, fps/vsync hooks
├─ Game/
│  ├─ EngineContext.cs, EngineSession.cs   subsystem container + isolated session
│  ├─ Configurations/              BlockRegistry (data-driven blocks), BlockLoader (static facade)
│  ├─ Graphics/                    Renderer, ShaderManager, Shader, Frustum, Transform,
│  │                               BufferObject, VertexArrayObject, UI/UIManager, Shader/*.vert|frag
│  ├─ Input/InputManager.cs        action bindings
│  ├─ Jobs/                        JobScheduler, IJob, JobContext, ChunkLoadorGenerateJob, MeshgenerateJob
│  ├─ Logic/
│  │  ├─ GameSettings.cs, GameLogic.cs (empty placeholder)
│  │  ├─ Player/                   PlayerCharacter, Camera, PlayerMovement
│  │  └─ TerrainManaging/          ChunkProvider, ChunkRequestor, ChunkData, World, PooledMeshBuffer
│  │     ├─ Generation/            TerrainGenerator
│  │     │  └─ Noise/              NoiseCalculator, WorldtypeNoiseCalculator, FastNoise2.cs (wrapper)
│  │     └─ Meshing/               BaseMesher, Lod0Mesher, LOD1Mesher, LOD2Mesher, LOD3Mesher (stub)
│  ├─ PhysicsSystem/               Physics, Raycaster, Structs/ (BlockResult, EntityResult, AABB)
│  ├─ Utilities/                   ChunkCoord, CoreAvailability, MathHelper
│  └─ texture/                     Texture, TextureArray
└─ Content/
   └─ Core/                        namespace "core"
      ├─ Blocks/stupid/*.json      dirt, grass, snow, stone
      ├─ Textures/*.png            one PNG per texture (all must share one size)
      ├─ WorldTypes/               1.txt (encoded noise tree), overworld/world_def.json (empty)
      └─ images/me.png             menu portrait (embedded resource)
BlockGame.Tests/                   console test/benchmark runner (Program.cs)
```

Namespaces do not always mirror folders. Examples: jobs live in `Basics.Game.Utilities`, `PlayerMovement` in `Basics.Game.Player`, `PlayerCharacter` in `Basics.Game.Logic.Player`, the physics structs in `Basics.PhysicsSystem.Structs`, input in `Basics.Input`, `Renderer`/`Frustum` in `Basics.Game.Graphics`, `Shader` in `Basics.Graphics`. Source comments and XML docs are mostly in German.

## 4. Runtime architecture

### 4.1 State machine and lifecycle

- `Programm.Main` → `new StateManager().Run()`.
- `StateManager.Run` calls `WindowSetup.CreateWindow()`, hooks `Load/Render/Update/FramebufferResize`, runs the window loop, then disposes the window.
- `onLoad` creates the GL and input contexts and enters `Menu`.
- `StateManager.StateChange(IStates)` only stores `_nextState`; the swap (`Exit()` of old, `Enter()` of new) happens at the end of the next `onUpdate`. Never switch states immediately.
- `IStates`: `Enter(GL, IInputContext, StateManager)`, `Update`, `Render`, `FramebufferResize`, `Exit`.
- `Menu`: Egui main menu (title "MeinKraft", start / settings / quit), a settings window (render distance, world size), a controls hint box and the embedded portrait image. "Spiel starten" calls `StateChange(new Game())`.
- `Game`: owns the gameplay loop (see 4.2). `Game.Exit()` disposes the `EngineContext`.
- `CloseEngine()` currently only disposes the GL object; it does not close the window.

### 4.2 Game state initialization (`Game.Enter`)

1. Creates `EngineContext(settings: GameSettings.Current)` and registers it with `EngineContext.SetGlobal`.
2. `CoreAvailability.InitializeInstance()` and `BlockRegistry.Initialize(gl, "Content")`, which loads textures into a `TextureArray` and block JSONs.
3. Creates `PlayerCharacter` at (0, 100, 0), `Renderer` (+ `Setup`), `PlayerMovement`.
4. Creates the Egui `Context` and `SilkGlIntegration`.
5. `InputManager.Initialize` (locks the mouse), binds `PlayerMovement` and the debug camera toggle.
6. Creates `ChunkRequestor` (subscribes to `PlayerCharacter.OnChunkChanged`).
7. Starts the `JobScheduler` with `CoreAvailability.GetCores()` workers and a `JobContext`.
8. Creates `UIManager`, then `_player.ForceChunkUpdate()` to trigger the first chunk request.

Per frame: `Update` → `PlayerMovement.MovementUpdate` and `ChunkProvider.RequestMeshes()`; `Render` → `Renderer.Clear/Render`, depth test off, then Egui via `SilkIntegration.Run`. Many fields in `Game` and `Renderer` are `static`, so only one game instance is expected at a time.

### 4.3 EngineContext and EngineSession

`EngineContext` holds all subsystems for one engine instance: `Settings`, `CoreAvailability`, `BlockRegistry`, `NoiseCalculator`, `TerrainGenerator`, `JobScheduler`, `ChunkProvider`, `Raycaster`, `Physics`, `World`. If the registry is not initialized, the constructor calls `InitializeHeadless()` (block definitions only, no GPU). Optionally it starts the scheduler (`startJobScheduler`).

- `EngineContext.Current` resolves to the `AsyncLocal` scoped context (`Activate()` / `Use()` return an `IDisposable` scope), falling back to the global context set by `SetGlobal`.
- `EngineSession` wraps a context for isolated runs (benchmarks, parallel A/B sessions). It exposes the subsystems and `Activate()`.
- Static facades delegate to `Current`: `World.ModifyBlock/GetBlock`, `Physics.Raycast`, `GameSettings.Current`, `BlockLoader.*`, `CoreAvailability.Current`, `ChunkProvider.Current`, `NoiseCalculator.Instance`, `WorldtypeNoiseCalculator.Instance`. Most have a lazily created fallback if no context exists.
- Subsystem constructors take optional dependencies and fall back to `EngineContext.Current` and then to the facades. Prefer passing dependencies explicitly in new code.
- `Dispose` stops the scheduler and disposes `ChunkProvider`, `NoiseCalculator` and `BlockRegistry`.

### 4.4 Coordinate systems and storage

- World coordinates: ints/floats `x, y, z` in blocks. Chunk coordinates: `ChunkCoord(short X, short Y, short Z, byte LodLevel)`, a 7-byte struct usable as a dictionary key.
- `ChunkData.Blocks` is `ushort[4096]` (or `null` = all air) indexed `x * 256 + y * 16 + z` (`ChunkData.ToIndex`).
- Helpers: `ChunkData.WorldToChunkCoord` (floor division), `ChunkData.WorldToLocal` (positive modulo). `Camera.GetChunkCoord` always returns LOD 0.
- Block ID 0 is air. Block IDs are `ushort`.
- LOD scale: a chunk at LOD *n* covers `16 * 2^n` world blocks per axis; the mesh model matrix scales by `2^n` and translates by `chunk * 16 * 2^n`.

### 4.5 Terrain pipeline (streaming → generation → meshing → upload)

1. `PlayerCharacter.Move` raises `OnChunkChanged` when the player enters a new chunk (also on `ForceChunkUpdate`, which is bound to `UIManager.OnRenderDistanceChanged`).
2. `ChunkRequestor` (on a `Task.Run`, coalescing updates) computes an ellipsoid of LOD-0 chunks around the player: `(x²+z²)/RenderDistance² + y²/VerticalRenderDistance² ≤ 1`. It calls `ChunkProvider.RequestChunk` for new ones, then unloads chunks that left the set. `UnloadAllChunks()` clears everything (UI button).
3. `RequestChunk` skips chunks already in `LoadedChunks`/`Chunkdata`, otherwise enqueues `ChunkLoadorGenerateJob` on the low-priority queue.
4. `ChunkLoadorGenerateJob` tries loading from disk (placeholder, always fails), otherwise `TerrainGenerator.GenerateChunk`, stores the result in `Chunkdata`, and calls `OnChunkDataGenerated`. That tries to queue the chunk and its 26 neighbors for meshing.
5. A chunk is queued only if it exists and all 26 neighbors exist in `Chunkdata` (so AO and face culling across borders are correct). `QueuedForMeshing` de-duplicates and `PendingMeshRequests` holds the coordinates. Chunks beyond `MapSize` are generated as empty chunks so neighbors can still mesh.
6. `Game.Update` → `ChunkProvider.RequestMeshes()` moves pending requests to the scheduler as `MeshgenerateJob`s while `UploadQueue.Count < MaxChunkMeshesInRam`.
7. `MeshgenerateJob` picks `Lod0Mesher` / `Lod1Mesher` / `Lod2Mesher` by `ChunkCoord.LodLevel` (other levels throw), builds the CPU mesh and enqueues it on `UploadQueue`.
8. `Renderer.Render` (main thread) drains `UploadQueue` with a ~5 ms time budget: it uploads to the GPU, swaps in the new mesh (the old one goes to `UnloadQueue`) and registers it in `LoadedChunks`. Meshes whose chunk data is no longer loaded are disposed. Then it renders all non-empty, frustum-visible meshes and disposes the `UnloadQueue`.
9. `ChunkProvider.ModifyBlock` edits `ChunkData`, remeshes that chunk and, if the block is on a border, the adjacent chunk(s) via `RemeshChunk` (requires all neighbors loaded). The old mesh stays visible until the new one is uploaded.

Note: the requestor currently only requests LOD 0. LOD 1/2 meshers exist and are reachable only if LOD>0 coordinates get into the pipeline.

### 4.6 Threading rules

- Background (via `JobScheduler`): chunk generation, meshing. Anything touching `ChunkProvider` concurrent collections must stay thread-safe.
- Main thread only: every OpenGL call (`UploadToGpu`, draw calls, texture creation, Egui rendering), input handling, `Game.Update/Render`.
- `JobScheduler`: worker threads (background threads) wait on a `SemaphoreSlim`; each enqueue releases one permit. Workers prefer the high-priority queue, then low. Job exceptions are caught and logged to stderr. `Stop()` sets a flag and releases 100 permits.
- Worker count: `CoreAvailability.DefaultConfig` = `max(1, ProcessorCount − 2) / 2 + 4`. `coreconfig.txt` is checked for but loading is a TODO that falls back to the default.
- `ChunkRequestor` uses a lock plus a single running `Task` loop for chunk-set recomputation.

### 4.7 GC and GPU memory mitigation

`ChunkProvider` owns pools that must be used instead of allocating per mesh:
- `VertexListPool` (`List<byte>`) and `IndexListPool` (`List<uint>`): `BaseMesher` dequeues them on construction and returns them after upload or `Dispose`.
- `VramPool` (`PooledMeshBuffer`: VAO, VBO, EBO + capacities): `UploadToGpu` reuses buffers (`BufferSubData`, or re-`BufferData` if too small or more than 2× too large) and `Dispose` returns them.

## 5. Rendering

### 5.1 Vertex format and shaders

- Vertex = 16 bytes (`BaseMesher.VertexStride`): `float x, y, z` (12 B, chunk-local), `ushort textureLayer` (2 B), `byte aoLevel` (1 B), 1 byte padding.
- Attributes (set up in `BaseMesher.UploadToGpu`): location 0 `vec3 aPos` (float), location 1 `uint aLayer` (`VertexAttribIPointer`, unsigned short at offset 12), location 2 `int aAoLevel` (`VertexAttribIPointer`, unsigned byte at offset 14).
- There are no UVs in the vertex data. `shader.vert` derives them from `gl_VertexID % 4` using the corner order (0,0), (1,0), (1,1), (0,1). Quads therefore must be emitted as exactly 4 vertices in that corner order.
- AO is a lookup in the vertex shader: levels 0..3 → brightness 1.0 / 0.8 / 0.6 / 0.4. The fragment shader samples `sampler2DArray uTexture` at `(u, v, layer)` and multiplies RGB by brightness.
- Uniforms: `uModel`, `uView`, `uProjection`, `uTexture`. `Shader.SetUniform(int)` throws if a uniform is missing; the matrix/vector/bool overloads silently skip missing uniforms.
- GL state in `ShaderManager.Use`: depth test on, back-face culling on, CCW front faces. Projection uses a fixed 45° FOV with `camera.nearPlane` (0.1) / `farPlane` (1500).

### 5.2 Texture array

`BlockRegistry.LoadTextures` gathers every `Content/<namespace>/Textures/**/*.png`, ids are `<namespace>:<filename-without-ext>` (lowercase), sorted ordinally. The sorted order defines the layer index. All PNGs must have identical dimensions or loading throws. Rows are flipped on upload. Sampler: clamp-to-edge, nearest mag, nearest-mipmap-nearest min, 16× anisotropy, mipmaps generated. There is also an atlas constructor (`TextureArray(gl, atlasPath, tileSize)`) that is not used by the registry.

### 5.3 Meshing

- Faces are only emitted where the neighboring block is air (non-air = occluder; there is no transparency handling yet, `isTransparent` in JSON is unused).
- `Lod0Mesher`: per-vertex AO (`CalcAoLevel` with the `AoOffsets` table: side1, side2, corner per vertex per face). The quad diagonal is flipped depending on AO (`ao0 + ao2 > ao1 + ao3`) to avoid interpolation artifacts. Neighbors are accessed through a 27-entry `_neighborCache` of block arrays; unloaded neighbors count as air. Inner blocks use a fast path without neighbor checks.
- `Lod1Mesher` / `Lod2Mesher`: same face culling, no AO, scaled by 2 / 4. They look up neighbors at their own LOD level, so those chunks must be generated with that `LodLevel`.
- `LOD3Mesher` is an empty class.
- Face indices: `Top=0, Bottom=1, Front=2 (+Z), Back=3 (−Z), Left=4 (−X), Right=5 (+X)` (`BlockRegistry`, mirrored in `BlockLoader`).
- Mesh data is kept in `List<byte>`/`List<uint>` and uploaded as `DynamicDraw`; after upload the CPU lists are cleared and returned to the pools.

### 5.4 Frustum culling

`Camera.CreateFrustum(view, projection)` extracts six planes. `Frustum.IsInFrustum(ChunkCoord)` tests the chunk's AABB (size scales with LOD) against the planes. While the debug camera is active, the frustum is still built from the player camera (the camera passed to `ShaderManager.Use`), but view/projection uniforms come from the debug camera. This is intentional so culling can be inspected from outside.

## 6. Data-driven content

### 6.1 Blocks

Block definitions live at `Content/<Namespace>/Blocks/**/*.json` (nesting allowed; currently `Blocks/stupid/`). The block name is `<namespace>:<filename>` (lowercase), e.g. `core:grass`. The registry parses:

```json
{
  "name": "Grass",
  "tags": ["tag:block", "tag:mineable_shovel", "tag:dirt", "tag:grass"],
  "properties": { "hardness": 0.5, "isSolid": true, "isTransparent": false, "lightEmission": 0 },
  "textures": { "top": "core:grass", "bottom": "core:dirt", "sides": "core:dirt-grass" }
}
```

- `textures` is required. Each of `top, bottom, front, back, left, right` falls back to `sides` if absent. Values are texture ids (`namespace:filename`) and must exist, otherwise `InvalidDataException`.
- `tags` are normalized (trim + lowercase) and indexed for `HasTag` / `GetByTag`.
- `name` and `properties` are not used by the engine at the moment (`properties` is ignored by the deserializer).
- IDs are assigned by ordinal sort of `namespace:name`, starting at 1 (0 = `core:air`). With the current content: `dirt=1, grass=2, snow=3, stone=4`. IDs are not stable when blocks are added or renamed, which matters once saving to disk exists.
- Headless mode (`InitializeHeadless`) discovers texture layers without GL (unknown textures resolve to layer 0). If there is no Content folder it uses hard-coded fallback blocks (dirt=1, grass=2, stone=3, snow=4; a different order than the real registry).
- `BlockLoader` is the static facade over the current `BlockRegistry`; the `TerrainGenerator` resolves `core:dirt/grass/stone/snow` by name at construction.

### 6.2 Textures

`Content/<Namespace>/Textures/*.png`, see 5.2. The grass side texture is `core:dirt-grass`.

### 6.3 World types

- The active terrain definition is `GameSettings.Worldtype`, an encoded FastNoise2 node tree string (default value equals the contents of `Content/Core/WorldTypes/1.txt`, presumably exported from the FastNoise2 node editor).
- `Content/Core/WorldTypes/overworld/world_def.json` is currently empty. World type files are not read by the generator yet.

### 6.4 Embedded resources

`Content/**` is embedded with a logical name that strips the `Content\` prefix and replaces path separators with dots, e.g. `Content/Core/images/me.png` → `Core.images.me.png` (used by `EguiHelpers.IncludeImageResource` in the menu).

## 7. UI and input

- **Egui.NET**: `SilkIntegration` (abstract) collects Silk.NET keyboard/mouse/focus events into `RawInput` and runs `Context.Run`; it also maps keys, clipboard (Ctrl+C/V/X), modifiers and DPI zoom. `SilkGlIntegration` (derives from it) implements `DrawOutput` with its own GL 3.1-style shader, VAO/VBO/EBO, texture management and scissoring. It replaces the stock `Egui.Silk.NET` renderer (it prints "using custom integration" on creation). Egui pointer-move events are ignored while `InputManager.IsMouseLocked`.
- **In game** (`UIManager.Draw`): crosshair, "Engine Settings" window (horizontal/vertical render distance sliders raising `OnRenderDistanceChanged`, "Unload All Chunks"), "PlayerSettings" window (move speed, mouse sensitivity), and a player info box (position, chunk, FPS). Press F to unlock the mouse before using the windows.
- **InputManager** (static): `Actions` enum → key (`_keyBindings`) or mouse button (`_mouseBindings`), and `Actions` → callback (`SetActionBindings`). Key-down and mouse-click events invoke callbacks; held-key polling uses `IsActionPressed`. Do not map raw Silk.NET inputs directly; add an `Actions` entry, a default binding and a callback. `PlayerMovement.MovementUpdate` polls movement actions each update; `LookUpdate` handles mouse deltas via `Camera.Rotate`.
- **Movement**: the player is a free-flying camera (no gravity or collision). `PlayerCharacter.Move` moves relative to the horizontal look direction. The debug camera (`F1`) moves independently with no chunk streaming events.

## 8. Physics and interaction

- `Raycaster.CastBlockRay` is an Amanatides–Woo DDA voxel traversal. It returns a `BlockResult { Hit, HitPosition, HitNormal }` for the first non-air block (any ID ≠ 0; unloaded chunks read as air). `CastEntityRay` is a stub.
- `Physics` wraps the raycaster (`CastRay`; static `Physics.Raycast` facade). `AABB` exists but is unused. There is no collision or gravity yet.
- `PlayerCharacter` binds `DestroyBlock` (sets block 0) and `PlaceBlock` (places `core:dirt` at `HitPosition + HitNormal`) to the mouse actions. Both use a reach of 5.0.

## 9. Configuration (`GameSettings`)

Per-engine-instance settings (thread-safe setters with clamping; `Clone()` supports session copies; `GameSettings.Current` resolves via the active `EngineContext`).

| Setting | Default | Notes |
|---|---|---|
| `RenderDistance` | 20 | horizontal, in chunks (min 1) |
| `VerticalRenderDistance` | 15 | in chunks |
| `Lod1Distance` / `Lod2Distance` | 15 / 20 | stored, not used by the requestor yet |
| `MaxChunkMeshesInRam` | 200 | cap on `UploadQueue` backlog |
| `PlayerMoveSpeed` | 10 | blocks/s |
| `MouseSensitivity` | 0.1 | |
| `Seed` | 1 | passed to FastNoise |
| `Worldtype` | encoded node tree | see 6.3 |
| `MapSize` | 100 | chunks in ±X/±Z (min 10 via setter) |
| `Msaa` | 2 | window sample count, read at window creation |
| `FrameRate` | 120 | window FPS cap; `SetFrameRate` fires `OnFpsChanged` |
| `VSync` | false | `SetVSync` fires `OnVSyncChanged` |
| `LowLatencyRenderer` | false | not used |

Window defaults (`WindowSetup`): 800×600, title "Game", 60 updates/s initially (set to the frame rate after `OnFpsChanged`).

## 10. Terrain generation details

`TerrainGenerator.GenerateChunk(coord)`:
1. If `|X|` or `|Z|` > `MapSize`, return an empty `ChunkData`.
2. Request a 16×17×16 density grid from `NoiseCalculator.GetNoiseValues` (3D `GenUniformGrid3D`, step size `2^LOD`, seed from settings). The extra Y row is used to detect the top surface. The grid layout is `x + y*16 + z*16*17` (note the `NoiseCalculator` x-fastest ordering versus the chunk array's x-slowest).
3. For each cell with `density > 0`: top solid block above world y 80 → `snow`; other top solid → `grass`; within 4×step of the surface (`density <= 4 * stepSize`) → `dirt`; else `stone`. `density <= 0` → air.
4. `DebugExportNoiseMap` is not usable with the 3D noise (see its TODO).

`FastNoise2.cs` is the stock FastNoise2 C# wrapper (metadata lookup, `Set`, `GenUniformGrid2D/3D/4D`, position arrays, single samples) loading `FastNoise.dll` via `DllImport`. Node handles are freed in `Dispose`/finalizer.

## 11. Tests

`BlockGame.Tests` is a console program (references the main project, copies the native DLLs and `Content`). It runs:
1. `TestIsolatedSessionsSideBySide`: two `EngineSession`s with different settings, verifying isolation of settings, chunks, `World`, `Physics` and pools.
2. `TestScopedStaticFacades`: static facades (`GameSettings.Current`, `World`, `Physics`) follow `session.Activate()`.
3. `TestFiftyBenchmarkIterationsLoop`: 50 create/generate/mesh/dispose iterations with a clean-state check.

`TestConcurrentSessionsWithDifferentRegistriesMeshCorrectly` exists but is not called from `Main`. Tests run headless (no GL); sessions use `BlockRegistry.InitializeHeadless`. The process returns exit code 0/1.

## 12. Conventions

- Keep gameplay/world logic going through `World`, `Physics`, `ChunkProvider` and `GameSettings` (instance or facade), not through ad-hoc statics.
- Use `StateManager.StateChange` for state transitions.
- Meshing and generation must not call GL. Anything creating GL objects belongs on the main thread (`Renderer.Render` / `UploadToGpu`).
- Reuse pooled lists and GPU buffers (see 4.7); do not allocate per-face or per-mesh in hot paths.
- A mesh vertex is exactly 16 bytes with the layout in 5.1. If you change it, update `BaseMesher` (`VertexStride`, `AddVertex`, attribute pointers), `shader.vert` and all meshers together.
- New block = new JSON in `Content/<ns>/Blocks/` plus a matching PNG in `Content/<ns>/Textures/` of the same size as the others.

## 13. Known limitations and inconsistencies

- Chunk save/load is a placeholder (`TryLoadFromDisk` returns false). Block edits are lost on exit.
- LOD: LOD1/LOD2 meshers exist but the requestor only requests LOD 0; `LOD3Mesher` is empty; `Lod1Distance`/`Lod2Distance` are unused.
- World type files are not wired into the generator; `world_def.json` is empty.
- `GameLogic`, `CastEntityRay`, `AABB`, `Transform`, `LowLatencyRenderer` are placeholders or unused. No gravity, collision, transparency, water, lighting beyond AO, sound, or trees.
- Block `properties` (hardness, isSolid, isTransparent, lightEmission) are in the JSON but unused; `name` is ignored.
- Block IDs depend on file-name sort order and differ between real and fallback (headless) registries.
- `StateManager.CloseEngine` only disposes the GL object.
- The menu text says "F1 - Debug Cam (No worki)".
- `AGENTS.md` is partly outdated (it mentions 32×32×32 chunks and a 5-float vertex layout). This document reflects the current 16³ chunks and the 16-byte vertex layout. `Renderer.Render` uses a ~5 ms upload budget (AGENTS.md says ~2 ms).
- `Renderer` and `Game` keep much of their state in statics, so run a single game instance per process (headless sessions are fine).
- `Renderer.Dispose` is never called from `Game.Exit`.
