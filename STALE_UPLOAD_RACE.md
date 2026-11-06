# Ticket: reject stale meshes after unload and reload

**Priority:** P0 correctness. **Status:** fixed in the upload acceptance path; headless regression tests pass.

`MeshgenerateJob` removes its generation marker before putting a mesh in `UploadQueue`. If `UnloadChunk` runs after that marker removal, then the same coordinates are requested again, `Renderer.Render` sees new `Chunkdata` at those coordinates and accepts the old mesh. An unload concurrent with publication has the same problem. The existing marker ownership test only proves that an old job cannot remove a newer marker.

**Implemented change:** Each mesher retains the exact `ChunkData` instance used to build it. The renderer checks the lifetime before GPU upload, then `ChunkProvider.AcceptUpload` checks again and installs the mesh under the same lock used by `UnloadChunk`. Rejected meshes are disposed on the upload thread. If unload happens during GPU upload, that upload is wasted, but the stale mesh cannot become the installed mesh. A mesh request generation alone is insufficient because it can be cleared before upload.

**Regression test:** `StaleUploadTests` fixes both orderings explicitly: unload between the renderer's early check and final acceptance, and acceptance before unload. It also races the two operations as an extra invariant check. The tests assert that the old mesh is absent from the new lifetime and that pooled CPU buffers are returned once.

**Related lifetime fix:** Generation requests now carry their own generation number. Unload invalidates it, preventing an old worker from publishing orphan chunk data. The future disk-load path also publishes through this gate and enqueues meshes for the renderer rather than inserting directly into `LoadedChunks`.

**Verification:** Debug and Release builds and all 301 tests in serial and collection-parallel runs passed. The user subsequently reported that the manual OpenGL smoke checklist passed without problems. The headless tests themselves do not execute `UploadToGpu`.
