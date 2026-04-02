# EdwinCraft Documentation

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Repository Structure](#2-repository-structure)
3. [Application Lifecycle](#3-application-lifecycle)
4. [Systems Overview](#4-systems-overview)
   - [Terrain Pipeline](#41-terrain-pipeline)
   - [Rendering Pipeline](#42-rendering-pipeline)
   - [Input System](#43-input-system)
   - [Chunk Lifecycle](#44-chunk-lifecycle)
5. [Class Reference](#5-class-reference)
   - [Entry Point & State Machine](#51-entry-point--state-machine)
   - [Engine States](#52-engine-states)
   - [Window](#53-window)
   - [Player](#54-player)
   - [TerrainManaging](#55-terrainmanaging)
   - [Physics](#56-physics)
   - [Graphics](#57-graphics)
   - [Input](#58-input)
   - [Configurations & Settings](#59-configurations--settings)
   - [Utilities](#510-utilities)
6. [Shaders](#6-shaders)
7. [Configuration Files](#7-configuration-files)
8. [Dependencies](#8-dependencies)
9. [Known Limitations & Planned Features](#9-known-limitations--planned-features)

---

## 1. Project Overview

EdwinCraft is a voxel-based terrain renderer written in C# using Silk.NET for windowing and OpenGL. The player can fly freely through a procedurally generated world that is split into 32×32×32 block **chunks**. The terrain is generated using 4D simplex noise (via the FastNoise2 library). The application uses a state-machine architecture with a main menu (rendered with Egui) and a game state that manages the full terrain and physics pipeline.

Key properties at a glance:

| Property | Value |
|---|---|
| Target Framework | .NET 10 |
| Chunk size | 32 × 32 × 32 blocks |
| Default world size | 100 × 100 chunks (`GameSettings.MapSize`) |
| Default render distance | 20 chunks radius (`GameSettings.RenderDistance`) |
| Block types | 0 Air, 1 Dirt/Grass, 2 Stone, 3 Snow |
| Rendering API | OpenGL 4.6 Core via Silk.NET |
| Ambient Occlusion | Per-vertex AO baked into the mesh |
| Frustum Culling | View-frustum AABB test per chunk |
| Multithreading | Chunk generation via `Parallel.For`; mesh building on dedicated worker threads in `ChunkProvider` |
| Noise library | FastNoise2 (via `FastNoise.dll`) wrapped by `NoiseCalculator` |
| UI library | Egui.NET (`SilkGlIntegration` / `SilkIntegration`) |
| Physics | Block raycasting via `Raycaster`; block placement and destruction supported |

---

## 2. Repository Structure

```
BlockGame/
├── Rendering.sln                      Solution file
└── BlockGame/
    ├── Programm.cs                    Entry point (class Programm, note double-m)
    ├── StateManager.cs                State-machine controller (owns window event loop)
    ├── SilkIntegration.cs             Egui ↔ Silk.NET input/window integration base class
    ├── SilkGlIntegration.cs           Egui ↔ OpenGL rendering integration
    ├── BlockGame.csproj               Project / NuGet references
    ├── FastNoise.dll                  Native FastNoise2 library
    ├── NodeEditorIpc.dll              Node editor IPC library
    │
    ├── Window/
    │   └── WindowSetup.cs             Silk.NET window creation & run-loop; global key bindings
    │
    ├── EngineStates/
    │   ├── IStates.cs                 Interface for engine states (Enter/Update/Render/Exit)
    │   ├── Menu.cs                    Main menu state (Egui UI; start game / settings / quit)
    │   └── Game.cs                    Game state (terrain pipeline, player, physics, rendering)
    │
    └── Game/
        ├── Configurations/
        │   ├── BlockTextureConfig.cs  Data classes + static loader for block texture layers
        │   └── TextureConfig.json     Texture-array layer indices for each block face
        │
        ├── Graphics/
        │   ├── Renderer.cs            Central rendering façade (frustum culling, GPU upload)
        │   ├── Shader.cs              Low-level shader compile & uniform upload
        │   ├── ShaderManager.cs       High-level shader wrapper (MVP matrices, textures)
        │   ├── Frustum.cs             Frustum and Plane structs for view-frustum culling
        │   ├── BufferObject.cs        Generic VBO / EBO wrapper
        │   ├── VertexArrayObject.cs   VAO wrapper with attribute layout helpers
        │   ├── Transform.cs           Position / rotation / scale → Model matrix
        │   ├── Shader/
        │   │   ├── shader.vert        GLSL vertex shader (texture array + AO brightness)
        │   │   └── shader.frag        GLSL fragment shader (sampler2DArray + AO)
        │   └── UI/
        │       └── UIManager.cs       In-game Egui UI (player info, engine settings)
        │
        ├── Input/
        │   └── InputManager.cs        Keyboard & mouse dispatch; action binding
        │
        ├── Logic/
        │   ├── GameLogic.cs           Placeholder for future item / game logic
        │   ├── GameSettings.cs        Centralised configurable game settings (static)
        │   ├── Player/
        │   │   ├── Camera.cs          First-person camera (view matrix, frustum creation)
        │   │   ├── PlayerCharacter.cs Player entity; owns Camera; fires OnChunkChanged; block interaction
        │   │   └── PlayerMovement.cs  Translates input into camera/player movement each frame
        │   └── TerrainManaging/
        │       ├── ChunkData.cs       Chunk block data container (byte[] + helpers)
        │       ├── ChunkProvider.cs   Chunk lifecycle manager (generate / mesh / upload / unload)
        │       ├── ChunkRequestor.cs  Decides which chunks to load based on player position
        │       ├── PooledMeshBuffer.cs Reusable GPU buffer struct (VBO/EBO/VAO pooling)
        │       ├── World.cs           Global static world API (ModifyBlock / GetBlock)
        │       └── Generation/
        │           ├── TerrainGenerator.cs  Procedural chunk block-data generation
        │           └── Noise/
        │               ├── FastNoise2.cs    FastNoise2 C# P/Invoke wrapper (third-party)
        │               └── NoiseCalculator.cs 4D noise height-map via FastNoise2
        │       └── Meshing/
        │           ├── BaseMesher.cs        Base class: GPU buffer management, render, dispose
        │           ├── Lod0Mesher.cs        Full-detail (LOD 0) mesh builder with per-vertex AO
        │           ├── LOD1Mesher.cs        LOD 1 stub (not yet implemented)
        │           ├── LOD2Mesher.cs        LOD 2 stub (not yet implemented)
        │           └── LOD3Mesher.cs        LOD 3 stub (not yet implemented)
        │
        ├── PhysicsSystem/
        │   ├── Physics.cs             Global static physics API (Raycast)
        │   ├── Raycaster.cs           DDA block-raycast implementation
        │   └── Structs/
        │       ├── AABB.cs            Axis-aligned bounding box struct
        │       └── RaycastResult.cs   Raycast result (hit position, hit normal, block ID)
        │
        ├── Utilities/
        │   ├── ChunkCoord.cs          Value type for chunk grid coordinates (X, Y, Z, LodLevel)
        │   ├── CoreAvailability.cs    Thread-budget helper (allocates CPU cores per task)
        │   └── MathHelper.cs          Degrees-to-radians and generic vector helpers
        │
        └── texture/
            ├── example.png            Terrain texture atlas (tile sheet)
            ├── Texture.cs             OpenGL 2D texture upload & binding
            └── TextureArray.cs        OpenGL Texture2DArray built from a tile atlas
```

---

## 3. Application Lifecycle

```
Programm.Main()
  └─ StateManager.Run()
       ├─ WindowSetup.CreateWindow()      (create 800×600 VSync window; bind global key actions)
       ├─ register window events
       │     onLoad, onRender, onUpdate, onFramebufferResize
       └─ WindowSetup.Run()               (blocks until window closes)
            │
            ├─ onLoad()
            │    ├─ GL and IInputContext created from window
            │    └─ initial state set to Menu; Menu.Enter() called
            │
            ├─ onRender(deltaTime)  [every frame]
            │    └─ _currentState.Render(deltaTime)
            │
            ├─ onUpdate(deltaTime)  [every frame]
            │    ├─ _currentState.Update(deltaTime)
            │    └─ deferred state transition (if StateManager.StateChange() was called)
            │         ├─ _currentState.Exit()
            │         ├─ _currentState = _nextState
            │         └─ _currentState.Enter(gl, inputContext, manager)
            │
            └─ onFramebufferResize(size)
                 └─ _currentState.FramebufferResize(size)
```

**Menu State (`EngineStates.Menu`):**

Renders a full-screen Egui main menu using `SilkGlIntegration`. Buttons transition to the Game state, open settings, or quit.

```
Menu.Enter()
  ├─ set mouse cursor to Normal (visible)
  └─ create Egui Context + SilkGlIntegration

Menu.Render()
  └─ _uiIntegration.Run(ctx => Draw(ctx))
       ├─ "MeinKraft" title + version label
       ├─ "Spiel starten"  → StateManager.StateChange(new Game())
       ├─ "Einstellungen"  → toggle settings panel
       │     RenderDistance slider (1–40)
       │     World size slider (1–500)
       └─ "Beenden"        → StateManager.CloseEngine()
```

**Game State (`EngineStates.Game`):**

```
Game.Enter()
  ├─ CoreAvailability.Initialize()        compute thread budget
  ├─ PlayerCharacter created at (0, 40, 0)
  │     PlayerCamera = player.Camera
  ├─ Renderer created; Renderer.Setup(PlayerCamera, gl)
  ├─ PlayerMovement created (wraps PlayerCharacter)
  ├─ InputManager.Initialize(inputContext) bind keyboard + mouse
  ├─ InputManager.SetPlayerMovement(playerMovement)
  ├─ InputManager.SetActionBindings(ToogleDebugCamera, ToggleDebugCamera)
  ├─ BlockTextures.Initialize()           load TextureConfig.json
  ├─ build terrain pipeline
  │     TerrainGenerator → ChunkProvider (N meshing threads) → ChunkRequestor
  ├─ World.Initialize(chunkProvider)      set global world API
  ├─ Physics.Initialize(new Raycaster(chunkProvider))
  └─ player.ForceChunkUpdate()            trigger initial chunk load

Game.Render(deltaTime)
  ├─ Fps = 1 / deltaTime
  ├─ Renderer.Clear()
  └─ Renderer.Render()

Game.Update(deltaTime)
  └─ playerMovement.MovementUpdate(deltaTime)

Game.FramebufferResize(size)
  └─ Renderer.FramebufferResize(size)
```

---

## 4. Systems Overview

### 4.1 Terrain Pipeline

The terrain pipeline is assembled in `Game.Enter()`:

```
TerrainGenerator  (NoiseCalculator + FastNoise2)
      │  GenerateChunk(ChunkCoord) → byte[32768]
      ▼
ChunkProvider                  ← central chunk cache; owns meshing worker threads
      │  RequestChunk → data stored in Chunkdata (ConcurrentDictionary<ChunkCoord, ChunkData>)
      │  MeshingQueue / UploadQueue (Channel<T>)
      ▼
Lod0Mesher (extends BaseMesher) ← built on a worker thread; GPU upload deferred to main thread
      ▼
ChunkRequestor                 ← listens to PlayerCharacter.OnChunkChanged
      │  calculates which ChunkCoords are within render distance (radius from GameSettings)
      └─ calls ChunkProvider.RequestChunk (via Parallel.For) / UnloadChunk
```

**How a chunk goes from noise to screen:**

1. `PlayerCharacter` fires `OnChunkChanged` whenever the player crosses a chunk boundary.
2. `ChunkRequestor.OnPlayerChunkChanged()` iterates all chunk coordinates within the configured render radius and calls `ChunkProvider.RequestChunk()` for each, **in parallel** via `Parallel.For`.
3. `ChunkProvider.RequestChunk()` checks its in-memory cache (`LoadedChunks`). If the chunk is absent it calls `TerrainGenerator.GenerateChunk()`, stores the resulting `byte[]` block data in a `ChunkData` object in `Chunkdata`, and queues the chunk for meshing once all its neighbours are also present.
4. `TerrainGenerator.GenerateChunk()` delegates noise evaluation to `NoiseCalculator`, which uses the **FastNoise2** library to produce a 2D height map. The result is a flat `byte[32768]` (`32×32×32`) block array. Chunks that are entirely above the maximum possible terrain height are skipped (`null`); chunks entirely below the surface are filled with solid stone.
5. A dedicated **meshing worker thread** inside `ChunkProvider` reads coordinates from the bounded `MeshingQueue` channel, constructs a `Lod0Mesher` (building CPU-side mesh data), and writes the finished mesher to the `UploadQueue` channel. **No OpenGL calls are made here.**
6. On each render frame, `Renderer.Render()` drains `ChunkProvider.UploadQueue`: for each queued `BaseMesher` it calls `UploadToGpu(gl)` on the main thread (time-capped to ~2 ms per frame to avoid stalling) and adds the chunk to `LoadedChunks`.
7. `Renderer.Render()` then performs a **frustum cull** using `Frustum.isInFrustum()` and skips any chunk whose AABB lies entirely outside the camera frustum.
8. Visible chunks are rendered by calling `chunk.Render(shaderManager)`.

**Block type assignment in `TerrainGenerator.GenerateChunk()` (density-based):**

| Block ID | Type | Condition |
|---|---|---|
| 0 | Air | `density ≤ 0` |
| 1 | Dirt / Grass | `density < 4` (top 3-4 surface blocks) |
| 2 | Stone | `density ≥ 4` (deep underground) |
| 3 | Snow | `density < 2` and `globalY > 30` (mountain peaks) |

**Block interaction:**
`PlayerCharacter` binds mouse buttons to `DestroyBlock` and `PlaceBlock` actions. These call `Physics.Raycast()` (DDA algorithm in `Raycaster`) with a reach of 5 blocks, then call `World.ModifyBlock()` which delegates to `ChunkProvider.ModifyBlock()` and marks the affected chunk as dirty for re-meshing.

### 4.2 Rendering Pipeline

Every frame, `Renderer.Render()` executes the following sequence:

```
ShaderManager.Use(gl, camera)  → returns Frustum
  ├─ gl.Enable(DepthTest + CullFace)
  ├─ _shader.Use()                       activate GLSL program
  ├─ compute View matrix from Camera (or DebugCamera if active)
  ├─ compute Projection matrix (45° FOV, near=0.1, far=1500)
  ├─ build Frustum from combined VP matrix
  └─ upload uView, uProjection, uTexture uniforms

ShaderManager.BindTexture(terrainTexture)
  └─ TextureArray.Bind(Texture0)

drain ChunkProvider.UploadQueue (time-capped ~2 ms):
  ├─ chunk.UploadToGpu(gl)               VBO / EBO / VAO created on main thread
  └─ ChunkProvider.LoadedChunks.TryAdd() register as GPU-ready

for each BaseMesher in ChunkProvider.LoadedChunks:
  ├─ if !frustum.isInFrustum(chunk.ChunkPosition, frustum) → skip
  └─ BaseMesher.Render(shaderManager)
       ├─ shaderManager.SetModelMatrix(model)   upload per-chunk uModel
       ├─ _vao.Bind()
       ├─ _ebo.Bind()
       └─ gl.DrawElements(Triangles, ...)
```

**Vertex layout per vertex** (stride = 5 floats):

| Attribute | Location | Components | Offset | Description |
|---|---|---|---|---|
| `aPos` (world position) | 0 | 3 floats (x, y, z) | 0 | Block-local vertex position |
| `aLayer` (texture layer) | 1 | 1 float | 3 | Texture2DArray layer index |
| `brightness` (AO) | 2 | 1 float | 4 | Per-vertex AO brightness (0.4–1.0) |

UV coordinates (`vec2`) are computed in the vertex shader from `gl_VertexID % 4` rather than being stored per vertex.

**Ambient Occlusion (AO):**  
For every vertex of every visible face, `Lod0Mesher.CalcAoLevel()` checks the two adjacent side blocks and the diagonal corner block. The brightness value is looked up from `AoLookup = { 1.0f, 0.8f, 0.6f, 0.4f }` (aoLevel 0 = fully lit, 3 = darkest). The AO level also determines which diagonal is used when splitting the quad into two triangles, preventing interpolation artifacts.

### 4.3 Input System

`InputManager` is a static class that abstracts Silk.NET's raw keyboard and mouse input into a double-layer mapping:

```
Physical key / mouse button  ──→  Actions (enum)  ──→  C# Action delegate
(Key.W)                            Forward              playerMovement.Move(…)
(MouseButton.Left)                 DestroyBlock         player.DestroyBlock()
```

**Layer 1 – Key ↔ Action mapping** (`_keyBindings: Dictionary<Actions, Key>`):

| Action | Default Key |
|---|---|
| Close | Escape |
| Fullscreen | F11 |
| Borderless | F12 |
| ToogleDebugCamera | F1 |
| ToggleMouseLock | F |
| Forward | W |
| Backward | S |
| Left | A |
| Right | D |
| Up | Space |
| Down | Left Shift |

**Mouse bindings** (`_mouseBindings: Dictionary<Actions, MouseButton>`):

| Action | Default Button |
|---|---|
| DestroyBlock | Left Mouse Button |
| PlaceBlock | Right Mouse Button |

**Layer 2 – Action ↔ Callback mapping** (`_actionBindings: Dictionary<Actions, Action>`):  
Registered via `InputManager.SetActionBindings(action, callback)`. Callbacks are invoked on `KeyDown` (keyboard) or `Click` (mouse). Continuous movement is polled per-frame in `PlayerMovement.MovementUpdate()` via `InputManager.IsActionPressed()`.

**Mouse handling:**  
`OnMouseMove` forwards the raw position to `PlayerMovement.LookUpdate()`, which calculates the delta from the last position and calls `camera.Rotate(deltaYaw, deltaPitch)`.

### 4.4 Chunk Lifecycle

```
State machine for a single ChunkCoord:

  [Unloaded]
      │  ChunkProvider.RequestChunk()  (called in parallel via Parallel.For)
      │    1. already in LoadedChunks? → stay [GPU-Loaded]
      │    2. TryLoadFromDisk()?       → [Data-Ready]  (stub, always false)
      │    3. TerrainGenerator.GenerateChunk() → byte[] stored in ChunkData
      ▼
  [Data-Ready]  (ChunkData in Chunkdata; waiting for all neighbours)
      │  ChunkProvider.TryQueueForMeshing()  (called after each neighbour arrives)
      │    → all neighbours present? → coord written to MeshingQueue channel
      ▼
  [Meshing-Queued]  (worker thread reads coord from MeshingQueue channel)
      │  Lod0Mesher constructor runs BuildMeshData()   (background thread)
      │  Finished mesher written to UploadQueue channel
      ▼
  [Upload-Pending]  (CPU mesh ready; no GPU resources yet)
      │  Renderer.Render() drains UploadQueue (main thread, time-capped ~2 ms/frame)
      │    BaseMesher.UploadToGpu(gl)  → VBO / EBO / VAO created
      │    LoadedChunks.TryAdd(coord, mesher)
      ▼
  [GPU-Loaded]  (lives in LoadedChunks dictionary, GPU mesh allocated)
      │  ChunkProvider.UnloadChunk()
      │    1. BaseMesher.Dispose()  release VBO / EBO / VAO
      │    2. remove from LoadedChunks and Chunkdata
      ▼
  [Unloaded]
```

`ChunkRequestor` drives the transitions: on every `OnChunkChanged` event fired by `PlayerCharacter` it computes the new set of active chunk coordinates, requests new chunks in parallel, and diffs against the previous set to unload chunks that moved out of range.

---

## 5. Class Reference

### 5.1 Entry Point & State Machine

#### `Programm` (`Programm.cs`)
**Namespace:** `Basics`  
Entry point of the application.

| Member | Description |
|---|---|
| `Main(string[] args)` | Creates a `StateManager` instance and calls `Run()`. |

---

#### `StateManager` (`StateManager.cs`)
**Namespace:** `Basics`  
Owns the Silk.NET window event loop and the active engine state. Implements a deferred state-transition pattern so that state changes requested during `Update` take effect at the end of the same frame.

| Member | Description |
|---|---|
| `Run()` | Creates the window via `WindowSetup`, registers events (`onLoad`, `onRender`, `onUpdate`, `onFramebufferResize`), starts the run-loop, and disposes the window on exit. |
| `StateChange(IStates newState)` | Schedules a deferred state switch; applied in the next `onUpdate` call. |
| `CloseEngine()` | Disposes the OpenGL context. |

---

### 5.2 Engine States

#### `IStates` (`EngineStates/IStates.cs`)
**Namespace:** `Basics.EngineStates`  
Interface every engine state must implement.

| Member | Description |
|---|---|
| `Enter(GL, IInputContext, StateManager)` | Called once when the state becomes active. |
| `Update(double delta)` | Called every frame for game logic. |
| `Render(double delta)` | Called every frame for rendering. |
| `FramebufferResize(Vector2D<int>)` | Called when the window is resized. |
| `Exit()` | Called once when the state is deactivated. |

---

#### `Menu` (`EngineStates/Menu.cs`)
**Namespace:** `Basics.EngineStates`  
Main-menu state. Uses `SilkGlIntegration` to render an Egui UI with play, settings, and quit buttons, and a render-distance / world-size settings panel.

---

#### `Game` (`EngineStates/Game.cs`)
**Namespace:** `Basics.EngineStates`  
Game state. Owns all gameplay subsystems.

| Member | Description |
|---|---|
| `PlayerCamera` (static `Camera`) | The main player camera; shared with `PlayerMovement` and `Renderer`. |
| `DebugCamera` (static `Camera?`) | Optional second free-cam. When non-null the renderer draws from this camera's view; frustum culling still uses `PlayerCamera`. |
| `Fps` (static `float`) | Current frames per second (updated each render frame). |
| `Enter(GL, IInputContext, StateManager)` | Initialises all subsystems (see Application Lifecycle §3). |
| `Render(double deltaTime)` | Clears the frame buffer and calls `Renderer.Render()`. |
| `Update(double deltaTime)` | Calls `PlayerMovement.MovementUpdate(deltaTime)`. |
| `FramebufferResize(Vector2D<int>)` | Passes the new size to `Renderer.FramebufferResize()`. |
| `ToggleDebugCamera()` (private static) | Creates a `DebugCamera` at the player's position and redirects `PlayerMovement` to control it. Calling again destroys the debug camera. |

---

### 5.3 Window

#### `WindowSetup` (`Window/WindowSetup.cs`)
**Namespace:** `Basics.Window`  
Static façade for Silk.NET window creation. Also registers the global Close / Fullscreen / Borderless key bindings.

| Member | Description |
|---|---|
| `Window` (static `IWindow`) | The active Silk.NET window instance. |
| `CreateWindow()` | Creates an 800×600 VSync window titled "Game" and binds Close (Escape), Fullscreen (F11), Borderless (F12) actions. |
| `Run()` | Starts the Silk.NET run-loop (blocks until closed). |

---

### 5.4 Player

#### `Camera` (`Game/Logic/Player/Camera.cs`)
**Namespace:** `Basics.Game`  
First-person camera that manages the view matrix and frustum creation. Chunk change detection has been moved to `PlayerCharacter`.

| Member | Description |
|---|---|
| `Position` (`Vector3`) | World-space position of the camera. |
| `Front` (`Vector3`) | Normalised look direction (default: −Z). |
| `GlobalUp` (`Vector3`) | World up vector (always +Y). |
| `Up` (`Vector3`, computed) | `Cross(Right, Front)` normalised. |
| `Right` (`Vector3`, computed) | `Cross(Front, GlobalUp)` normalised. |
| `Yaw` (`float`) | Horizontal rotation angle in degrees (default: −90°). |
| `Pitch` (`float`) | Vertical rotation angle in degrees (default: 0°, clamped to ±89°). |
| `nearPlane` (`float`) | Near clip plane distance (default: 0.1). |
| `farPlane` (`float`) | Far clip plane distance (default: 1500). |
| `fovY` (`float`) | Vertical field of view in degrees (default: 45). |
| `AspectRatio` (`float`) | Viewport aspect ratio; updated by `Renderer.FramebufferResize()`. |
| `GetChunkCoord(Vector3 pos)` | Divides world position by 32 using `MathF.Floor`; returns the `ChunkCoord` that contains `pos`. |
| `GetViewMatrix()` | Returns the `Matrix4x4` look-at matrix for use in the shader. |
| `Rotate(float deltaYaw, float deltaPitch)` | Updates `Yaw` and `Pitch` from mouse delta values, clamping pitch to ±89°, and recomputes `Front`. |
| `CreateFrustum(Matrix4x4 view, Matrix4x4 projection)` | Builds a `Frustum` from the combined VP matrix using Gribb/Hartmann plane extraction. |

---

#### `PlayerCharacter` (`Game/Logic/Player/PlayerCharacter.cs`)
**Namespace:** `Basics.Game.Player`  
Player entity. Owns a `Camera`, fires `OnChunkChanged` when the player crosses a chunk boundary, and handles block destruction/placement via the Physics system.

| Member | Description |
|---|---|
| `Camera` (`Camera`) | The player's first-person camera. |
| `OnChunkChanged` (`event Action<ChunkCoord>?`) | Fired when the player enters a new chunk. Subscribed to by `ChunkRequestor`. |
| `ForceChunkUpdate()` | Recalculates the current chunk coord and fires `OnChunkChanged`. Called at startup to seed the chunk loader. |
| `Move(Vector3 direction)` | Moves the player (XZ grounded via `groundedFront`, Y free via `GlobalUp`). Fires `OnChunkChanged` if the chunk changes. |
| `DestroyBlock()` (private) | Casts a ray from the camera (reach 5 blocks); calls `World.ModifyBlock(..., 0)` on hit. |
| `PlaceBlock()` (private) | Casts a ray from the camera; places block ID 1 on the face adjacent to the hit surface. |

---

#### `PlayerMovement` (`Game/Logic/Player/PlayerMovement.cs`)
**Namespace:** `Basics.Game.Player`  
Translates per-frame input into camera/player movement. Supports switching between the player camera and a debug camera.

| Member | Description |
|---|---|
| `PlayerMovement(PlayerCharacter player)` | Stores the player reference; sets the active camera to the player's camera. |
| `SetActiveCamera(Camera camera)` | Redirects movement and look to the given camera (used for debug camera). |
| `UsePlayerCamera()` | Reverts to controlling the player's camera. |
| `MovementUpdate(double deltaTime)` | Polls `InputManager.IsActionPressed()` for all directional actions, normalises the direction vector, scales by `GameSettings.PlayerMoveSpeed * deltaTime`, and calls `player.Move()` (player cam) or `MoveCamera()` (debug cam). |
| `LookUpdate(Vector2 mousePosition)` | Computes mouse delta and calls `camera.Rotate()` scaled by `GameSettings.MouseSensitivity`. |

---

#### `GameLogic` (`Game/Logic/GameLogic.cs`)
**Namespace:** `Basics.Game`  
Placeholder class for future item / dropped-entity logic.

---

#### `GameSettings` (`Game/Logic/GameSettings.cs`)
**Namespace:** `Basics.Game`  
Thread-safe static class centralising all configurable game values.

| Setting | Default | Setter |
|---|---|---|
| `RenderDistance` | 20 | `SetRenderDistance(int)` |
| `Lod1Distance` | 15 | `SetLod1Distance(int)` |
| `Lod2Distance` | 20 | `SetLod2Distance(int)` |
| `VerticalRenderDistance` | 10 | `SetVerticalRenderDistance(int)` |
| `PlayerMoveSpeed` | 10f | `SetPlayerMoveSpeed(float)` |
| `MouseSensitivity` | 0.1f | `SetMouseSensitivity(float)` |
| `Seed` | 1 | `SetSeed(int)` |
| `MapSize` | 100 | `SetMapSize(int)` |

---

### 5.5 TerrainManaging

#### `ChunkData` (`Game/Logic/TerrainManaging/ChunkData.cs`)
**Namespace:** `Basics.Game.TerrainManaging`  
Container for a single chunk's block data (`byte[]`) with bounds-checked and unsafe accessors, dirty-flag tracking, and static coordinate conversion helpers.

| Member | Description |
|---|---|
| `Blocks` (`byte[]?`) | Flat `32³` block array; `null` means all-air. |
| `Coord` (`ChunkCoord`) | The chunk's grid coordinate. |
| `IsDirty` (`bool`) | Set to `true` by `SetBlock`; signals that the chunk needs re-meshing. |
| `ToIndex(x,y,z)` | Converts local coordinates to array index (`x*1024 + y*32 + z`). |
| `GetBlock(x,y,z)` | Returns the block ID at local position. Returns 0 if `Blocks` is null. |
| `SetBlock(x,y,z,id)` | Sets the block ID; allocates `Blocks` if null; sets `IsDirty`. |
| `GetBlockSafe` / `SetBlockSafe` | Bounds-checked variants. |
| `IsBlock(x,y,z)` | Returns `true` if the block is solid (non-zero) with bounds check. |
| `WorldToLocal(wx,wy,wz)` | Converts world coordinates to local chunk coordinates (handles negatives). |
| `WorldToChunkCoord(wx,wy,wz)` | Converts world coordinates to the containing `ChunkCoord`. |

---

#### `ChunkProvider` (`Game/Logic/TerrainManaging/ChunkProvider.cs`)
**Namespace:** `Basics.Game.TerrainManaging`  
Central chunk cache. Owns N meshing worker threads and the `MeshingQueue`/`UploadQueue` channels. Also handles block modification and chunk invalidation.

| Member | Description |
|---|---|
| `Chunkdata` (static `ConcurrentDictionary<ChunkCoord, ChunkData>`) | All chunk block data currently in memory. |
| `MeshingQueue` (`Channel<ChunkCoord>`) | Bounded channel (capacity 200) written by `RequestChunk`, read by worker threads. |
| `UploadQueue` (`Channel<BaseMesher>`) | Bounded channel (capacity 300) written by workers, drained by `Renderer` on the main thread. |
| `UnloadQueue` (`ConcurrentQueue<BaseMesher>`) | Meshers waiting to have their GPU resources freed. |
| `LoadedChunks` (static `ConcurrentDictionary<ChunkCoord, BaseMesher>`) | GPU-ready chunks. |
| `VramPool` / `VertexListPool` / `IndexListPool` | Object pools to reduce GC pressure on mesh buffers. |
| `RequestChunk(ChunkCoord)` | Generates (or retrieves cached) chunk data and queues for meshing. |
| `UnloadChunk(ChunkCoord)` | Disposes GPU resources and removes from all caches. |
| `ModifyBlock(x,y,z,blockId)` | Sets a block in world coordinates, marks the chunk dirty, and re-queues affected chunks for meshing. |
| `GetBlockAt(x,y,z)` | Returns the block ID at world coordinates. |

---

#### `ChunkRequestor` (`Game/Logic/TerrainManaging/ChunkRequestor.cs`)
**Namespace:** `Basics.Game.TerrainManaging`  
Subscribes to `PlayerCharacter.OnChunkChanged`. On each chunk crossing it computes the set of chunks within the configured render distance, requests new ones in parallel, and unloads any that went out of range.

| Member | Description |
|---|---|
| `ChunkRequestor(PlayerCharacter, ChunkProvider, int cores)` | Subscribes to `player.OnChunkChanged`; configures `Parallel.For` degree of parallelism. |
| `RenderDistance` (`int`) | Adjustable render distance (defaults to `GameSettings.RenderDistance`). |
| `UnloadAllChunks()` | Forces all currently loaded chunks to be unloaded (used by in-game UI). |

---

#### `TerrainGenerator` (`Game/Logic/TerrainManaging/Generation/TerrainGenerator.cs`)
**Namespace:** `Basics.Game.TerrainManaging.Generation`  
Generates chunk block data from a 2D height map via `NoiseCalculator`.

| Member | Description |
|---|---|
| `GenerateChunk(ChunkCoord coord)` | Returns a flat `byte[32768]` block array for the given chunk. Returns `null` for chunks fully above the terrain ceiling. Uses `GameSettings.MapSize` to reject out-of-bounds requests. Supports LOD scaling via `coord.LodLevel`. |
| `DebugExportNoiseMap(string filename, int steps)` | Exports the entire world height map as a quantised greyscale PNG (uses `ImageSharp`). |

---

#### `World` (`Game/Logic/TerrainManaging/World.cs`)
**Namespace:** `Basics.Game.TerrainManaging`  
Global static façade for world modification. Allows any class to call `World.ModifyBlock` or `World.GetBlock` without holding a direct reference to `ChunkProvider`.

| Member | Description |
|---|---|
| `Initialize(ChunkProvider)` | Stores the `ChunkProvider` reference. Called once at startup. |
| `ModifyBlock(x,y,z,blockId)` | Delegates to `ChunkProvider.ModifyBlock()`. |
| `GetBlock(x,y,z)` | Delegates to `ChunkProvider.GetBlockAt()`. |

---

#### `PooledMeshBuffer` (`Game/Logic/TerrainManaging/PooledMeshBuffer.cs`)
**Namespace:** `Basics.Game.Logic.TerrainManaging`  
Value struct holding OpenGL handles for a reusable VAO/VBO/EBO triple, along with the current GPU capacity of each buffer. Stored in `ChunkProvider.VramPool` to reduce allocation overhead.

---

#### Meshing classes (`Game/Logic/TerrainManaging/Meshing/`)

| Class | Description |
|---|---|
| `BaseMesher` | Abstract base: holds GPU handles, exposes `UploadToGpu(gl)`, `Render(shaderManager)`, `Dispose()`. |
| `Lod0Mesher` | Full-detail mesh builder. Iterates all 6 face directions, skips hidden faces, computes per-vertex AO via `CalcAoLevel()`, and writes vertices + indices into pooled lists. |
| `LOD1Mesher` | LOD 1 stub (not yet implemented). |
| `LOD2Mesher` | LOD 2 stub (not yet implemented). |
| `LOD3Mesher` | LOD 3 stub (not yet implemented). |

---

### 5.6 Physics

#### `Physics` (`Game/PhysicsSystem/Physics.cs`)
**Namespace:** `Basics.PhysicsSystem`  
Global static façade for physics queries. Initialised once with a `Raycaster` instance.

| Member | Description |
|---|---|
| `Initialize(Raycaster)` | Stores the raycaster. Called once in `Game.Enter()`. |
| `Raycast(start, dir, maxDist)` | Delegates to `Raycaster.CastBlockRay()` and returns a `BlockResult`. |

---

#### `Raycaster` (`Game/PhysicsSystem/Raycaster.cs`)
**Namespace:** `Basics.PhysicsSystem`  
Implements a DDA (Digital Differential Analyzer) voxel traversal algorithm for block raycasting.

| Member | Description |
|---|---|
| `Raycaster(ChunkProvider)` | Stores the chunk provider for block data access. |
| `CastBlockRay(start, dir, maxDist)` | Steps through voxels along the ray; returns a `BlockResult` with hit position, surface normal, and block ID. |

---

#### `BlockResult` / `AABB` (`Game/PhysicsSystem/Structs/`)

| Struct | Description |
|---|---|
| `RaycastResult` (aka `BlockResult`) | Holds `Hit` (bool), `HitPosition` (int3), `HitNormal` (int3), and block ID. |
| `AABB` | Axis-aligned bounding box (min/max `Vector3D<float>`). |

---

### 5.7 Graphics

#### `Renderer` (`Game/Graphics/Renderer.cs`)
**Namespace:** `Basics.Game.Graphics`  
Central rendering façade. Holds a reference to `ChunkProvider` (set by `Game.Enter()`), drains the upload queue, performs frustum culling, and issues draw calls.

| Member | Description |
|---|---|
| `ChunkProvider` (static) | Set by `Game.Enter()` after `ChunkProvider` is created. |
| `Setup(Camera, GL)` | Initialises OpenGL state, shaders, and the terrain `TextureArray`. |
| `SetCamera(Camera)` | Switches the camera used for rendering (called when toggling debug cam). |
| `Clear()` | Clears colour and depth buffers. |
| `Render()` | Runs the full render sequence (see §4.2). |
| `FramebufferResize(Vector2D<int>)` | Updates the OpenGL viewport and the camera's `AspectRatio`. |

---

#### `ShaderManager` (`Game/Graphics/ShaderManager.cs`)
**Namespace:** `Basics.Game.Graphics`  
High-level shader wrapper. Activates the GLSL program, uploads View/Projection/Texture uniforms, and returns the computed `Frustum`.

---

#### `Shader` (`Game/Graphics/Shader.cs`)
**Namespace:** `Basics.Game.Graphics`  
Compiles vertex + fragment shaders, links the program, and provides `SetUniform` helpers for common types.

---

#### `Frustum` / `Plane` (`Game/Graphics/Frustum.cs`)
**Namespace:** `Basics.Graphics`  
Six-plane frustum struct and `Plane` (normal + distance). `Frustum.isInFrustum(chunkPosition, frustum)` performs an AABB-vs-planes test to decide whether a chunk is visible.

---

#### `BufferObject<T>` (`Game/Graphics/BufferObject.cs`)
Generic VBO / EBO wrapper. Uploads data to the GPU and handles binding.

#### `VertexArrayObject` (`Game/Graphics/VertexArrayObject.cs`)
VAO wrapper with attribute pointer helpers.

#### `Transform` (`Game/Graphics/Transform.cs`)
Computes a Model matrix from position, rotation, and scale.

#### `UIManager` (`Game/Graphics/UI/UIManager.cs`)
**Namespace:** `Basics.Graphics.UI`  
Egui-based in-game HUD (currently commented out in `Game.cs`). Defines panels for player coordinates/FPS, render-distance slider, speed/sensitivity sliders, and an "Unload All Chunks" button.

---

#### `SilkIntegration` (`SilkIntegration.cs`)
**Namespace:** `Basics`  
Abstract base class that bridges Silk.NET window/input events into Egui `RawInput`. Handles keyboard, mouse, scroll, and focus events. Must be subclassed with `DrawOutput()` to provide actual rendering.

#### `SilkGlIntegration` (`SilkGlIntegration.cs`)
**Namespace:** `Basics`  
Concrete `SilkIntegration` subclass that renders Egui output via OpenGL. Used by both `Menu` and (when enabled) `Game`.

---

### 5.8 Input

#### `InputManager` (`Game/Input/InputManager.cs`)
**Namespace:** `Basics.Input`  
All-static class. Manages two-layer key/action and action/callback mappings. Handles keyboard key-down/up, mouse movement, mouse clicks, and scroll.

| Member | Description |
|---|---|
| `Initialize(IInputContext)` | Binds keyboard and mouse events; sets mouse to raw (hidden) mode; loads default bindings. |
| `SetPlayerMovement(PlayerMovement)` | Stores the `PlayerMovement` reference used by `OnMouseMove`. |
| `SetActionBindings(Actions, Action)` | Registers (or replaces) a callback for an action. |
| `SetkeyBindings(Actions, Key)` | Overrides a key binding. |
| `IsActionPressed(Actions)` | Returns `true` if the key or mouse button bound to the action is currently held. |
| `IsMouseLocked` (`bool`) | Whether the mouse cursor is in raw/captured mode. |

---

### 5.9 Configurations & Settings

#### `BlockTextureConfig` (`Game/Configurations/BlockTextureConfig.cs`)
**Namespace:** `Basics.Configurations`  
Static loader for `TextureConfig.json`. Populates the `BlockTextures` dictionary mapping block IDs and face directions to `Texture2DArray` layer indices.

---

### 5.10 Utilities

#### `ChunkCoord` (`Game/Utilities/ChunkCoord.cs`)
**Namespace:** `Basics.Utilities`  
Value type for 3D chunk grid coordinates with an additional `LodLevel` byte. Implements equality and `GetHashCode` for use as dictionary keys. LOD level 0 is used by the camera and all normal gameplay chunks.

#### `CoreAvailability` (`Game/Utilities/CoreAvailability.cs`)
**Namespace:** `Basics.Utilities`  
Reads `Environment.ProcessorCount` and partitions available cores between terrain generation (`GetTerrainGenerationCores()`) and chunk meshing (`GetChunkMeshingCores()`).

#### `MathHelper` (`Game/Utilities/MathHelper.cs`)
**Namespace:** `Basics.Utilities`  
`DegreesToRadians(float)` and `ToGeneric(Vector3)` helpers (converts `System.Numerics.Vector3` to `Silk.NET.Maths.Vector3D<float>`).

---

## 6. Shaders

Both shaders live in `BlockGame/Game/Graphics/Shader/`.

### `shader.vert`
- Inputs: `aPos` (vec3, location 0), `aLayer` (float, location 1), `brightness` (float, location 2).
- Uniforms: `uModel`, `uView`, `uProjection` (mat4); `uTexture` (sampler2DArray).
- UV coordinates are generated procedurally from `gl_VertexID % 4` (no per-vertex UV storage needed).
- Outputs `TexCoords` (vec3 for sampler2DArray, with `z = aLayer`), `Brightness` (float), and `gl_Position`.

### `shader.frag`
- Samples `uTexture` (sampler2DArray) at `TexCoords`.
- Multiplies the texture colour by `Brightness` to apply per-vertex AO.

---

## 7. Configuration Files

### `TextureConfig.json` (`Game/Configurations/TextureConfig.json`)
Defines the mapping from block ID + face direction to a `Texture2DArray` layer index.  
`BlockTextureConfig.Initialize(path)` reads this file once at startup and populates the `BlockTextures` static dictionary.

---

## 8. Dependencies

| Package | Purpose |
|---|---|
| Silk.NET | Windowing, OpenGL 4.6 bindings, input |
| FastNoise2 (`FastNoise.dll`) | Native noise generation library |
| Egui.NET | Immediate-mode UI (main menu, in-game HUD) |
| NodeEditorIpc (`NodeEditorIpc.dll`) | Node-editor IPC (terrain generation tooling) |
| SixLabors.ImageSharp | PNG export for debug noise map |

---

## 9. Known Limitations & Planned Features

### High Priority
- Store chunk data more efficiently (disk persistence)
- LODs (implementation planned but stubs not yet complete)
- General debugging tools (debug camera added; more planned)

### Low Priority
- Block breaking visual effects
- Sound and music
- Water
- Data-driven terrain generation
- Better terrain (node-based FastNoise2 integration)
- Client→Server←Client networking structure

### Future
- Placing trees and bushes
- Dynamic grass
- Full in-game UI menu (start screen done)
- Main menu polish
