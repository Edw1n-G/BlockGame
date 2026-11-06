# First testing milestone — implementation record

Status: **in progress**; headless build and test work is implemented, but the manual UI smoke check and Silk runtime decision remain open. The objective is a reproducible Windows x64 baseline and safe, observable shutdown of all background work. BenchmarkDotNet and broader context, fixture and coverage changes remain follow-on work.

## Implementation status

- SDK 10.0.103 is pinned with `latestPatch`; the unused ImageSharp export and dependency are removed. Debug and Release builds succeeded. On this machine `verify-tests.ps1` completed 20 serial and 20 collection-parallel passes with four threads, all 296 tests passing each time. MTP also passed all 296 tests.
- The subsequent lifecycle test changes pass in a 297-test collection-parallel run; the 20-pass result above predates that extra test.
- Scheduler shutdown, requestor termination and player static-event cleanup are implemented. Timeout defaults to ten seconds and leaves session resources alive. Controlled tests cover timeout, discard, failure reporting and request marker ownership.
- Shared-node FastNoise test passes on the bundled DLL. The binary has no version metadata; SHA-256 is `BBB9741E55EF11596FA18E163548D40D853AEDF920338298651D9CA35D5D1050`. The observed active feature-set value was `16382`; it is hardware dependent. Upstream safety claims cannot be tied to this unversioned build.
- Excluding both local Silk files experimentally produced a successful build against the package. The files remain in use because the UI/runtime behavior has not been compared. Computer-use launch approval timed out, so the manual UI smoke checklist remains outstanding.
- **Follow-up risk:** [Stale mesh upload ticket](STALE_UPLOAD_RACE.md) tracks the unload/reload race and its required regression test.

## Baseline and release blockers

On 6 November 2026, after fast-forwarding to `origin/main`:

- A forced restore with network access completed without NuGet advisory warnings for the restored projects. The sandboxed attempt could not retrieve vulnerability data (`NU1900`). This is a point-in-time restore check, not a complete security audit.
- Debug build succeeded, with warnings. The xUnit v3 MTP run passed **287/287**, with none skipped. Direct runner invocations also passed **287/287** once each in serial and collection-parallel modes. These single runs are a baseline, not proof of race freedom.
- **Release build fails** because ImageSharp 4.1.3 requires a license. Its only use is the unused, nonfunctional debug noise-map export. First option: remove that method and package; a future debug tool could use StbImageWriteSharp. Obtaining a license is a secondary option. Do not roll back to vulnerable 3.x releases.
- Build warnings report `CS0436` type conflicts: the local `SilkIntegration` and `SilkGlIntegration` share names with Egui package types. `GameEguiIntegration` currently resolves to the local implementation and accesses its public `_rawInput`; removing the files without a replacement for that behavior is not a safe cleanup.
- Manual UI smoke testing has **not** been performed. Egui 0.10.5 and the input integration changed in the update; passing headless tests does not validate them.

## Scope and completion

1. Capture the repeatable build/test baseline, pin the SDK, resolve the Release prerequisite, and perform the UI smoke check.
2. Implement and test the scheduler shutdown contract and meshing-marker cleanup.
3. Investigate the shipped FastNoise binary and add one focused concurrent-generation stress test.
4. Correct `AGENTS.md` and `Documentation.md`; decide and document the local Silk integration's ownership.

The milestone is complete when the agreed Windows x64 environment builds Debug and Release, the UI smoke checklist and all existing plus new tests pass, serial and collection-parallel runs have been repeated without unexplained failures, scheduler lifecycle tests prove the specified shutdown behavior, and documentation matches the chosen implementation. Track any remaining failure or deliberate limitation explicitly; do not use repeated green runs as proof that no races exist.

## 1. Baseline and UI smoke check

- [x] Pin an agreed installed .NET 10 SDK and roll-forward policy in `global.json`, preserving MTP selection. Prefer a small checked-in verification script documenting restore, build, test and nonzero discovery. Confirm test filters and the chosen configuration with the actual MTP runner.
- [x] Remove the unused ImageSharp debug export and dependency, recheck restore/audit, and build Debug and Release.
- [x] Run the full suite and confirm discovery; run **20 serial and 20 collection-parallel repetitions** with a documented worker limit. Retain the nonparallel static-façade collection for now.
- [ ] On a Windows machine with a suitable OpenGL context, manually check: launch and menu rendering; buttons and settings sliders; start game and terrain display; mouse lock/unlock and camera movement; in-game UI interaction with mouse unlocked; resize and fullscreen/borderless controls; exit/close without errors. Record results and environment. This is a smoke check, not exhaustive graphics validation.

**Exit:** documented commands work on the agreed environment, both configurations build, the test count is recorded and the UI checklist passes or has an explicitly tracked failure.

## 2. Scheduler and mesh markers — P0

The first scheduler and marker changes are implemented. Remaining work here includes the unload/upload lifetime test, pooled buffer return proof, and any further independent-session coverage; see the separate stale upload ticket.

### Risks identified in the pre-implementation review

`JobScheduler` starts background threads without retaining or joining them. `Stop()` sets a flag and releases a fixed 100 signals. `EngineContext.Dispose()` immediately disposes provider, native noise and registry afterwards, potentially while a worker still uses them. Worker exceptions are only written to stderr. `MeshgenerateJob` can leave `QueuedForMeshing` set when chunk data is missing or generation throws; a stale marker may prevent later requests. `ChunkRequestor` also runs an untracked task that uses the provider, and `PlayerCharacter` subscribes to a static UI event without unsubscribing. Both must be handled at game exit, before context disposal.

### Recommended contract

1. Atomically transition to stopping and **reject new jobs**. Define the result callers receive.
2. **Discard queued jobs**, including any associated request bookkeeping, and allow already running jobs to finish. Do not start additional queued work after shutdown begins. Clarify precisely which jobs count as in flight when dequeue races with stop.
3. Wake and join the actual worker set, using one generous, configurable or documented **overall** deadline rather than a timeout per worker. Stop is idempotent and concurrent callers observe a consistent result.
4. **Report timeout or worker failure explicitly.** On timeout, log it, return false and skip disposal of resources a worker might still access; process exit reclaims them. Never dispose native or GPU resources underneath live work. No retry or escalation framework is required.
5. Audit every current job for waits on main-thread work or locks held by the stopping thread **before joining on the main thread**. No worker may require main-thread GPU upload to complete. A timeout bounds the wait but does not make an unsafe dependency or cleanup safe.

The policy favors quick exit over completing queued world generation. It needs a precise ownership rule for discarded job data and mesh markers; if gameplay needs a drain operation later, give that a separate explicit contract. Define start/restart and enqueue-before-start behavior instead of accidentally inheriting today's behavior.

### Proposed changes and tests

- [x] Stop and await `ChunkRequestor` before stopping the scheduler or disposing the provider; unsubscribe player and requestor events on exit. Retain worker handles, make state transitions and enqueue/stop races safe, report exceptions, and make teardown conditional on successful termination. All GPU operations remain on the main thread.
- [ ] Clear meshing markers on success, missing chunks, exceptions and discarded queued work. Use unique generation numbers and conditional key/value removal so an older request cannot clear a newer marker; force the unload/requeue race in a test. Dispose abandoned CPU mesh resources exactly once.
- [ ] Use controlled `IJob` instances and synchronization primitives in tests to hold and release an in-flight job. Assert rejected enqueues, queued discard, ordering of shutdown versus resource disposal, workers exiting, timeout reporting, failures, idempotence and independent sessions. Always release held jobs in `finally`; use bounded waits to diagnose hangs.
- [ ] For higher-level tests prefer an injected scheduling interface with a synchronous or controllable test scheduler, if a small seam is justified by actual consumers. At the worker boundary use test-owned job completion signals. Do **not** add a production idle-wait API solely for tests; real production observability should have a production use case. Retain real-thread integration tests.
- [ ] Test unload while a mesh is in flight and pooled CPU buffer return exactly once. These are high-risk boundaries; if a full stale-upload solution is larger than this milestone, capture the failing scenario and agree a bounded follow-up instead of silently claiming it fixed.

**Exit:** controlled tests show that no worker outlives resources it accesses, the agreed timeout/error behavior is observable, and marker cleanup permits valid later requests without corrupting newer ones.

## 3. Early FastNoise concurrency check

The session shares one `NoiseCalculator` and native node across generation workers. The current [upstream FAQ](https://github.com/Auburn/FastNoise2/wiki/FAQ) says generation on a shared node tree is thread-safe when node parameters are not modified concurrently. That is useful evidence for current upstream, **not proof for the vendored DLL**: its version/build provenance is not recorded in this repository. Upstream release history also contains an older thread-safety fix. Disposal racing generation remains unsafe regardless of concurrent generation support.

- [x] Identify the shipped binary's version, source/build and architecture if possible, and compare the relevant upstream guarantee and fixes. If provenance remains unknown, record that limit.
- [x] Add one bounded Windows x64 stress test: construct a shared `FastNoise` node from the world type, compute serial reference results for fixed seeds and varied coordinates, synchronize workers to generate concurrently, compare full outputs over repeated rounds, then join before disposal. Report `GetActiveFeatureSet()` and enough diagnostic information to reproduce a mismatch. A passing stress test cannot prove thread safety.
- [ ] If the binary's guarantee cannot be established or the test fails, agree a mitigation (e.g. separate node per worker or synchronization) before claiming safe shared use; measure any performance effect later. Do not run disposal concurrently in this generation stress test; cover lifetime ordering in scheduler tests.

**Exit:** provenance and limits are documented, the targeted test passes on the supported binary, and any remaining uncertainty or mitigation is explicitly resolved or tracked.

## 4. Documentation and Silk ownership

- [x] Correct `AGENTS.md` to match actual code: chunks currently use **16³**, the vertex is packed into **16 bytes** (3 floats, `ushort` texture layer, byte AO level, padding), and the scheduler uses threads, queues and monitor signaling. Verify API names and threading/GPU rules.
- [x] Update `Documentation.md` for the current package versions and actual `GameEguiIntegration`/vendored integration behavior. Document only shutdown and SDK behavior that has actually been implemented and verified.
- [ ] Timebox the Silk comparison: temporarily exclude local files and build to identify breakage, then compare relevant input and rendering behavior. Restore the files after the experiment. If kept, document the reason and type conflicts; if removed, replace required behavior and re-run the UI smoke checklist.

**Exit:** both documents describe the resulting code, and the Silk choice and its trade-offs are recorded.

## Follow-on, to re-scope after the scheduler work

- Decide context and injected-resource ownership based on the final scheduler contract; review mutable static fallbacks and session isolation.
- Revisit smaller headless fixtures and native/content independence. Prioritize negative coordinate boundaries and any unresolved stale upload/unload race; existing tests already cover several pool and coordinate cases. Avoid a broad geometry coverage target or tests that merely duplicate mesher algorithms.
- Introduce BenchmarkDotNet after lifecycle stability, starting with representative CPU meshing, generation and allocations. Later add Windows x64 CI, isolated worker checkouts and full verification of combined changes. Tests reduce regression risk; they do not guarantee nonbreaking changes.

## Decisions for review

1. Implementer: select a generous timeout; use the approved discard/finish and timeout policy above.
2. Implementer: pin SDK and remove the unused ImageSharp dependency; licensing is a fallback only if removal proves unsuitable.
3. Implementer: decide whether to retain local Silk after the timeboxed comparison; record the decision.
4. Implementer: decide whether stale uploads require a bounded fix now or a documented follow-up based on investigation.

## Appendix: machine-specific baseline commands

The accessible SDK here is `C:\Users\Edwin\.dotnet\dotnet.exe` 10.0.103. `C:\Program Files\dotnet\dotnet.exe` reported a runtime but no SDK. Commands below were run from the repository root; select the intended SDK on other machines.

```powershell
$dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
& $dotnet restore BlockGame.Tests/BlockGame.Tests.csproj --disable-parallel
& $dotnet build BlockGame.Tests/BlockGame.Tests.csproj --no-restore -m:1
& $dotnet test --project BlockGame.Tests/BlockGame.Tests.csproj --no-build
& $dotnet BlockGame.Tests/bin/Debug/net10.0/BlockGame.Tests.dll -parallelMode none
& $dotnet BlockGame.Tests/bin/Debug/net10.0/BlockGame.Tests.dll -parallelMode collections
& $dotnet build BlockGame/BlockGame.csproj --no-restore -c Release -m:1
```

MTP's named pipe was blocked inside the sandbox, so the MTP run used an approved outside-sandbox execution; the direct runner worked in the sandbox. SDK selection and sandbox details are environment notes, not engine requirements.
