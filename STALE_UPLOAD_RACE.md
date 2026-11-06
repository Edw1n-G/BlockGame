# Ticket: reject stale meshes after unload and reload

**Priority:** P0 correctness. **Status:** fixed in the upload acceptance path; headless regression tests pass.

`MeshgenerateJob` removes its generation marker before putting a mesh in `UploadQueue`. If `UnloadChunk` runs after that marker removal, then the same coordinates are requested again, `Renderer.Render` sees new `Chunkdata` at those coordinates and accepts the old mesh. An unload concurrent with publication has the same problem. The existing marker ownership test only proves that an old job cannot remove a newer marker.

**Implemented change:** Each mesher retains the exact `ChunkData` instance used to build it. The renderer checks the lifetime before GPU upload, then `ChunkProvider.AcceptUpload` checks again and installs the mesh under the same lock used by `UnloadChunk`. Rejected meshes are disposed on the upload thread. If unload happens during GPU upload, that upload is wasted, but the stale mesh cannot become the installed mesh. A mesh request generation alone is insufficient because it can be cleared before upload.

**Regression test:** `StaleUploadTests` exercises publication after reload and publication racing with unload through the same headless acceptance path used by the renderer. It asserts that the old mesh is absent from the new lifetime and that pooled CPU buffers are returned once.

**Verification:** Debug test build and a 299-test collection-parallel run passed. A manual OpenGL smoke check of this change remains useful because the headless tests do not execute `UploadToGpu`.
