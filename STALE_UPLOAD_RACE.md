# Ticket: reject stale meshes after unload and reload

**Priority:** P0 correctness follow-up. **Status:** open.

`MeshgenerateJob` removes its generation marker before putting a mesh in `UploadQueue`. If `UnloadChunk` runs after that marker removal, then the same coordinates are requested again, `Renderer.Render` sees new `Chunkdata` at those coordinates and accepts the old mesh. An unload concurrent with publication has the same problem. The existing marker ownership test only proves that an old job cannot remove a newer marker.

**Required change:** Give each chunk data lifetime a stable identity. Carry it with the CPU mesh into the upload queue. On the main thread, accept an upload only if its identity still matches the provider's current chunk data. Dispose rejected meshes exactly once. Synchronize unload/reload and publication so validation and replacement cannot cross a lifetime transition; preserve main-thread-only GPU calls. A mesh request generation alone is insufficient because it can be cleared before upload.

**Regression test:** Hold a mesh after CPU generation and before upload; unload its chunk; create new data at the same coordinates; release the old mesh; process the upload. Assert that the old mesh is disposed, no stale GPU mesh is installed, and a mesh from the new lifetime can be installed. Repeat with unload overlapping publication and check pooled CPU buffers are returned exactly once. The test needs a controllable upload consumer or a headless acceptance seam, not an OpenGL context.

**Done when:** both race orderings pass deterministically and the main-thread renderer uses the same checked acceptance path as the test.
