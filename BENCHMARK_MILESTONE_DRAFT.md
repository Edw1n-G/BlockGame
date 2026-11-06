# Milestone review and BenchmarkDotNet implementation draft

Reviewed 6 November 2026. Status: **draft for implementation; benchmarks have not been added or measured**.

## Decision

**The first milestone's engineering foundation is complete enough to begin CPU benchmarking. Its documentation closeout is incomplete.** Finish the small closeout in task 1, then proceed to milestone two. There is no evidence here that a broad testability refactor must precede benchmarking.

This is a narrower conclusion than “every engine function is fully tested.” Headless generation, meshing, world access, raycasting, session isolation and lifecycle behavior are exercised. GPU behavior remains a manual validation boundary. Stronger geometry checks and reproducible benchmark inputs belong at the beginning of milestone two, before trusting performance numbers.

The three stages remain distinct:

1. **Testability:** executable correctness checks and safe, isolated CPU lifecycles.
2. **Measurement:** representative BenchmarkDotNet workloads with clearly defined costs and retained results.
3. **Comparison:** a repeatable baseline/candidate workflow that people and agents can use to judge a change.

## What was reviewed and verified

Remote branch tips were checked with `git ls-remote --heads origin`:

| Branch | Reviewed revision | Finding |
|---|---|---|
| `claude/xunit3-engine-tests` | `2603d35b5dcf353e782476e952a4c43e307caa5b` | Earlier xUnit work. Its test files are retained on main, with subsequent fixes and additions. It is not a Git ancestor of main; that alone does not mean its work is missing. |
| `codex/test&refactor` | `491d6a4db6b8614274478117523d88539ad02997` | Contains the first milestone implementation and historical completion record. |
| `main` | `9a9428e8de5693a8ead5d987e4ce090330edb040` | Merges the refactor, removes both local Silk integration files, and deletes the old plan and stale-upload ticket. This is the implementation baseline. |

The working checkout was still on `codex/test&refactor`, with local deletions of `BlockGame/SilkIntegration.cs` and `BlockGame/SilkGlIntegration.cs`. Verification used a clean `git archive` of main under `out/milestone-review-9a9428e/source`; it did not switch branches or change those deletions.

Environment: Windows x64, OS version `10.0.26200`, SDK `10.0.103`, runtime `10.0.3`. Results from the archived main revision:

| Check | Result |
|---|---|
| Restore test project and referenced engine | Passed with network access; no restore warnings reported |
| Debug build of test project and engine | Passed, 68 warnings, zero errors |
| Release build of test project and engine | Passed, 68 warnings, zero errors |
| Debug direct xUnit runner, serial | 20/20 runs passed; 301 tests per run, none skipped |
| Debug direct xUnit runner, collection parallelism, four threads | 20/20 runs passed; 301 tests per run, none skipped |
| Release direct xUnit runner, collection parallelism, four threads | 301/301 passed, none skipped |
| Debug `dotnet test` using Microsoft.Testing.Platform | 301/301 passed outside the sandbox; sandbox launch failed with an access-denied runner error |
| OpenGL/UI smoke | Existing main documentation records the user's package-backed smoke check as passed. Not independently rerun in this review. |

Build warnings include nullable/unused-member and xUnit cancellation warnings. Neither build reported the old `CS0436` Silk type conflicts. Repeated passes support the baseline; they do not prove race freedom or complete geometry correctness.

Local logs are retained in `out/milestone-review-9a9428e/` (Git-ignored): `build-debug.log`, `build-release.log`, `tests-debug-<mode>-<run>.log`, `tests-release.log`, and `tests-mtp-approved.log`. These are review artifacts, not portable CI evidence.

### Evidence for the first milestone

| Requirement | Owning implementation and tests | Assessment |
|---|---|---|
| Headless CPU entry points | `EngineContext`, `EngineSession`, `TerrainGenerator.GenerateChunk`, `Lod0Mesher`/LOD1/LOD2 constructors; `TerrainGenerationTests`, `MesherTests` | Available without an OpenGL context |
| Independent sessions | `SessionIsolationTests`, `ScopedFacadeTests`, `SessionLifecycleTests`, `SchedulerMeshingTests` | Instance paths and scoped facades are exercised; process-wide facade tests deliberately remain nonparallel |
| Stop before disposing dependencies | `JobScheduler.Stop`, `EngineContext.Shutdown`, `ChunkRequestor.Stop`, `Game.Exit`; `SchedulerShutdownTests`, `GameLifecycleTests` | Reject/discard/join behavior, timeout retention, failure reporting and event cleanup are covered |
| Request ownership and stale results | `MeshRequest.Generation`, chunk-load generations, `BaseMesher.SourceChunkData`, `ChunkProvider.AcceptUpload`; `StaleUploadTests` | Old load/mesh results cannot replace a new chunk lifetime; rejection and repeated disposal return CPU buffers once in the tested paths |
| Repeatable build/test baseline | `global.json`, `verify-tests.ps1`, xUnit v3/MTP project | Working on the verified Windows x64 environment |
| Documentation matches implementation | `Documentation.md` sections 2, 3 and 11 | **Incomplete:** still describes a console test/benchmark runner and deleted `Program.cs` methods, including `TestFiftyBenchmarkIterationsLoop` |

### Limits to carry forward explicitly

- **Headless does not mean native-free.** `EngineContext` creates `NoiseCalculator` and initializes a content-backed registry even when a test only needs a mesher. Use explicit dependencies and setup outside measurement; do not require a broad dependency-injection rewrite.
- **Meshing correctness checks are currently coarse.** Most `MesherTests` observe `IsEmpty`, position, unchanged input and pooling. They do not establish complete vertex/index, winding, material or AO correctness. Add small independent output checks for the benchmark fixtures.
- **Shared FastNoise provenance remains unresolved.** The shipped `BlockGame/FastNoise.dll` hashes to `BBB9741E55EF11596FA18E163548D40D853AEDF920338298651D9CA35D5D1050`. The historical record could not identify its build/version. `FastNoiseConcurrencyTests` compares shared-node output with serial output, but a passing stress test is not a thread-safety guarantee. Keep this risk tracked; initial benchmarks below are single-threaded. Resolve provenance or select and document a mitigation before extending the performance claim to shared-node worker scaling.
- **Timeout coverage is not a universal watchdog.** The noise stress test has finite rounds but awaits all tasks without a deadline. `verify-tests.ps1` also accepts zero/negative repetitions and has no child-process deadline. Harden the automation before relying on unattended runs; a native hang should fail the process, not free a node while workers still use it.
- **Injected dependencies are disposed by the context.** Initial benchmark fixtures must own their entire dependency graph. Do not share a disposable registry/noise/provider across sessions. Broader ownership redesign remains separate work.
- **There is no BenchmarkDotNet project or comparison workflow yet.** Passing test durations are not engine benchmark results. In particular, collection-parallel test times do not measure engine worker scaling.

## Ordered implementation tasks

Each task depends on the preceding acceptance gate. Keep fixture/harness changes separate from engine optimizations so the baseline remains interpretable.

### 1. Close the first milestone's documentation gap

Update `Documentation.md` sections 2, 3 and 11 to describe xUnit v3/MTP, the existing verification script, the static-facade collection, supported Windows/native requirements, and the distinction between tests and future benchmarks. Remove references to the deleted console harness. Record the merged package Silk choice, existing user-reported smoke result, this 301-test baseline, and the native provenance limitation in durable documentation. Link the new plan from the testing documentation; do not restore obsolete unchecked checklists as current requirements.

**Acceptance:** a new contributor can follow the documented commands and discover/run the suite; the documentation makes no claim that benchmarks already exist. This is the bounded milestone-one follow-up, not another engine refactor.

### 2. Define versioned workloads and their correctness checks

Introduce a small shared fixture project, for example `BlockGame.Workloads`, referenced by tests and benchmarks, with no xUnit or BenchmarkDotNet dependency. Give each scenario a stable ID and version, explicit seed/settings, coordinates, LOD, neighbor policy and content identity. Resolve block names through the fixture registry; never assume production and fallback numeric block IDs coincide.

Start with this bounded matrix:

| Family | Required scenarios | Unit and purpose |
|---|---|---|
| LOD0 meshing | Null-storage air; allocated all-air; isolated solid with air neighbors; solid with all solid neighbors; half-filled floor; fixed checkerboard; one frozen terrain neighborhood | One CPU mesh constructed and disposed; distinguish early return, traversal, face emission, culling and AO-heavy work |
| Terrain generation | Fixed small coordinate set at LOD0, seeds 1 and 42; select representative air, solid and surface chunks; separate outside-map control | One generated chunk; includes noise, block classification and result allocations |
| Noise generation | Fixed `16 x 17 x 16` grid, step 1, explicit node tree/seed/coordinates | One `NoiseCalculator.GetNoiseValues` call, including its managed output allocation |

Specify all 26 neighbors for meshing. Keep terrain-derived mesh input frozen/versioned so a generator change cannot silently alter a mesher benchmark. Validate generated scenario classifications rather than guessing that a coordinate is a surface chunk. Defer LOD1/LOD2, raycast microbenchmarks and worker pipelines until this minimum suite is useful.

Add a small read-only internal CPU mesh inspection seam if necessary (counts and read-only vertex/index data, valid before disposal/upload). Avoid reflection, mutable buffer exposure and any new hot-path copying. Extend friend access only where required.

Correctness tests should verify independently predictable simple cases (e.g. one isolated block has six quads, 24 vertices and 36 indices), fully occluded output, index bounds, vertex stride, outward winding, expected material layers, selected AO/diagonal cases and unchanged source data. Validate complex fixtures with invariants and reviewed fingerprints; do not make a snapshot hash the sole oracle or duplicate the meshing algorithm. Native output fingerprints must record the DLL and active feature set; do not assume cross-ISA bitwise equality.

**Acceptance:** tests detect deliberately empty/invalid output for nonempty scenarios; fixture identities and semantic expectations are recorded. Tests and benchmarks consume the same inputs. A correctness-changing workload revision requires an explicit version change.

### 3. Add a dedicated BenchmarkDotNet executable

Create `BlockGame.Benchmarks`, target `net10.0`, reference the engine and workload project, and add it to `Rendering.sln`. Pin an exact BenchmarkDotNet version verified to work with the selected SDK/runtime. Use `BenchmarkSwitcher` for discovery/filtering and normal generated child-process execution. Do not benchmark through xUnit or put BenchmarkDotNet in the shipping engine dependency graph.

Copy/resolve the exact required native DLL and content in the generated benchmark process. Preflight their presence and identity; fail instead of silently using fallback content. Select Windows x64 explicitly and report the runtime actually loaded. Separate a discovery/smoke profile from a measurement profile; smoke numbers are never a baseline.

**Acceptance:** a clean checkout can list the expected cases and run one case from every family in Release without a debugger or GL context. Missing dependencies and invalid filters produce failures, not successful empty reports. Release/no-debugger execution and result consumption follow [BenchmarkDotNet's good practices](https://benchmarkdotnet.org/articles/guides/good-practices.html).

### 4. Implement explicit measurement boundaries

- **Meshing:** time construction of `Lod0Mesher` (which performs the CPU build), consumption of a cheap output summary, and disposal returning the lists. Name the operation accordingly, e.g. `BuildAndReturnBuffers`. Prepare registry, chunk data, neighbors and a representative warmed list pool outside timing. Every operation returns its buffers; queues/pools must not grow across invocations. Cold-pool/startup costs are separate future scenarios, not mixed into the steady-state series.
- **Terrain:** initialize the native node/registry outside timing. Time `GenerateChunk` and return/consume its result. Include the current density-field and block-array allocations. Exclude fixture checks, content loading and session creation.
- **Noise:** initialize the node outside timing, call `GetNoiseValues`, and return/consume the array. This isolates noise-plus-array cost from terrain classification; do not label it raw native computation alone.

Use explicit instance dependencies, disable the scheduler for these families, and never call GPU upload/rendering. Put expensive checksums/assertions outside timing. Dispose fixture-owned resources in cleanup. Repeated invocations must do equivalent work with stable retained state.

Use global setup/cleanup for reusable fixtures. Avoid per-iteration reset attributes for these small operations: they constrain invocation/unrolling behavior and can distort a microbenchmark. See [BenchmarkDotNet setup and cleanup](https://benchmarkdotnet.org/articles/features/setup-and-cleanup.html). If later benchmarking mutations requires reset, document the reset cost and choose a deliberate batching strategy.

**Acceptance:** repeated dry executions preserve input fingerprints, expected output semantics and bounded pool/queue counts. The documented operation matches the code actually timed. No broad production API is added solely to wait for scheduler idleness.

### 5. Export distinct results with reproduction metadata

Enable `MemoryDiagnoser` for managed allocation/GC observations; do not call its output native memory or VRAM usage. Profiling those requires separate instrumentation. See [BenchmarkDotNet diagnosers](https://benchmarkdotnet.org/articles/configs/diagnosers.html).

Retain Markdown for review, full JSON and raw measurements for comparison, and logs. BenchmarkDotNet provides these [exporters](https://benchmarkdotnet.org/articles/configs/exporters.html). Put each run in its own artifact directory; never overwrite a previous baseline.

Write a versioned manifest containing:

- Engine commit, harness/workload revision and dirty-tree status; run ID and UTC timestamp.
- SDK/runtime, BenchmarkDotNet version, configuration, architecture, OS, CPU identity/logical cores, and effective runtime/GC/JIT and benchmark job settings.
- DLL hash and active FastNoise feature set; node-tree/content hashes; block-name/ID/layer mapping; complete scenario parameters and pool policy.
- Command/filter, machine identifier, power mode and known competing load; test/preflight status and paths to raw artifacts.

For each scenario report mean, error/confidence information, standard deviation, managed bytes/op, relevant GC columns, and the exact operation unit. Do not collapse different scenarios into a single “engine score.” Define operations-per-invocation accurately if batching is introduced. Missing allocation data is unavailable, not zero.

**Acceptance:** another worker can identify exactly what produced each row and rerun it. Timings, correctness status and allocation metrics remain distinct. Raw measurements survive report generation.

### 6. Deliver one repeatable verification-and-measurement command

Add a wrapper such as `run-benchmarks.ps1` with explicit `Smoke` and `Measure` modes, case filter and output directory. Validate positive test repetitions/worker limits in `verify-tests.ps1`. Add bounded child-process execution and failure propagation; terminate timed-out test/benchmark process trees and retain diagnostics. Do not solve a native hang by disposing resources still in use.

Run restore/build and correctness preflight before measuring; stop on failed, skipped-required or zero-discovery checks. Check that BenchmarkDotNet executed every selected case successfully, not only the launcher's exit code. Capture test counts/results in a machine-readable sidecar. Prevent concurrent benchmark jobs on the measurement machine; tests/builds must finish before timing starts.

**Acceptance:** one documented command creates a complete artifact bundle from a clean checkout. Exercise wrong filter, missing native/content dependency, failed correctness preflight and timeout paths. A smoke-only run is visibly marked unsuitable for performance comparisons.

### 7. Establish and inspect the initial baseline — milestone two exit

Run the full minimum matrix in the measurement profile on one quiet Windows x64 machine, then repeat in a separate process/run to assess stability. Keep the same power settings and workload/harness/native identities. Use normal BenchmarkDotNet warmup/measurement behavior rather than choosing an artificially tiny iteration count. Review noisy cases and warnings; increase sampling or revise a poorly defined workload before making claims.

Record per-case timing distributions and managed allocations, investigate unexplained differences, and retain both bundles plus a short interpretation. Define acceptable measurement noise from these observations; do not invent a universal percentage threshold now.

**Milestone two is complete when:** the suite is correctness-validated, Release child-process execution is reproducible, every required scenario has a named metric and complete artifacts, repeated measurements have been reviewed, and documentation explains the results' scope. These are CPU measurements, not FPS, frame-time, GPU upload, VRAM, or total gameplay performance claims.

## Milestone three handoff: comparable evidence for people and agents

Design the manifest in task 5 now, but implement the comparison/CI policy after the measurement suite is stable. Continue in this order:

1. **Run baseline and candidate with the same harness.** Use separate clean checkouts and output folders; run sequentially on the same measurement machine. Record both engine revisions and the common workload/harness revision. The pre-harness main revision cannot magically run the new project: either establish the first baseline after the harness lands or apply an identical reviewed harness overlay to both revisions and record it. Never compare a changed workload under the old scenario version.
2. **Validate comparability before calculating deltas.** Reject missing/failed cases, failed correctness checks, incompatible scenario versions, differing units/pool policies or unaccounted native/runtime/job/environment changes. If a dependency or runtime change is the subject of the experiment, label it explicitly as a different experiment; do not attribute the entire effect to engine code. Cross-machine absolute timings are informational only.
3. **Produce a per-case comparison.** Join on stable scenario/configuration identities. Report baseline/candidate values, candidate-to-baseline ratio, percentage change, uncertainty and managed allocation differences. Handle zero baselines/missing values explicitly. Return `improved`, `regressed`, `inconclusive` or `not comparable` with reasons. Use calibrated practical thresholds plus repeat evidence; a tiny mean difference or overlapping intervals alone is not a reliable decision rule. BenchmarkDotNet's within-run baseline annotation is not a Git revision comparison system.
4. **Validate the comparison tool.** Check identical inputs, a controlled slowdown/allocation increase, noisy results, missing cases and mismatched manifests. These checks establish that a broken or incomparable run cannot be reported as an improvement.
5. **Add automation and agent guidance.** General Windows CI runs correctness and benchmark smoke checks. Run decision-grade measurements on a designated stable runner with a concurrency lock and retained artifacts. Require the final combined commit to pass verification and any requested performance comparison, even if individual worker branches passed. Document commands, supported environments and interpretation in `AGENTS.md`/testing documentation. Start with advisory performance reporting; enable failure thresholds only after repeatability has been demonstrated.

**Milestone three exit:** a worker or agent can supply two revisions, obtain correctness status and a defensible per-case comparison with raw evidence, and receive an explicit inconclusive/not-comparable result when the experiment cannot support a conclusion.

GPU/frame-time instrumentation, native memory profiling, multiworker scaling, broad fixture decoupling and general ownership/static-facade redesign remain separately scoped follow-ups. They should not delay a useful, validated single-threaded CPU measurement suite.
