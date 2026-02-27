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

EdwinCraft is a voxel-based terrain renderer written in C# using Silk.NET for windowing and OpenGL. The player can fly freely through a procedurally generated world that is split into 32×32×32 block **chunks**. The terrain is generated using 4D OpenSimplex noise mapped onto a torus so that the world wraps seamlessly at its edges (no visible seams).

Key properties at a glance:

| Property | Value |
|---|---|
| Target Framework | .NET 10 |
| Chunk size | 32 × 32 × 32 blocks |
| Default world size | 32 × 32 chunks |
| Render distance | 6 chunks (radius) |
| Block types | 0 Air, 1 Dirt/Grass, 2 Stone, 3 Snow |
| Rendering API | OpenGL 3.3 Core via Silk.NET |

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
    │   ├── Camera.cs                  First-person camera (view matrix + chunk events)
    │   ├── GameLogic.cs               Placeholder for future physics / item logic
    │   ├── Movement.cs                Keyboard + mouse movement logic
    │   └── TerrainManaging/
    │       ├── TerrainGenerator.cs    Procedural chunk generation (4D noise)
    │       ├── ChunkMesher.cs         Greedy-ish mesh builder for a single chunk
    │       ├── ChunkProvidor.cs       Chunk lifecycle manager (load / cache / unload)
    │       └── ChunkRequestor.cs      Decides which chunks to load based on player position
    │
    ├── Graphics/
    │   ├── Renderer.cs                Central rendering façade
    │   ├── Shader.cs                  Low-level shader compile & uniform upload
    │   ├── ShaderManager.cs           High-level shader wrapper (MVP matrices, textures)
    │   ├── Texture.cs                 OpenGL texture upload & binding
    │   ├── BufferObject.cs            Generic VBO / EBO wrapper
    │   ├── VertexArrayObject.cs       VAO wrapper with attribute layout helpers
    │   ├── Transform.cs               Position / rotation / scale → Model matrix
    │   └── Shader/
    │       ├── shader.vert            GLSL vertex shader
    │       └── shader.frag            GLSL fragment shader
    │
    ├── Input/
    │   └── InputManager.cs            Keyboard & mouse dispatch; action binding
    │
    ├── Configurations/
    │   ├── BlockTextureConfig.cs      Data classes + static loader for block UVs
    │   └── TextureConfig.json         UV coordinates for each block face in the atlas
    │
    ├── Utilities/
    │   ├── ChunkCoord.cs              Value type for chunk grid coordinates
    │   ├── MathHelper.cs              Degrees-to-radians helper
    │   └── OpenSimplex2S.cs           K.jpg's OpenSimplex 2S noise implementation
    │
    └── texture/
        └── example.png                Terrain texture atlas
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
             │    ├─ Renderer.Setup()              init OpenGL, camera, shaders, textures
             │    ├─ InputManager.Initialize()     bind keyboard + mouse
             │    ├─ map key-bindings → callbacks  (Close, Fullscreen, Borderless)
             │    ├─ BlockTextures.Initialize()    load TextureConfig.json
             │    ├─ build terrain pipeline
             │    │     TerrainGenerator → ChunkProvidor → ChunkRequestor
             │    └─ Camera.ForceChunkUpdate()     trigger initial chunk load
             │
             ├─ OnRender(deltaTime)  [every frame]
             │    ├─ Renderer.Clear()
             │    └─ Renderer.Render()
             │
             ├─ OnUpdate(deltaTime)  [every frame]
             │    └─ Movement.MovementUpdate()
             │
             └─ OnFramebufferResize(size)
                  └─ Renderer.FramebufferResize()
```

---

## 4. Systems Overview

### 4.1 Terrain Pipeline

The terrain pipeline is a three-stage chain assembled in `MainClass.OnLoad()`:

```
TerrainGenerator
      │  GenerateChunk(ChunkCoord)
      ▼
ChunkProvidor                  ← central chunk cache (Dictionary<ChunkCoord, ChunkMesher>)
      │  RequestChunk / UnloadChunk / GetLoadedChunks
      ▼
ChunkRequestor                 ← listens to Camera.OnChunkChanged
      │  calculates which ChunkCoords are within render distance
      └─ calls ChunkProvidor.RequestChunk / UnloadChunk
```

**How a chunk goes from noise to screen:**

1. The player's `Camera` fires `OnChunkChanged` whenever the player crosses a chunk boundary.
2. `ChunkRequestor.OnPlayerChunkChanged()` iterates all chunk coordinates within a circular radius of 6 chunks and calls `ChunkProvidor.RequestChunk()` for each.
3. `ChunkProvidor.RequestChunk()` checks its in-memory cache. If the chunk is absent it calls `TerrainGenerator.GenerateChunk()`.
4. `TerrainGenerator.GenerateChunk()` uses **4D OpenSimplex noise** with torus-mapping to compute a height value for every XZ column in the 32×32 grid, fills a `int[32,32,32]` block array, then creates a `ChunkMesher`.
5. `ChunkMesher` immediately builds an OpenGL mesh (VBO / EBO / VAO) by iterating all blocks and emitting only visible faces (face culling against adjacent blocks).
6. Each render frame, `Renderer.Render()` iterates every `ChunkMesher` from `ChunkProvidor.GetLoadedChunks()` and calls `chunk.Render(shaderManager)`.

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
ShaderManager.Use(gl, camera)
  ├─ gl.Enable(DepthTest + CullFace)
  ├─ _shader.Use()                       activate GLSL program
  ├─ compute View matrix from Camera
  ├─ compute Projection matrix (45° FOV, near=0.1, far=1000)
  └─ upload uView, uProjection, uTexture uniforms

ShaderManager.BindTexture(terrainTexture)
  └─ Texture.Bind(Texture0)

for each ChunkMesher in ChunkProvidor.GetLoadedChunks():
  └─ ChunkMesher.Render(shaderManager)
       ├─ shaderManager.SetModelMatrix(model)   upload per-chunk uModel
       ├─ _vao.Bind()
       ├─ _ebo.Bind()
       └─ gl.DrawElements(Triangles, ...)
```

**Vertex layout per vertex** (stride = 5 floats):

| Attribute | Location | Components | Offset |
|---|---|---|---|
| `aPos` (world position) | 0 | 3 floats (x, y, z) | 0 |
| `aTexCoord` (UV) | 1 | 2 floats (u, v) | 3 |

### 4.3 Input System

`InputManager` is a static class that abstracts Silk.NET's raw keyboard and mouse input into a double-layer mapping:

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
      │  ChunkProvidor.RequestChunk()
      │    1. already in cache?  → stay [Loaded]
      │    2. TryLoadFromDisk()? → [Loaded]  (stub, always false)
      │    3. TerrainGenerator.GenerateChunk()
      ▼
  [Loaded]  (lives in _loadedChunks dictionary, GPU mesh allocated)
      │  ChunkProvidor.UnloadChunk()
      │    1. chunk.Dispose()     release VBO / EBO / VAO
      │    2. remove from cache
      ▼
  [Unloaded]
```

`ChunkRequestor` drives the transitions: on every `OnChunkChanged` event it computes the new set of active chunk coordinates and diffs it against the previous set to unload chunks that moved out of range.

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
| `Run()` | Creates the window, registers event handlers, starts the run-loop, and disposes the window on exit. |
| `OnLoad()` | Initialises OpenGL via `Renderer.Setup()`, sets up `InputManager`, loads block texture config, builds the terrain pipeline, and triggers the first chunk load. |
| `OnRender(double deltaTime)` | Clears the frame buffer and calls `Renderer.Render()`. |
| `OnUpdate(double deltaTime)` | Calls `Movement.MovementUpdate()` for player movement. |
| `OnFramebufferResize(Vector2D<int>)` | Passes the new size to `Renderer.FramebufferResize()` to update the OpenGL viewport. |
| `ToggleFullscreen()` (private) | Toggles between fullscreen and normal windowed mode. |
| `ToggleBorderless()` (private) | Toggles between borderless-maximised and normal windowed mode. |

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
First-person camera that manages the view matrix and notifies listeners when the player changes chunks.

| Member | Description |
|---|---|
| `Position` (`Vector3`) | World-space position of the camera. |
| `Front` (`Vector3`) | Normalised look direction (default: −Z). |
| `Up` (`Vector3`) | Up vector (default: +Y). |
| `Right` (`Vector3`, computed) | Cross product of `Front` and `Up`, normalised. |
| `OnChunkChanged` (`event Action<ChunkCoord>?`) | Fired when the camera crosses a chunk boundary, or manually via `ForceChunkUpdate()`. |
| `Camera(Vector3 position)` | Constructor. Sets initial position and computes the starting chunk coordinate. |
| `ForceChunkUpdate()` | Recalculates the current chunk and fires `OnChunkChanged`. Used at startup to seed the chunk loader. |
| `GetViewMatrix()` | Returns the `Matrix4x4` look-at matrix for use in the shader. |
| `Move(Vector3 direction)` | Translates the camera relative to its current heading (XZ grounded, Y free-fly). Checks for a chunk boundary crossing after every move. |
| `Rotate(float deltaYaw, float deltaPitch)` | Updates `_yaw` and `_pitch` from mouse delta values, clamping pitch to ±89°. Recomputes `Front`. |

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
| `MovementUpdate(double deltaTime, Camera camera)` | Polls `InputManager.IsActionPressed()` for all directional actions, assembles a direction vector, normalises it, scales by `Speed * deltaTime`, and calls `camera.Move()`. |
| `LookUpdate(Vector2 mousePosition, Camera camera)` | Computes the mouse delta from the last stored position and calls `camera.Rotate()` with the result scaled by `sensitivity`. |

Constants: `Speed = 12f`, `sensitivity = 0.1f`.

---

### 5.4 TerrainManaging

#### `TerrainGenerator` (`Game/TerrainManaging/TerrainGenerator.cs`)
**Namespace:** `Basics.Game`  
Generates chunk block data using 4D OpenSimplex noise.

| Member | Description |
|---|---|
| `setMapSize(int size)` | Sets the total number of chunks across both axes. `radius = size/2`, `mapLimit = radius * 32`. Must be called before `GenerateChunk()`. |
| `GenerateChunk(ChunkCoord coord)` | Generates a full `int[32,32,32]` block array for the given chunk and returns a ready-to-render `ChunkMesher`. Uses **torus mapping** to ensure world-edge continuity. |
| `DebugExportNoiseMap(string filename)` | Exports a greyscale PNG of the noise map across the whole world. Red pixels indicate heights below 0, blue pixels heights above 31. Saved to the working directory. |

**Torus mapping** explanation:  
To avoid a seam at the world edge, world coordinates are projected onto a 4D torus. The X and Z coordinates are each independently converted to an angle `θ = (worldCoord + mapLimit) / (2 * mapLimit) * 2π`, then expanded to a 2D circle `(sin(θ), cos(θ))`, giving a 4-component input `(x4, y4, z4, w4)` to the noise function. This ensures that coordinate 0 and coordinate `mapLimit*2-1` sample nearby noise values.

---

#### `ChunkMesher` (`Game/TerrainManaging/ChunkMesher.cs`)
**Namespace:** `Basics.Game`  
Builds and owns the OpenGL geometry (VAO/VBO/EBO) for a single 32×32×32 chunk.

| Member | Description |
|---|---|
| `ChunkPosition` (`ChunkCoord`) | World-block position of the chunk's origin corner. |
| `ChunkMesher(GL gl, ChunkCoord position, int[,,] blockData)` | Constructor. Immediately calls `InitializeGeometry()` to build the mesh. |
| `Render(ShaderManager shaderManager)` | Uploads the per-chunk model matrix, binds the VAO and EBO, then issues `DrawElements`. |
| `Dispose()` | Releases all OpenGL buffer objects (VBO, EBO, VAO). |

**Mesh generation detail (`InitializeGeometry`):**  
Iterates every block in the 3D array. For each non-air block it checks all 6 neighbours; if a neighbour is air (or the block is on the chunk boundary), the corresponding face is added. Each face consists of 4 vertices (position + UV) stored as 5 floats per vertex, and 2 triangles (6 indices). UV coordinates come from `BlockTextures.Get(blockId, faceIndex)`.

**Face index constants** (defined in `BlockTextures`): `Top=0`, `Bottom=1`, `Front=2`, `Back=3`, `Left=4`, `Right=5`.

---

#### `ChunkProvidor` (`Game/TerrainManaging/ChunkProvidor.cs`)
**Namespace:** `Basics.Game`  
Central in-memory registry for all live chunks. Acts as a cache between `ChunkRequestor` and `TerrainGenerator`.

| Member | Description |
|---|---|
| `ChunkProvidor(TerrainGenerator terrainGenerator)` | Constructor. Stores the generator reference. |
| `RequestChunk(ChunkCoord coord)` | Loads or generates the chunk at `coord` if it is not already in the cache. |
| `UnloadChunk(ChunkCoord coord)` | Disposes the chunk's GPU resources and removes it from the cache. |
| `GetLoadedChunks()` | Returns all currently cached `ChunkMesher` instances (used by `Renderer`). |
| `IsChunkLoaded(ChunkCoord coord)` | Returns `true` if the chunk is in the cache. |
| `Dispose()` | Disposes all cached chunks and clears the dictionary. |

`TryLoadFromDisk()` is a stub that always returns `false`; disk persistence is a planned feature.

---

#### `ChunkRequestor` (`Game/TerrainManaging/ChunkRequestor.cs`)
**Namespace:** `Basics.Game`  
Subscribes to `Camera.OnChunkChanged` and drives chunk loading/unloading based on the player's current chunk coordinate.

| Member | Description |
|---|---|
| `RenderDistance` (`int`, default 6) | Radius in chunks around the player that should be loaded. Minimum 1. |
| `ChunkRequestor(Camera camera, ChunkProvidor chunkProvidor)` | Constructor. Subscribes `OnPlayerChunkChanged` to `camera.OnChunkChanged`. |

**Algorithm in `OnPlayerChunkChanged(ChunkCoord playerChunk)`:**

1. Build `newActiveChunks`: iterate `(-RenderDistance … +RenderDistance)²` on the XZ plane, skip coordinates where `x²+z²  > RenderDistance²` (circular mask), and call `ChunkProvidor.RequestChunk()` for each.
2. Diff against `_activeChunks` (previous frame's set): call `ChunkProvidor.UnloadChunk()` for every chunk that was active before but is not in `newActiveChunks`.
3. Replace `_activeChunks` with `newActiveChunks`.

---

### 5.5 Graphics

#### `Renderer` (`Graphics/Renderer.cs`)
**Namespace:** `Basics.Graphics`  
Static façade that owns global OpenGL resources and drives the render loop.

| Member | Description |
|---|---|
| `gl` (static `GL`) | Silk.NET OpenGL context. |
| `terrainshader` (static `ShaderManager`) | The shader used for all terrain chunks. |
| `terrainTexture` (static `Texture`) | The terrain atlas texture. |
| `PlayerCamera` (static `Camera`) | The active camera; shared with `ChunkRequestor` and `Movement`. |
| `ChunkProvidor` (static `ChunkProvidor`) | Set from `MainClass.OnLoad()`; provides chunks for rendering. |
| `Setup()` | Creates the GL context, sets the clear colour, constructs the camera at (0, 33, 0), loads shaders and the terrain texture. |
| `Render()` | Activates the shader, binds the texture, iterates all loaded chunks, and renders each one. |
| `Clear()` | Clears the colour and depth buffers. |
| `Dispose()` | Disposes the shader, texture, chunk providor, and GL context. |
| `FramebufferResize(Vector2D<int> size)` | Updates the OpenGL viewport to match the new window dimensions. |

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
| `Use(GL gl, Camera camera)` | Enables depth testing and back-face culling, activates the shader, and uploads the view matrix, projection matrix, and `uTexture = 0`. |
| `SetModelMatrix(Matrix4x4 model)` | Uploads the per-object `uModel` uniform. |
| `BindTexture(Texture texture)` | Binds the texture to `TextureUnit.Texture0`. |
| `Dispose()` | Forwards to `Shader.Dispose()`. |

---

#### `Texture` (`Graphics/Texture.cs`)
**Namespace:** `Basics.Graphics`  
Manages a single OpenGL 2D texture.

| Member | Description |
|---|---|
| `Texture(GL gl, string path)` | Loads an image from disk using StbImageSharp, uploads it as RGBA, and applies texture parameters + mipmaps. |
| `Texture(GL gl, Span<byte> data, uint width, uint height)` | Creates a texture from raw byte data generated at runtime. |
| `Bind(TextureUnit textureSlot)` | Activates the given texture unit and binds this texture. |
| `Dispose()` | Deletes the OpenGL texture handle. |

Texture parameters: wrap mode `ClampToEdge`, minification filter `NearestMipmapLinear`, magnification filter `Nearest`, 8 mipmap levels.

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

> Note: `Transform` is currently not used by any active code path; chunks use a `Matrix4x4` directly in `ChunkMesher`.

---

### 5.6 Input

#### `Actions` (enum, `Input/InputManager.cs`)
**Namespace:** `Basics.Input`  
Defines all game actions that can be bound to keys.

`Close`, `Fullscreen`, `Borderless`, `Up`, `Down`, `Left`, `Right`, `Forward`, `Backward`

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

#### `FaceUV` (`Configurations/BlockTextureConfig.cs`)
**Namespace:** `Basics.Configurations`  
Holds the UV rectangle `(UMin, VMin, UMax, VMax)` for one face of a block in the texture atlas.

---

#### `BlockFaces` (`Configurations/BlockTextureConfig.cs`)
**Namespace:** `Basics.Configurations`  
Groups the six `FaceUV` objects (`Top`, `Bottom`, `Front`, `Back`, `Left`, `Right`) for one block type.

---

#### `BlockTextureEntry` / `BlockTextureConfigRoot`
Data-transfer objects used when deserialising `TextureConfig.json`. `BlockTextureConfigRoot` is the top-level object containing a `List<BlockTextureEntry>`.

---

#### `BlockTextures` (static class, `Configurations/BlockTextureConfig.cs`)
**Namespace:** `Basics.Configurations`  
Static loader and lookup table for block face UVs.

| Member | Description |
|---|---|
| `Top`, `Bottom`, `Front`, `Back`, `Left`, `Right` (constants `int`) | Face index constants (0–5) used throughout the meshing code. |
| `Initialize(string jsonPath)` | Deserialises `TextureConfig.json` and builds a `FaceUV[blockId, faceIndex]` lookup table. Idempotent (skips if already loaded). |
| `Get(int blockId, int faceIndex)` | Returns the `FaceUV` for the given block and face. Throws `InvalidOperationException` if `Initialize()` has not been called. |

---

### 5.8 Utilities

#### `ChunkCoord` (`Utilities/ChunkCoord.cs`)
**Namespace:** `Basics.Utilities`  
Immutable value type (`struct`) representing a chunk's position in the chunk grid (not block-world coordinates, except when used as the chunk's world-block origin in `ChunkMesher`).

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

#### `OpenSimplex2S` (`Utilities/OpenSimplex2S.cs`)
**Namespace:** *(global)*  
Open-source implementation of K.jpg's OpenSimplex 2, smooth variant. Used exclusively by `TerrainGenerator` for 4D noise evaluation.

| Method used | Description |
|---|---|
| `Noise4_Fallback(long seed, double x, double y, double z, double w)` | Evaluates 4D simplex noise at the given coordinates. Returns a value roughly in the range `[-1, 1]`. |

This file is a third-party library included directly in the project source.

---

## 6. Shaders

Both GLSL shaders are in `Graphics/Shader/` and are copied to the output directory on build.

### `shader.vert` — Vertex Shader (GLSL 3.30 Core)

**Inputs:**

| Attribute | Location | Type | Description |
|---|---|---|---|
| `aPos` | 0 | `vec3` | Local block position |
| `aTexCoord` | 1 | `vec2` | UV coordinate from texture atlas |

**Uniforms:**

| Name | Type | Description |
|---|---|---|
| `uModel` | `mat4` | Per-chunk model matrix (translation to world position) |
| `uView` | `mat4` | Camera view matrix |
| `uProjection` | `mat4` | Perspective projection matrix |

**Output:** `fragTexCoords` (`vec2`) forwarded to the fragment shader.  
**Clip-space calculation:** `gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0)`

### `shader.frag` — Fragment Shader (GLSL 3.30 Core)

**Inputs:** `fragTexCoords` (`vec2`) from the vertex shader.  
**Uniform:** `uTexture` (`sampler2D`) — the terrain atlas bound to texture unit 0.  
**Output:** `outColor = texture(uTexture, fragTexCoords)` — samples the atlas at the interpolated UV.

---

## 7. Configuration Files

### `Configurations/TextureConfig.json`

Defines UV coordinates for each block type's faces within the `texture/example.png` atlas (assumed to be a 4-column × 2-row grid where each cell is 0.25 × 0.5 in UV space).

| Block ID | Type | Description |
|---|---|---|
| 1 | Dirt/Grass | Top face uses col 1 row 1; sides use col 3 row 1 |
| 2 | Stone | All faces use col 4 row 1 |
| 3 | Snow | All faces use col 1 row 2 |

Block ID `0` (Air) has no entry; it is never rendered.

---

## 8. Dependencies

All dependencies are managed via NuGet (see `Terrain_Generator.csproj`).

| Package | Version | Purpose |
|---|---|---|
| `Silk.NET.Windowing` | 2.23.0 | Cross-platform window creation and the main run-loop |
| `Silk.NET.OpenGL` | 2.23.0 | OpenGL 3.3 bindings |
| `Silk.NET.Input` | 2.23.0 | Keyboard and mouse input |
| `StbImageSharp` | 2.30.15 | Image loading (PNG → raw RGBA bytes) |
| `System.Drawing.Common` | 10.0.3 | Used in `TerrainGenerator.DebugExportNoiseMap()` to save debug PNG |

---

## 9. Known Limitations & Planned Features

The following items are tracked in the project README:

**High priority:**
- Multi-threading for chunk generation and loading
- Improved chunk mesh generation (e.g., greedy meshing)
- Cubic / multi-height chunks
- More efficient block data storage
- Frustum culling (skip rendering chunks outside the camera's view frustum)
- Disk persistence: saving and loading chunks
- Block breaking and placing
- General debugging tools

**Low priority:**
- Ambient and directional shading
- Better terrain shapes and biomes
- Client ↔ Server architecture

**Future:**
- Trees and bushes
- Dynamic grass
- UI / main menu
