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
   - [Program & MainClass](#51-program--mainclass)
   - [Setup](#52-setup)
   - [Game](#53-game)
   - [TerrainManaging](#54-terrainmanaging)
   - [Graphics](#55-graphics)
   - [Input](#56-input)
   - [Configurations](#57-configurations)
   - [Utilities](#58-utilities)
6. [Shaders](#6-shaders)
7. [Configuration Files](#7-configuration-files)
8. [Dependencies](#8-dependencies)
9. [Known Limitations & Planned Features](#9-known-limitations--planned-features)

---

## 1. Project Overview

EdwinCraft is a voxel-based terrain renderer written in C# using Silk.NET for windowing and OpenGL. The player can fly freely through a procedurally generated world that is split into 32×32×32 block **chunks**. The terrain is generated using 4D simplex noise (via the FastNoise2 library) mapped onto a torus so that the world wraps seamlessly at its edges (no visible seams).

Key properties at a glance:

| Property | Value |
|---|---|
| Target Framework | .NET 10 |
| Chunk size | 32 × 32 × 32 blocks |
| Default world size | 32 × 32 chunks |
| Render distance | 16 chunks (radius) |
| Block types | 0 Air, 1 Dirt/Grass, 2 Stone, 3 Snow |
| Rendering API | OpenGL 4.6 Core via Silk.NET |
| Ambient Occlusion | Per-vertex AO baked into the mesh |
| Frustum Culling | View-frustum AABB test per chunk |
| Multithreading | Chunk generation via `Parallel.For`; mesh building on dedicated worker threads in `ChunkProvider` |
| Noise library | FastNoise2 (via `FastNoise.dll`) wrapped by `NoiseCalculator` |

---

## 2. Repository Structure

```
EdwinCraft/
├── Rendering.sln                      Solution file
└── Terrain_Generator/
    ├── Program.cs                     Entry point
    ├── MainClass.cs                   Application controller (game loop)
    ├── Terrain_Generator.csproj       Project / NuGet references
    │
    ├── Setup/
    │   └── WindowSetup.cs             Silk.NET window creation & run-loop
    │
    ├── Game/
    │   ├── Camera.cs                  First-person camera (view matrix, frustum, chunk events)
    │   ├── GameLogic.cs               Placeholder for future physics / item logic
    │   ├── Movement.cs                Keyboard + mouse movement logic
    │   └── TerrainManaging/
    │       ├── ChunkProvider.cs       Chunk lifecycle manager (load / cache / unload / meshing workers)
    │       ├── ChunkRequestor.cs      Decides which chunks to load (parallel generation)
    │       ├── Generation/
    │       │   ├── TerrainGenerator.cs    Procedural chunk block-data generation
    │       │   └── Noise/
    │       │       ├── FastNoise2.cs      FastNoise2 C# P/Invoke wrapper (third-party)
    │       │       └── NoiseCalculator.cs 4D torus-mapped noise using FastNoise2
    │       └── Meshing/
    │           ├── BaseMesher.cs      Base class: GPU buffer management, render, dispose
    │           ├── Lod0Mesher.cs      Full-detail (LOD 0) mesh builder with per-vertex AO
    │           ├── LOD1Mesher.cs      LOD 1 stub (not yet implemented)
    │           ├── LOD2Mesher.cs      LOD 2 stub (not yet implemented)
    │           └── LOD3Mesher.cs      LOD 3 stub (not yet implemented)
    │
    ├── Graphics/
    │   ├── Renderer.cs                Central rendering façade (frustum culling)
    │   ├── Shader.cs                  Low-level shader compile & uniform upload
    │   ├── ShaderManager.cs           High-level shader wrapper (MVP matrices, textures)
    │   ├── Frustum.cs                 Frustum and Plane structs for view-frustum culling
    │   ├── BufferObject.cs            Generic VBO / EBO wrapper
    │   ├── VertexArrayObject.cs       VAO wrapper with attribute layout helpers
    │   ├── Transform.cs               Position / rotation / scale → Model matrix
    │   └── Shader/
    │       ├── shader.vert            GLSL vertex shader (texture array + AO brightness)
    │       └── shader.frag            GLSL fragment shader (sampler2DArray + AO)
    │
    ├── Input/
    │   └── InputManager.cs            Keyboard & mouse dispatch; action binding
    │
    ├── Configurations/
    │   ├── BlockTextureConfig.cs      Data classes + static loader for block texture layers
    │   └── TextureConfig.json         Texture-array layer indices for each block face
    │
    ├── Utilities/
    │   ├── ChunkCoord.cs              Value type for chunk grid coordinates
    │   ├── CoreAvailability.cs        Thread-budget helper (allocates CPU cores per task)
    │   └── MathHelper.cs              Degrees-to-radians helper
    │
    └── texture/
        ├── example.png                Terrain texture atlas (tile sheet)
        ├── Texture.cs                 OpenGL 2D texture upload & binding
        └── TextureArray.cs            OpenGL Texture2DArray built from a tile atlas
```

---

## 3. Application Lifecycle

```
Program.Main()
  └─ MainClass.Run()
       ├─ WindowSetup.CreateWindow()      (create 800×600 VSync window)
       ├─ register window events
       │     OnLoad, OnRender, OnUpdate, OnFramebufferResize
       └─ WindowSetup.Run()               (blocks until window closes)
             │
             ├─ OnLoad()
             │    ├─ Renderer (new instance)
             │    ├─ Camera created at (0, 40, 0)
             │    ├─ Renderer.Setup(camera)         init OpenGL, shaders, TextureArray
             │    ├─ Movement.SetPlayerCamera()     set camera reference
             │    ├─ InputManager.Initialize()      bind keyboard + mouse
             │    ├─ map key-bindings → callbacks   (Close, Fullscreen, Borderless, ToggleDebugCamera)
             │    ├─ BlockTextures.Initialize()     load TextureConfig.json
             │    ├─ CoreAvailability.Initialize()  compute thread budget
             │    ├─ build terrain pipeline
             │    │     TerrainGenerator → ChunkProvider → ChunkRequestor
             │    └─ Camera.ForceChunkUpdate()      trigger initial chunk load
             │
             ├─ OnRender(deltaTime)  [every frame]
             │    ├─ Renderer.Clear()
             │    └─ Renderer.Render()
             │
             ├─ OnUpdate(deltaTime)  [every frame]
             │    └─ Movement.MovementUpdate(deltaTime)
             │
             └─ OnFramebufferResize(size)
                  └─ Renderer.FramebufferResize()
```

---

## 4. Systems Overview

### 4.1 Terrain Pipeline

The terrain pipeline is a multi-stage chain assembled in `MainClass.OnLoad()`:

```
TerrainGenerator  (NoiseCalculator + FastNoise2)
      │  GenerateChunk(ChunkCoord) → byte[32768]
      ▼
ChunkProvider                  ← central chunk cache; owns meshing worker threads
      │  RequestChunk → data written to Chunkdata (ConcurrentDictionary<ChunkCoord, byte[]>)
      │  MeshingQueue / UploadQueue (ConcurrentQueue)
      ▼
Lod0Mesher (extends BaseMesher) ← built on a worker thread; GPU upload deferred to main thread
      ▼
ChunkRequestor                 ← listens to Camera.OnChunkChanged
      │  calculates which ChunkCoords are within render distance (radius 16)
      └─ calls ChunkProvider.RequestChunk (via Parallel.For) / UnloadChunk
```

**How a chunk goes from noise to screen:**

1. The player's `Camera` fires `OnChunkChanged` whenever the player crosses a chunk boundary.
2. `ChunkRequestor.OnPlayerChunkChanged()` iterates all chunk coordinates within a circular radius of **16 chunks** and calls `ChunkProvider.RequestChunk()` for each, **in parallel** via `Parallel.For`.
3. `ChunkProvider.RequestChunk()` checks its in-memory cache (`LoadedChunks`). If the chunk is absent it calls `TerrainGenerator.GenerateChunk()`, stores the resulting `byte[]` block data in `Chunkdata`, and queues the chunk for meshing once all its neighbours are also present.
4. `TerrainGenerator.GenerateChunk()` delegates noise evaluation to `NoiseCalculator`, which uses the **FastNoise2** library to produce 4D simplex noise with torus-mapping. The result is a flat `byte[32768]` (`32×32×32`) block array.
5. A dedicated **meshing worker thread** inside `ChunkProvider` dequeues coordinates from `MeshingQueue`, constructs a `Lod0Mesher`, and places the finished mesher into `UploadQueue`. **No OpenGL calls are made here.**
6. On each render frame, `Renderer.Render()` drains `ChunkProvider.UploadQueue`: for each queued `BaseMesher` it calls `UploadToGpu(gl)` on the main thread (time-capped to ~2 ms per frame to avoid stalling) and adds the chunk to `LoadedChunks`.
7. `Renderer.Render()` then performs a **frustum cull** using `Frustum.isInFrustum()` and skips any chunk whose AABB lies entirely outside the camera frustum.
8. Visible chunks are rendered by calling `chunk.Render(shaderManager)`.

**Block type assignment in `TerrainGenerator.GenerateChunk()`:**

| Block ID | Type | Condition |
|---|---|---|
| 0 | Air | `y > height` |
| 1 | Dirt / Grass | surface layers (`y > height - 2` and `y <= 20`) |
| 2 | Stone | deep underground (`y <= height - 2`) or mid-height (`y > 20`) |
| 3 | Snow | peaks (`y > 28`) |

### 4.2 Rendering Pipeline

Every frame, `Renderer.Render()` executes the following sequence:

```
ShaderManager.Use(gl, camera)  → returns Frustum
  ├─ gl.Enable(DepthTest + CullFace)
  ├─ _shader.Use()                       activate GLSL program
  ├─ compute View matrix from Camera (or DebugCamera if active)
  ├─ compute Projection matrix (45° FOV, near=0.1, far=1000)
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

`InputManager` is a class with all-static members that abstracts Silk.NET's raw keyboard and mouse input into a double-layer mapping:

```
Physical key  ──→  Actions (enum)  ──→  C# Action delegate
(Key.W)              Forward              camera.Move(…)
```

**Layer 1 – Key ↔ Action mapping** (`_keyBindings: Dictionary<Actions, Key>`):

| Action | Default Key |
|---|---|
| Close | Escape |
| Fullscreen | F11 |
| Borderless | F12 |
| ToogleDebugCamera | F1 |
| Forward | W |
| Backward | S |
| Left | A |
| Right | D |
| Up | Space |
| Down | Left Shift |

**Layer 2 – Action ↔ Callback mapping** (`_actionBindings: Dictionary<Actions, Action>`):  
Registered via `InputManager.SetActionBindings(action, callback)`. Callbacks are invoked on `KeyDown`. Continuous movement is polled per-frame in `Movement.MovementUpdate()` via `InputManager.IsActionPressed()`.

**Mouse handling:**  
`OnMouseMove` forwards the raw position to `Movement.LookUpdate()`, which calculates the delta from the last position and calls `camera.Rotate(deltaYaw, deltaPitch)`.

### 4.4 Chunk Lifecycle

```
State machine for a single ChunkCoord:

  [Unloaded]
      │  ChunkProvider.RequestChunk()  (called in parallel via Parallel.For)
      │    1. already in LoadedChunks? → stay [GPU-Loaded]
      │    2. TryLoadFromDisk()?       → [Data-Ready]  (stub, always false)
      │    3. TerrainGenerator.GenerateChunk() → byte[] stored in Chunkdata
      ▼
  [Data-Ready]  (block data in Chunkdata; waiting for all 4 XZ neighbours)
      │  ChunkProvider.TryQueueForMeshing()  (called after each neighbour arrives)
      │    → all neighbours present? → added to MeshingQueue
      ▼
  [Meshing-Queued]  (worker thread picks up coord from MeshingQueue)
      │  Lod0Mesher constructor runs BuildMeshData()   (background thread)
      │  Finished mesher pushed to UploadQueue
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

`ChunkRequestor` drives the transitions: on every `OnChunkChanged` event it computes the new set of active chunk coordinates, requests new chunks in parallel, and diffs against the previous set to unload chunks that moved out of range.

---

## 5. Class Reference

### 5.1 Program & MainClass

#### `Program` (`Program.cs`)
**Namespace:** `Basics`  
Entry point of the application.

| Member | Description |
|---|---|
| `Main(string[] args)` | Creates a `MainClass` instance and calls `Run()`. |

---

#### `MainClass` (`MainClass.cs`)
**Namespace:** `Basics`  
Owns the application controller. Coordinates all subsystems during the window event loop.

| Member | Description |
|---|---|
| `PlayerCamera` (static `Camera`) | The main player camera; shared with `Movement` and `Renderer`. |
| `DebugCamera` (static `Camera?`) | Optional second free-cam used for debugging. When non-null the renderer draws from this camera's view while frustum culling still uses `PlayerCamera`. |
| `Run()` | Creates the window, registers event handlers, starts the run-loop, and disposes the window on exit. |
| `OnLoad()` | Calls `CoreAvailability.Initialize()`, creates `Renderer` and `PlayerCamera` at (0, 40, 0), calls `Renderer.Setup(camera)`, sets the player camera on `Movement`, sets up `InputManager`, loads block texture config, builds the terrain pipeline (`TerrainGenerator` → `ChunkProvider` with meshing worker threads → `ChunkRequestor`), exports a debug noise-map PNG, and triggers the first chunk load. |
| `OnRender(double deltaTime)` | Clears the frame buffer and calls `Renderer.Render()`. |
| `OnUpdate(double deltaTime)` | Calls `Movement.MovementUpdate(deltaTime)` for player movement. |
| `OnFramebufferResize(Vector2D<int>)` | Passes the new size to `Renderer.FramebufferResize()` to update the OpenGL viewport. |
| `ToggleFullscreen()` (private) | Toggles between fullscreen and normal windowed mode. |
| `ToggleBorderless()` (private) | Toggles between borderless-maximised and normal windowed mode. |
| `ToggleDebugCamera()` (private) | Creates a `DebugCamera` at the player's current position/orientation and redirects `Movement` to control it. Calling again destroys the debug camera and returns control to `PlayerCamera`. |

---

### 5.2 Setup

#### `WindowSetup` (`Setup/WindowSetup.cs`)
**Namespace:** `Basics.Setup`  
Static façade for Silk.NET window creation.

| Member | Description |
|---|---|
| `window` (static `IWindow`) | The active Silk.NET window instance. |
| `CreateWindow()` | Creates a 800×600 VSync window titled "Terrain Generator". |
| `Run()` | Starts the Silk.NET run-loop (blocks until closed). |

---

### 5.3 Game

#### `Camera` (`Game/Camera.cs`)
**Namespace:** `Basics.Game`  
First-person camera that manages the view matrix, frustum creation, and notifies listeners when the player changes chunks.

| Member | Description |
|---|---|
| `Position` (`Vector3`) | World-space position of the camera. |
| `Front` (`Vector3`) | Normalised look direction (default: −Z). |
| `GlobalUp` (`Vector3`) | World up vector (always +Y). |
| `Up` (`Vector3`, computed) | Up vector relative to current pitch: `Cross(Right, Front)` normalised. |
| `Right` (`Vector3`, computed) | Cross product of `Front` and `GlobalUp`, normalised. |
| `Yaw` (`float`) | Horizontal rotation angle in degrees (default: −90°). |
| `Pitch` (`float`) | Vertical rotation angle in degrees (default: 0°, clamped to ±89°). |
| `nearPlane` (`float`) | Near clip plane distance (default: 0.1). |
| `farPlane` (`float`) | Far clip plane distance (default: 1000). |
| `fovY` (`float`) | Vertical field of view in degrees (default: 45). |
| `AspectRatio` (`float`) | Viewport aspect ratio; updated by `Renderer.FramebufferResize()`. |
| `OnChunkChanged` (`event Action<ChunkCoord>?`) | Fired when the camera crosses a chunk boundary, or manually via `ForceChunkUpdate()`. |
| `ForceChunkUpdate()` | Recalculates the current chunk and fires `OnChunkChanged`. Used at startup to seed the chunk loader. |
| `GetViewMatrix()` | Returns the `Matrix4x4` look-at matrix for use in the shader. |
| `Move(Vector3 direction)` | Translates the camera relative to its current heading (XZ grounded, Y free-fly). Checks for a chunk boundary crossing after every move. |
| `Rotate(float deltaYaw, float deltaPitch)` | Updates `Yaw` and `Pitch` from mouse delta values, clamping pitch to ±89°. Recomputes `Front`. |
| `CreateFrustum(Matrix4x4 view, Matrix4x4 projection)` | Builds a `Frustum` from the combined view-projection matrix using Gribb/Hartmann plane extraction. Used each frame for frustum culling. |

**Chunk detection detail:**  
`GetChunkCoord(Vector3 pos)` divides world position by 32 (chunk size) using `MathF.Floor` to correctly handle negative coordinates.

---

#### `GameLogic` (`Game/GameLogic.cs`)
**Namespace:** `Basics.Game`  
Currently empty placeholder class intended for future physics and dropped-item management.

---

#### `Movement` (`Game/Movement.cs`)
**Namespace:** `Basics.Input`  
Static helper class responsible for translating input state into camera movement each frame.

| Member | Description |
|---|---|
| `SetPlayerCamera(Camera playerCamera)` | Sets the camera that `MovementUpdate` and `LookUpdate` will control. Called once at startup and again when the debug camera is toggled. |
| `MovementUpdate(double deltaTime)` | Polls `InputManager.IsActionPressed()` for all directional actions, assembles a direction vector, normalises it, scales by `Speed * deltaTime`, and calls `camera.Move()`. |
| `LookUpdate(Vector2 mousePosition)` | Computes the mouse delta from the last stored position and calls `camera.Rotate()` with the result scaled by `Sensitivity`. |

Constants: `Speed = 12f`, `Sensitivity = 0.1f`.

---

### 5.4 TerrainManaging

#### `TerrainGenerator` (`Game/TerrainManaging/Generation/TerrainGenerator.cs`)
**Namespace:** `Basics.Game.TerrainManaging.Generation`  
Generates chunk block data using 4D noise via `NoiseCalculator`.

| Member | Description |
|---|---|
| `SetMapSize(int size)` | Sets the total number of chunks across both axes. `radius = size/2`, `mapLimit = radius * 32`. Must be called before `GenerateChunk()`. |
| `GenerateChunk(ChunkCoord coord)` | Generates a flat `byte[32768]` (`32×32
