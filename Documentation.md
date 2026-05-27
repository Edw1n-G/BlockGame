# BlockGame Documentation

## Overview
BlockGame is a voxel terrain renderer in C# built on Silk.NET (OpenGL) with an Egui.NET UI layer. The engine streams 16×16×16 chunks around the player, generates terrain via FastNoise2, and supports block placement/destruction through raycasting.

Key facts:
- Target framework: **net10.0**
- Rendering: **OpenGL 4.6** via Silk.NET
- Chunk size: **16 × 16 × 16** blocks (`ChunkData.ChunkSize`)
- World bounds: **X/Z limited by `GameSettings.MapSize`** (default 100)
- Rendering features: texture arrays, frustum culling, per-vertex AO
- Threading: background jobs for generation/meshing; GPU uploads on the main thread

## Quick start
### Requirements
- .NET 10 SDK
- GPU/driver with OpenGL 4.6 support
- Native DLL support for `FastNoise.dll` and `NodeEditorIpc.dll` (bundled with the project)

### Build and run
From the repository root:
```
dotnet restore BlockGame/BlockGame.csproj
dotnet run --project BlockGame/BlockGame.csproj
```
If using an IDE, set the working directory to `BlockGame/` so the `Content` folder resolves correctly.

## Controls
Default bindings from `InputManager`:
- **W/A/S/D**: Move
- **Space**: Up
- **Left Shift**: Down
- **F**: Toggle mouse lock
- **F1**: Toggle debug/free camera
- **F11**: Fullscreen
- **F12**: Borderless fullscreen
- **Esc**: Close window
- **Left mouse**: Destroy block (raycast)
- **Right mouse**: Place block (raycast)

## Repository layout
```
Rendering.sln
BlockGame/
├─ Programm.cs            Entry point → StateManager.Run()
├─ StateManager.cs        Deferred state machine & window lifecycle
├─ Window/WindowSetup.cs  Window creation and global key actions
├─ EngineStates/          Menu and Game states
├─ Game/
│  ├─ Configurations/     BlockLoader (data-driven blocks + textures)
│  ├─ Graphics/           Renderer, shaders, frustum, UI
│  ├─ Input/              InputManager and action bindings
│  ├─ Jobs/               JobScheduler + generation/meshing jobs
│  ├─ Logic/              Player, terrain management, GameSettings
│  ├─ PhysicsSystem/      Raycaster and physics structs
│  ├─ Utilities/          ChunkCoord, math helpers, thread budgeting
│  └─ texture/            Texture2D and Texture2DArray wrappers
└─ Content/
   └─ Core/
      ├─ Blocks/          Block JSON definitions (nested folders allowed)
      ├─ Textures/        PNG textures used by block faces
      ├─ WorldTypes/      World definition placeholders
      └─ images/          UI assets (e.g. menu portrait)
```

## Runtime architecture
### State machine and lifecycle
- `Programm.Main` → `StateManager.Run()`.
- `StateManager` creates the window and registers `Load/Render/Update/Resize` callbacks.
- On load it enters `EngineStates.Menu`.
- Menu uses Egui.NET; starting a game calls `StateManager.StateChange(new Game())`, which swaps states at end of frame.

### Game state initialization
`EngineStates.Game.Enter` sets up:
- CoreAvailability (thread budget)
- BlockLoader + texture array (`Content` root)
- Player + cameras
- Renderer and in-game UI
- InputManager bindings
- JobScheduler and terrain pipeline
- Global facades `World` and `Physics`

### Terrain pipeline (streaming + meshing)
1. `PlayerCharacter` raises `OnChunkChanged` when the player enters a new chunk.
2. `ChunkRequestor` computes an ellipsoid of chunks within render distances and requests them.
3. `JobScheduler` runs background jobs:
   - `ChunkLoadorGenerateJob` generates chunk block data with `TerrainGenerator` (FastNoise2).
   - `MeshgenerateJob` converts chunk data into meshes (LOD0 implemented).
4. `ChunkProvider` queues mesh uploads and manages chunk lifecycle.
5. `Renderer` uploads queued meshes on the main thread (time budget ~5ms per frame) and renders visible chunks with frustum culling.

Chunk details:
- Size: **16×16×16** blocks
- Map bounds: `GameSettings.MapSize` caps X/Z range; outside returns empty chunks
- Block edits: `World.ModifyBlock` remeshes affected chunks

### Rendering
- Shaders: `Game/Graphics/Shader/shader.vert` and `shader.frag`
- Texture array: `TextureArray` built from `Content/<namespace>/Textures` PNGs, sorted by ID
- Per-vertex AO is computed in `Lod0Mesher` and passed as a vertex attribute
- Frustum culling uses `Camera.CreateFrustum` + `Renderer` checks

### Input and UI
- `InputManager` maps actions to keys/mouse buttons and binds callbacks
- Menu UI and in-game UI use Egui.NET via `SilkGlIntegration`

### Physics
- `Physics.Raycast` wraps `Raycaster.CastBlockRay`, a DDA grid walk
- Used by the player to place/destroy blocks

### Threading rules
- Generation/meshing runs on background threads via `JobScheduler`
- OpenGL/GPU work (UploadToGpu, drawing) must happen on the main thread in `Renderer.Render`

## Data-driven content
### Block definitions
- Stored under `Content/<namespace>/Blocks/**.json`
- JSON includes `textures` per face and optional `tags`/`properties`
- `BlockLoader` currently uses textures and tags; extra properties are parsed but not used by gameplay yet

### Textures
- Stored under `Content/<namespace>/Textures/*.png`
- All textures are packed into a `Texture2DArray` and indexed by ID

### World types
- `Content/Core/WorldTypes` is a placeholder location
- The active noise definition comes from `GameSettings.Worldtype` (encoded node tree string)

## Configuration
- `GameSettings`: render distances, map size, movement speed, mouse sensitivity, MSAA, seed
- `CoreAvailability`: computes worker thread count (optionally reads `coreconfig.txt`; load is currently unimplemented)

## Known limitations
- Chunk save/load to disk is a placeholder
- LOD1–LOD3 meshers are stubs
- `GameLogic` and entity raycasts are placeholders
- World type files are not wired into the generator yet
