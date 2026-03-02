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
| `GenerateChunk(ChunkCoord coord)` | Generates a flat `byte[32768]` (`32×32×32`) block array for the given chunk and returns it. Uses `NoiseCalculator.GetNoiseValues()` for height data. Logs a warning if the requested chunk lies outside world limits. |
| `DebugExportNoiseMap(string filename)` | Exports a greyscale PNG of the noise map across the whole world using `SixLabors.ImageSharp`. Red pixels indicate heights below 0, blue pixels heights above 31. Saved to the working directory. |

**Torus mapping** explanation:  
World coordinates are projected onto a 4D torus inside `NoiseCalculator`. Each axis coordinate is converted to an angle `θ = coord / mapLimit * 2π`, then expanded to a unit-circle pair `(cos θ, sin θ)`, giving a 4-component input `(cosX, sinX, cosZ, sinZ)` to the noise function. This ensures seamless world edges.

---

#### `NoiseCalculator` (`Game/TerrainManaging/Generation/Noise/NoiseCalculator.cs`)
**Namespace:** `Basics.Game.TerrainManaging.Generation`  
Encapsulates FastNoise2 node configuration and batch noise evaluation. Created and owned by `TerrainGenerator`.

| Member | Description |
|---|---|
| `SetMapSize(int size)` | Mirrors `TerrainGenerator.SetMapSize()`; stores map limits for torus-angle calculation. |
| `GetNoiseValues(int startX, int startZ, int sizeX, int sizeZ)` | Lazily initialises the FastNoise2 node graph on first call, builds 4D torus-mapped coordinate arrays, then calls `GenPositionArray4D()` for SIMD batch evaluation. Returns a `float[]` of `sizeX × sizeZ` height values. |

Constants: `Scale = 25f` (domain scale, controls terrain zoom), `multiplicator = 13f` (output scale, controls terrain height amplitude), `Seed = 1223456789`.

FastNoise2 node graph: `Simplex` → `DomainScale(Scale)` → `Multiply(multiplicator)`.

---

#### `FastNoise2` (`Game/TerrainManaging/Generation/Noise/FastNoise2.cs`)
**Namespace:** *(global)*  
C# P/Invoke wrapper for the native `FastNoise.dll` (FastNoise2 library). Provides a node-graph API for constructing noise pipelines and evaluating them in bulk. Used exclusively through `NoiseCalculator`.

Key method used: `GenPositionArray4D(float[] output, float[] xPos, float[] yPos, float[] zPos, float[] wPos, float xOffset, float yOffset, float zOffset, float wOffset, int seed)` — evaluates 4D noise for each position tuple and writes results into `output`.

This file is a third-party library included directly in the project source.

---

#### `BaseMesher` (`Game/TerrainManaging/Meshing/BaseMesher.cs`)
**Namespace:** `Basics.Game.TerrainManaging.Meshing`  
Abstract base class for all chunk mesh builders. Manages the per-chunk OpenGL buffer objects and provides the `UploadToGpu`, `Render`, and `Dispose` lifecycle.

| Member | Description |
|---|---|
| `ChunkPosition` (`ChunkCoord`) | Chunk-grid position of this mesh. |
| `IsUploaded` (`bool`, read-only) | `true` after `UploadToGpu()` has been called. |
| `UploadToGpu(GL gl)` | Creates the VBO, EBO, and VAO from `_vertices`/`_indices` and uploads them. **Must be called on the OpenGL (main) thread.** Idempotent. Clears the CPU-side lists after upload to free memory. |
| `Render(ShaderManager shaderManager)` | Sets the model matrix uniform, binds VAO + EBO, issues `DrawElements`. No-ops if not uploaded. |
| `AddIndices(uint baseIndex)` | Helper: appends the two triangles (0,1,2 and 0,2,3) for a quad starting at `baseIndex`. Used by subclasses that do not need AO-based diagonal flipping. |
| `Dispose()` | Deletes VBO, EBO, VAO. |

Protected fields available to subclasses: `_vertices` (`List<float>`), `_indices` (`List<uint>`), `model` (`Matrix4x4`), `_indicesCount` (`uint`).

---

#### `Lod0Mesher` (`Game/TerrainManaging/Meshing/Lod0Mesher.cs`)
**Namespace:** `Basics.Game.TerrainManaging`  
Full-detail (LOD 0) mesh builder. Extends `BaseMesher`. Constructed and run on a meshing worker thread; GPU upload is deferred to the main thread via `BaseMesher.UploadToGpu`.

| Member | Description |
|---|---|
| `Lod0Mesher(ChunkCoord position, byte[] blockData)` | Stores the block data, immediately calls `BuildMeshData()`. **No OpenGL calls.** |

**Mesh generation detail (`BuildMeshData`):**  
Iterates every block in the 32×32×32 array (flat index `x*1024 + y*32 + z`). For each non-air block it checks all 6 neighbours; if a neighbour is air (or out-of-bounds, resolved via `ChunkProvider.Chunkdata`), the corresponding face is added. Each face consists of 4 vertices stored as 5 floats per vertex `(x, y, z, textureLayer, brightness)`, and 2 triangles (6 indices). The texture layer comes from `BlockTextures.Get(blockId, faceIndex)`. Per-vertex AO brightness is computed by `CalcAoLevel()` using a pre-baked `AoOffsets[6,4,3,3]` lookup table, then mapped via `AoLookup = { 1.0f, 0.8f, 0.6f, 0.4f }`. The quad diagonal is flipped when `b0+b2 > b1+b3` to avoid AO interpolation artifacts.

---

#### `LOD1Mesher` / `LOD2Mesher` / `LOD3Mesher` (`Game/TerrainManaging/Meshing/`)
**Namespace:** `Basics.Game.TerrainManaging`  
Stub classes for lower LOD levels. Not yet implemented; bodies are empty.

---

#### `ChunkProvider` (`Game/TerrainManaging/ChunkProvider.cs`)
**Namespace:** `Basics.Game.TerrainManaging`  
Central in-memory registry for all live chunk data and meshes. Owns a pool of meshing worker threads.

| Member | Description |
|---|---|
| `LoadedChunks` (static `Dictionary<ChunkCoord, BaseMesher>`) | GPU-ready meshes indexed by chunk coordinate. |
| `Chunkdata` (static `ConcurrentDictionary<ChunkCoord, byte[]>`) | Raw block data for all generated chunks (including those not yet meshed). |
| `MeshingQueue` (`ConcurrentQueue<ChunkCoord>`) | Coords whose block data and all XZ neighbours are ready for meshing. |
| `UploadQueue` (`ConcurrentQueue<BaseMesher>`) | Finished CPU-side meshes waiting for GPU upload on the main thread. |
| `ChunkProvider(TerrainGenerator terrainGenerator, int meshingThreads)` | Starts `meshingThreads` background `Task`s running `MeshingWorkerLoop`. |
| `RequestChunk(ChunkCoord coord)` | Generates block data via `TerrainGenerator` if the chunk is absent, then calls `OnChunkDataGenerated`. Thread-safe. |
| `OnChunkDataGenerated(ChunkCoord coord, byte[] data)` | Stores block data in `Chunkdata`, then calls `TryQueueForMeshing` for the chunk and all 6 neighbours. |
| `UnloadChunk(ChunkCoord coord)` | Disposes the `BaseMesher`, removes from `LoadedChunks` and `Chunkdata`. |
| `GetLoadedChunks()` | Returns all GPU-ready `BaseMesher` instances (used by `Renderer`). |
| `IsChunkLoaded(ChunkCoord coord)` | Returns `true` if `coord` is in `LoadedChunks`. |
| `Dispose()` | Disposes all loaded meshes and clears the dictionary. |

`TryLoadFromDisk()` is a stub that always returns `false`; disk persistence is a planned feature.

**Neighbour requirement:** A chunk is only queued for meshing once all four horizontal XZ neighbours (`±X`, `±Z`) have block data in `Chunkdata`. This prevents seam artifacts at chunk borders because `Lod0Mesher` reads neighbour block data directly during AO and face-visibility checks.

---

#### `ChunkRequestor` (`Game/TerrainManaging/ChunkRequestor.cs`)
**Namespace:** `Basics.Game`  
Subscribes to `Camera.OnChunkChanged` and drives chunk loading/unloading based on the player's current chunk coordinate.

| Member | Description |
|---|---|
| `RenderDistance` (`int`, default 16) | Radius in chunks around the player that should be loaded. Minimum 1. |
| `ChunkRequestor(Camera camera, ChunkProvider chunkProvider, int availableCores)` | Constructor. Subscribes `OnPlayerChunkChanged` to `camera.OnChunkChanged`. Configures `Parallel.For` with `availableCores` as the degree of parallelism. |

**Algorithm in `OnPlayerChunkChanged(ChunkCoord playerChunk)`:**

1. Build `_chunksToLoad`: iterate `(-RenderDistance … +RenderDistance)²` on the XZ plane, skip coordinates where `x²+z² > RenderDistance²` (circular mask).
2. Call `ChunkProvider.RequestChunk()` for each coordinate in parallel via `Parallel.For`.
3. Diff against `_activeChunks` (previous set): call `ChunkProvider.UnloadChunk()` for every chunk that was active before but is not in the new set.
4. Replace `_activeChunks` with the new set.

---

### 5.5 Graphics

#### `Renderer` (`Graphics/Renderer.cs`)
**Namespace:** `Basics.Graphics`  
Instance class that owns global OpenGL resources and drives the render loop.

| Member | Description |
|---|---|
| `gl` (static `GL`) | Silk.NET OpenGL context. |
| `terrainshader` (static `ShaderManager`) | The shader used for all terrain chunks. |
| `terrainTexture` (static `TextureArray`) | The terrain atlas as a `Texture2DArray`. |
| `ChunkProvider` (static `ChunkProvider`) | Set from `MainClass.OnLoad()`; provides chunks for rendering. |
| `Setup(Camera camera)` | Creates the GL context, sets the clear colour, stores the camera reference, constructs the shader and terrain texture array. |
| `Render()` | Calls `ShaderManager.Use()` (returns a `Frustum`), binds the texture array, drains `ChunkProvider.UploadQueue` (time-capped ~2 ms) to upload pending meshes, frustum-culls each chunk in `LoadedChunks`, and renders visible chunks. |
| `Clear()` | Clears the colour and depth buffers. |
| `Dispose()` | Disposes the shader, texture array, chunk provider, and GL context. |
| `FramebufferResize(Vector2D<int> size)` | Updates the OpenGL viewport and sets `Camera.AspectRatio`. |

---

#### `Shader` (`Graphics/Shader.cs`)
**Namespace:** `Basics.Graphics`  
Compiles a GLSL vertex/fragment shader pair into a linked program and provides type-safe uniform setters.

| Member | Description |
|---|---|
| `Shader(GL gl, string vertexPath, string fragmentPath)` | Loads, compiles, and links both shaders. Throws `Exception` on compile or link failure. Detaches and deletes the individual shader objects after linking. |
| `Use()` | Binds the shader program with `glUseProgram`. |
| `SetUniform(string name, int value)` | Uploads an `int` uniform. Throws if the name is not found. |
| `SetUniform(string name, float value)` | Uploads a `float` uniform. Throws if the name is not found. |
| `SetUniform(string name, Matrix4x4 value)` | Uploads a `mat4` uniform. Silently skips if the uniform is not found (returns early). |
| `Dispose()` | Deletes the linked program with `glDeleteProgram`. |

---

#### `ShaderManager` (`Graphics/ShaderManager.cs`)
**Namespace:** `Basics.Graphics`  
High-level wrapper around `Shader` that sets the MVP matrices and binds textures.

| Member | Description |
|---|---|
| `ShaderManager(GL gl, string vertexShaderFile, string fragmentShaderFile)` | Prepends the `Graphics/Shader/` path prefix and constructs a `Shader`. |
| `Use(GL gl, Camera camera)` | Enables depth testing and back-face culling, activates the shader, uploads the view and projection matrices (`uView`, `uProjection`) and `uTexture = 0`. Builds and returns a `Frustum` from the VP matrix. If `MainClass.DebugCamera` is non-null the debug camera's matrices are sent to the shader instead. |
| `SetModelMatrix(Matrix4x4 model)` | Uploads the per-object `uModel` uniform. |
| `BindTexture(TextureArray texture)` | Binds the `TextureArray` to `TextureUnit.Texture0`. |
| `Dispose()` | Forwards to `Shader.Dispose()`. |

---

#### `Texture` (`texture/Texture.cs`)
**Namespace:** `Basics.Graphics`  
Manages a single OpenGL 2D texture.

| Member | Description |
|---|---|
| `Texture(GL gl, string path)` | Loads an image from disk using StbImageSharp, uploads it as RGBA, and applies texture parameters + mipmaps. |
| `Texture(GL gl, Span<byte> data, uint width, uint height)` | Creates a texture from raw byte data generated at runtime. |
| `Bind(TextureUnit textureSlot)` | Activates the given texture unit and binds this texture. |
| `Dispose()` | Deletes the OpenGL texture handle. |

Texture parameters: wrap mode `ClampToEdge`, minification filter `NearestMipmapNearest`, magnification filter `Nearest`, anisotropic filtering ×16, 8 mipmap levels.

---

#### `TextureArray` (`texture/TextureArray.cs`)
**Namespace:** `Basics.Graphics`  
Manages an OpenGL `Texture2DArray` built from a tile-sheet atlas. Used for all block textures.

| Member | Description |
|---|---|
| `TextureArray(GL gl, string atlasPath, int tileSize = 32)` | Loads the atlas image, slices it into `tileSize × tileSize` tiles (row-major), flips each tile vertically for correct OpenGL orientation, and uploads the result as a `Texture2DArray`. |
| `Bind(TextureUnit textureSlot)` | Activates the given texture unit and binds the `Texture2DArray`. |
| `Dispose()` | Deletes the OpenGL texture handle. |

Texture parameters: wrap mode `ClampToEdge`, minification filter `NearestMipmapNearest`, magnification filter `Nearest`, anisotropic filtering ×16, 8 mipmap levels.

---

#### `Frustum` (`Graphics/Frustum.cs`)
**Namespace:** `Basics.Graphics`  
Axis-aligned bounding-box frustum culling using six half-space planes.

| Member | Description |
|---|---|
| `TopFace`, `BottomFace`, `LeftFace`, `RightFace`, `NearFace`, `FarFace` (`Plane`) | The six clip planes extracted from the view-projection matrix. |
| `isInFrustum(ChunkCoord chunk, Frustum frustum)` | Returns `true` if the chunk's AABB (centred at `16 + 32*coord`, half-extents 16×16×16) is not entirely behind any of the six planes. |

#### `Plane` (`Graphics/Frustum.cs`)
**Namespace:** `Basics.Graphics`  
A half-space plane defined by a normal and a distance offset.

| Member | Description |
|---|---|
| `Normal` (`Vector3`) | Unit normal pointing towards the inside of the frustum. |
| `Distance` (`float`) | Signed distance from the origin along the normal. |
| `GetDistanceToPoint(Vector3 point)` | Returns the signed distance from the plane to `point` (positive = in front). |

---

#### `BufferObject<TDataType>` (`Graphics/BufferObject.cs`)
**Namespace:** `Basics.Graphics`  
Generic wrapper for an OpenGL Buffer Object (VBO or EBO).

| Member | Description |
|---|---|
| `BufferObject(GL gl, Span<TDataType> data, BufferTargetARB bufferType)` | Generates a buffer, binds it, and uploads `data` with `StaticDraw` usage. |
| `Bind()` | Binds the buffer to its stored target (`ArrayBuffer` or `ElementArrayBuffer`). |
| `Dispose()` | Calls `glDeleteBuffer`. |

---

#### `VertexArrayObject<TVertexType, TIndexType>` (`Graphics/VertexArrayObject.cs`)
**Namespace:** `Basics.Graphics`  
Wraps an OpenGL Vertex Array Object, linking a VBO and EBO.

| Member | Description |
|---|---|
| `VertexArrayObject(GL gl, BufferObject<TVertexType> vbo, BufferObject<TIndexType> ebo)` | Generates the VAO, binds it, and immediately binds the VBO and EBO to associate them. |
| `VertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint vertexSize, int offSet)` | Configures a vertex attribute pointer and enables the attribute array. |
| `Bind()` | Binds the VAO. |
| `Dispose()` | Calls `glDeleteVertexArray`. Does **not** delete the linked VBO/EBO (they may be shared). |

---

#### `Transform` (`Graphics/Transform.cs`)
**Namespace:** `Basics.Graphics`  
Utility class that computes a model matrix from position, rotation, and scale.

| Member | Description |
|---|---|
| `Position` (`Vector3`, default (0,0,0)) | Translation component. |
| `Scale` (`float`, default 1) | Uniform scale. |
| `Rotation` (`Quaternion`, default Identity) | Rotation component. |
| `ModelMatrix` (computed `Matrix4x4`) | `Identity * CreateFromQuaternion(Rotation) * CreateScale(Scale) * CreateTranslation(Position)`. |

> Note: `Transform` is currently not used by any active code path; chunks use a `Matrix4x4` directly in `Lod0Mesher`.

---

### 5.6 Input

#### `Actions` (enum, `Input/InputManager.cs`)
**Namespace:** `Basics.Input`  
Defines all game actions that can be bound to keys.

`Close`, `Fullscreen`, `Borderless`, `ToogleDebugCamera`, `Up`, `Down`, `Left`, `Right`, `Forward`, `Backward`

---

#### `InputManager` (`Input/InputManager.cs`)
**Namespace:** `Basics.Input`  
Static class. Handles all keyboard and mouse input and dispatches them to game logic.

| Member | Description |
|---|---|
| `Initialize(IInputContext input)` | Grabs the first keyboard and mouse from the Silk.NET input context. Hooks `KeyDown`, `KeyUp`, `MouseMove`, and `Scroll` events. Calls `DefaultKeyBindings()`. |
| `SetkeyBindings(Actions action, Key key)` | Adds or updates the `Actions → Key` mapping. |
| `SetActionBindings(Actions action, Action method)` | Adds or updates the `Actions → callback` mapping (for on-press events). |
| `IsKeyPressed(IKeyboard keyboard, Key key)` | Queries the cached keyboard for the raw key state. |
| `IsActionPressed(Actions action)` | Looks up the key bound to `action` and queries the keyboard. Used for continuous per-frame polling. |

Mouse cursor mode is set to `CursorMode.Raw` on initialisation (hidden, unlimited movement).

---

### 5.7 Configurations

#### `BlockFaces` (`Configurations/BlockTextureConfig.cs`)
**Namespace:** `Basics.Configurations`  
Groups the six texture-array layer indices (`Top`, `Bottom`, `Front`, `Back`, `Left`, `Right`) for one block type. Each field is a `byte` holding the layer index within the `TextureArray`.

---

#### `BlockTextureEntry` / `BlockTextureConfigRoot`
Data-transfer objects used when deserialising `TextureConfig.json`. `BlockTextureConfigRoot` is the top-level object containing a `List<BlockTextureEntry>`.

---

#### `BlockTextures` (static class, `Configurations/BlockTextureConfig.cs`)
**Namespace:** `Basics.Configurations`  
Static loader and lookup table for block face texture-array layers.

| Member | Description |
|---|---|
| `Top`, `Bottom`, `Front`, `Back`, `Left`, `Right` (constants `byte`) | Face index constants (0–5) used throughout the meshing code. |
| `Initialize(string jsonPath)` | Deserialises `TextureConfig.json` and builds a `byte[blockId, faceIndex]` lookup table. Idempotent (skips if already loaded). |
| `Get(int blockId, int faceIndex)` | Returns the `byte` texture-array layer for the given block and face. Throws `InvalidOperationException` if `Initialize()` has not been called. |

---

### 5.8 Utilities

#### `CoreAvailability` (`Utilities/CoreAvailability.cs`)
**Namespace:** `Basics.Utilities`  
Static helper that distributes available CPU cores across different background tasks.

| Member | Description |
|---|---|
| `TotalCores` (static `int`) | `Environment.ProcessorCount`. |
| `AvailableCores` (static `int`) | `TotalCores - 2` (reserves one core each for the render and logic threads). Minimum 1. |
| `Initialize()` | Loads `coreconfig.txt` if present, otherwise calls `DefaultConfig()` which reserves 2 cores for meshing and the rest for terrain generation. |
| `GetTerrainGenerationCores()` | Returns the number of cores to pass to `Parallel.For` in `ChunkRequestor`. |
| `GetChunkMeshingCores()` | Returns the number of meshing worker threads to start in `ChunkProvider`. |

---

#### `ChunkCoord` (`Utilities/ChunkCoord.cs`)
**Namespace:** `Basics.Utilities`  
Immutable value type (`struct`) representing a chunk's position in the chunk grid (not block-world coordinates, except when used as the chunk's world-block origin in `Lod0Mesher`).

| Member | Description |
|---|---|
| `X`, `Y`, `Z` (`int`, readonly) | Grid coordinates. `Y` is always 0 in the current implementation. |
| `ChunkCoord(int x, int y, int z)` | Constructor. |
| `Equals`, `GetHashCode`, `==`, `!=` | Value equality based on all three components. |
| `ToString()` | Returns `"(X, Y, Z)"`. |

Implements `IEquatable<ChunkCoord>` and overrides `GetHashCode` using `HashCode.Combine` so it can be used safely as a `Dictionary` key or in a `HashSet`.

---

#### `MathHelper` (`Utilities/MathHelper.cs`)
**Namespace:** `Basics.Utilities`  
Static utility class.

| Member | Description |
|---|---|
| `DegreesToRadians(float degrees)` | Converts degrees to radians using `MathF.PI / 180f * degrees`. |

---

## 6. Shaders

Both GLSL shaders are in `Graphics/Shader/` and are copied to the output directory on build.

### `shader.vert` — Vertex Shader (GLSL 4.60 Core)

**Inputs:**

| Attribute | Location | Type | Description |
|---|---|---|---|
| `aPos` | 0 | `vec3` | Local block position |
| `aLayer` | 1 | `float` | Texture2DArray layer index |
| `brightness` | 2 | `float` | Per-vertex AO brightness (0.4–1.0) |

**Uniforms:**

| Name | Type | Description |
|---|---|---|
| `uModel` | `mat4` | Per-chunk model matrix (translation to world position) |
| `uView` | `mat4` | Camera view matrix |
| `uProjection` | `mat4` | Perspective projection matrix |

**Outputs:** `fragTexCoords` (`vec3`, xy = computed UV, z = layer), `fragbrightness` (`float`) forwarded to the fragment shader.  
**UV computation:** UV coordinates are derived from `gl_VertexID % 4` using a fixed 4-entry look-up table `[(0,0),(1,0),(1,1),(0,1)]` instead of being stored per vertex.  
**Clip-space calculation:** `gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0)`

### `shader.frag` — Fragment Shader (GLSL 4.60 Core)

**Inputs:** `fragTexCoords` (`vec3`) and `fragbrightness` (`float`) from the vertex shader.  
**Uniform:** `uTexture` (`sampler2DArray`) — the terrain atlas bound to texture unit 0.  
**Output:** `outColor = vec4(texture(uTexture, fragTexCoords).rgb * fragbrightness, texColor.a)` — samples the texture array and applies AO darkening.

---

## 7. Configuration Files

### `Configurations/TextureConfig.json`

Defines the `Texture2DArray` layer index for each face of each block type. The atlas `texture/example.png` is sliced into 32×32 tiles row-major; each tile becomes one layer (layer 0 = tile 0, layer 1 = tile 1, …).

| Block ID | Type | Face → Layer |
|---|---|---|
| 1 | Dirt/Grass | Top=0, Bottom=1, Front/Back/Left/Right=2 |
| 2 | Stone | All faces = 3 |
| 3 | Snow | All faces = 4 |

Block ID `0` (Air) has no entry; it is never rendered.

---

## 8. Dependencies

All dependencies are managed via NuGet (see `Terrain_Generator.csproj`).

| Package | Version | Purpose |
|---|---|---|
| `Silk.NET.Windowing` | 2.23.0 | Cross-platform window creation and the main run-loop |
| `Silk.NET.OpenGL` | 2.23.0 | OpenGL 4.6 bindings |
| `Silk.NET.Input` | 2.23.0 | Keyboard and mouse input |
| `StbImageSharp` | 2.30.15 | Image loading (PNG → raw RGBA bytes) for `Texture` and `TextureArray` |
| `SixLabors.ImageSharp` | 3.1.12 | Image creation and PNG export used in `TerrainGenerator.DebugExportNoiseMap()` |
| `System.Drawing.Common` | 10.0.3 | Included as a transitive dependency |

**Native libraries (shipped as DLLs):**

| File | Purpose |
|---|---|
| `FastNoise.dll` | FastNoise2 native library; P/Invoke'd by `FastNoise2.cs` for SIMD noise evaluation |
| `NodeEditorIpc.dll` | FastNoise2 node-editor IPC helper (bundled with FastNoise2) |

---

## 9. Known Limitations & Planned Features

The following items are tracked in the project README:

**High priority:**
- ~~Multi-threading for chunk generation and loading~~ *(done – `Parallel.For` in `ChunkRequestor` + meshing worker threads in `ChunkProvider`)*
- ~~Frustum culling (skip rendering chunks outside the camera's view frustum)~~ *(done – `Frustum` + `isInFrustum`)*
- ~~Improved chunk mesh generation~~ *(done – `BaseMesher`/`Lod0Mesher` hierarchy with AO-diagonal flip)*
- Level-of-detail (LOD) system *(framework started – `LOD1Mesher`, `LOD2Mesher`, `LOD3Mesher` stubs added)*
- Cubic / multi-height chunks
- More efficient block data storage
- Player object with collision
- Physics
- Disk persistence: saving and loading chunks
- Block breaking and placing
- General debugging tools *(debug camera added)*

**Low priority:**
- ~~Ambient occlusion~~ *(done – per-vertex AO baked into the mesh)*
- Directional and ambient shading
- Better terrain shapes and biomes
- Client ↔ Server architecture

**Future:**
- Trees and bushes
- Dynamic grass
- UI / main menu
