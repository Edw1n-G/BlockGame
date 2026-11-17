# CPU benchmarking

## World block access conversion experiment (2026-11-17)

Select a component not previously optimized in the history: shared world-to-local coordinate conversion used by `ChunkBlockAccess.GetBlockAt` and block edits. This is separate from the prior LOD0 mesher's private neighbor lookup. `ChunkData.WorldToLocal` used two remainder operations per axis; for the fixed 16-block chunk size, the low four bits give exactly the same nonnegative remainder, including negative integers.

Steps:

1. Review history and callers. Existing families covered meshing, terrain/noise and selection but not world block reads. Add `block-access/*` with four fixed cases: positive, negative, mixed coordinates and missing chunks. Each operation performs 256 real provider lookups and returns a checksum. Inputs cycle across a 64-block span, include chunk boundaries and all local offsets, and are explicitly serialized in the scenario contract. Loaded blocks contain local linear index + 1, so distinct local positions have distinct IDs. No native noise or graphics setup is required.
2. Validate every returned ID against an independent floor-division/subtraction oracle before timing and again during cleanup; fingerprint the complete returned sequence, not just the checksum. Run discovery, four-case Smoke, and the full suite (431 passed, zero failed/skipped). Rider reports no workload-file errors. Smoke is validation only.
3. Commit the harness and its four tests as baseline `877135d575dffbdab3790c103337675324a9db37`, with engine code unchanged. Measure all four cases from the clean tree and preserve the complete directory before changing the engine.
4. Replace only `ChunkData.WorldToLocal` with three masks. Keep world-to-chunk conversion, dictionary access and missing-chunk handling unchanged. Add an independent arithmetic test covering all local offsets around positive/negative boundaries and both integer extremes. Full candidate suite: 432 passed, zero failed/skipped; Rider reports no errors in `ChunkData.cs`.
5. Commit candidate `8b8f60f992ea54701b6cd68ccbd0f38631da238c`, verify a clean tree, and measure the same four workloads. Compare saved runs and validate comparator controls (4/4). All scenario IDs, versions, units, input fingerprints and output fingerprints match. The benchmark tree is identical in both measured revisions.
6. Retain the change: the three loaded-chunk cases improve by 19.09–22.73% in this pair, while missing-chunk reads are inconclusive. All rows remain at zero allocated bytes per batch. See [benchmark-history.md](benchmark-history.md) for SHAs and results.

Commands (PowerShell, repository root; logs redirected into `artifacts/`):

```powershell
& 'C:/Users/Edwin/.dotnet/dotnet.exe' test
& 'C:/Users/Edwin/.dotnet/dotnet.exe' run -c Release --project BlockGame.Benchmarks -- --list --case 'block-access/*'
& 'C:/Users/Edwin/.dotnet/dotnet.exe' run -c Release --project BlockGame.Benchmarks -- --mode Smoke --case 'block-access/*' --output artifacts/block-access-smoke-20261117
# Commit the validated harness before baseline measurement.
& 'C:/Users/Edwin/.dotnet/dotnet.exe' run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'block-access/*' --output artifacts/block-access-baseline-20261117
# Apply the engine/test change, run full tests, then commit the candidate.
& 'C:/Users/Edwin/.dotnet/dotnet.exe' test
& 'C:/Users/Edwin/.dotnet/dotnet.exe' run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'block-access/*' --output artifacts/block-access-candidate-20261117
& 'C:/Users/Edwin/.dotnet/dotnet.exe' run --project BlockGame.Benchmarks -- compare artifacts/block-access-baseline-20261117 artifacts/block-access-candidate-20261117 artifacts/block-access-comparison-20261117.json
& 'C:/Users/Edwin/.dotnet/dotnet.exe' run --project BlockGame.Benchmarks -- validate-controls
```

Troubles and limitations:

- No test, build or benchmark execution failed. Reuse the established out-of-sandbox .NET and Git setup; use the explicit SDK path without changing PATH. Existing compiler/analyzer warnings and Git line-ending warnings remain.
- Initial exploratory reads guessed `Physics/Physics.cs` and a separate `BlockResult.cs`; those paths do not exist. Reading the actual `PhysicsSystem` sources resolved the inspection. No measurement depended on those failed reads.
- A new benchmark family was necessary. Commit it before baseline collection so both measured revisions use exactly the same workloads and correctness expectations; do not compare a new workload against old timing rows.
- Missing-chunk reads bypass the optimized conversion, yet their mean rose 5.24% (49.64 ns per 256 reads). The advisory result is inconclusive after practical threshold and uncertainty allowance. Do not infer a direct optimization effect from this control or describe it as unchanged timing.
- Background load was reported minimal earlier in this session. Runs were sequential, without concurrent builds/tests, and preserved the power plan. Baseline and candidate took about 73 and 80 seconds respectively including build. Raw reports retain outlier-removal hints.
- This single pair measures warmed provider reads over a small fixed coordinate set, not cold dictionary access, concurrent writes, complete raycasts, block edits or frame timing. Broader callers benefit from the shared helper mechanically, but no end-to-end speedup is claimed for them. The helper still relies on the engine's fixed chunk size of 16.

Retain ignored `artifacts/block-access-{baseline,candidate,smoke}-20261117/` directories with raw reports, manifests, child checks and normalized indexes. Test logs are `block-access-{baseline,candidate}-tests-20261117.log`; launcher logs match directory names, discovery is `block-access-discovery-20261117.log`, and comparison is `block-access-comparison-20261117.{json,md,log}`. No run output is committed.

## LOD0 face vertex reservation experiment (2026-11-17)

Reserve the entire 64-byte quad once per visible face instead of reserving each of its four vertices separately. Write four fixed slices through a static helper and increment the vertex count once. Explicit little-endian writes, zero padding, face order, AO diagonals, pooled ownership and GPU behavior remain unchanged.

Steps:

1. Inspect the workflow, mesher and existing tests. Start from clean baseline `3f42e9a9e2765132988b28c69faa062ca72d3d4f`. Run the full suite: 425 passed, zero failed/skipped.
2. Measure all seven existing `mesh/*` cases with the normal Measure job. Preserve the complete baseline directory before editing source.
3. Implement face reservation in `BlockGame.Core/Terrain/Meshing/Lod0Mesher.cs`. Add two cases in the existing `WorkloadTests`: dirty vertex/index lists with deliberately tiny capacities must produce exactly the same bytes and indices after reuse and growth. Existing tests independently cover winding, materials, AO, padding and geometry counts.
4. Rider file analysis returned no errors. Run the full suite: 427 passed, zero failed/skipped. Commit candidate `0e1df0079a4f8bc01d7561ab3f79c5d16d6f7752`; confirm a clean working tree.
5. Measure the identical seven cases, compare saved directories and run comparator controls (4/4 passed). Explicitly verify matching scenario IDs, versions, units, input fingerprints, successful correctness checks and output fingerprints for all seven cases. The benchmark harness tree is unchanged.
6. Retain the candidate: three geometry cases improve beyond the practical/uncertainty threshold; four rows are inconclusive and none are classified as regressed. Allocations remain 408 B/op. Full tables and SHAs are in [benchmark-history.md](benchmark-history.md).

Commands (PowerShell, repository root; log redirection omitted):

```powershell
& 'C:/Users/Edwin/.dotnet/dotnet.exe' test
& 'C:/Users/Edwin/.dotnet/dotnet.exe' run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'mesh/*' --output artifacts/face-batch-baseline-20261117
# Apply engine/test changes, run the entire suite, then commit:
& 'C:/Users/Edwin/.dotnet/dotnet.exe' test
git add BlockGame.Core/Terrain/Meshing/Lod0Mesher.cs BlockGame.Tests/WorkloadTests.cs
git commit -m "Reserve LOD0 vertex storage once per face"
& 'C:/Users/Edwin/.dotnet/dotnet.exe' run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'mesh/*' --output artifacts/face-batch-candidate-20261117
& 'C:/Users/Edwin/.dotnet/dotnet.exe' run --project BlockGame.Benchmarks -- compare artifacts/face-batch-baseline-20261117 artifacts/face-batch-candidate-20261117 artifacts/face-batch-comparison-20261117.json
& 'C:/Users/Edwin/.dotnet/dotnet.exe' run --project BlockGame.Benchmarks -- validate-controls
```

Troubles and limits:

- Sandboxed `dotnet test` failed at MSBuild project-property discovery with exit code 1. The same command succeeded outside the sandbox. Subsequent CLI checks and measurements used that working environment, the explicit SDK executable and no PATH edits.
- Sandboxed Git staging/commit could not create `.git/index.lock` (permission denied). Staging the two explicit files and committing succeeded with elevated sandbox access. No push was performed.
- Read-only inspection initially looked for history at the repository root instead of `docs/`, then queried a nonexistent `scenarios` property instead of the run index's `cases`. Both were corrected without affecting measurements. A documentation-writing tool call also failed to parse because of nested backticks; the edit was reapplied with a patch.
- Existing compiler nullable and xUnit analyzer warnings remain; Rider reporting no errors does not imply a warning-free build.
- The user reported minimal background load. Runs were sequential with no concurrent tests/builds and no power-plan change requested. Each seven-case run took about 2m32s including build. Reports contain outlier-removal hints.
- This is one baseline/candidate pair on one machine with warmed pools. Frozen terrain increased 1.10% but remains inconclusive. Results do not establish a general terrain or FPS improvement, cold-pool allocation behavior, or cross-machine performance. The near-zero null-air control cannot support a percentage claim.

Evidence stays uncommitted under ignored `artifacts/`: complete `face-batch-baseline-20261117/` and `face-batch-candidate-20261117/` directories (BenchmarkDotNet reports/raw measurements, manifests, child correctness evidence and run indexes); matching launcher `.log` files; `face-batch-baseline-tests-20261117.log`, `face-batch-candidate-tests-20261117.log`; and `face-batch-comparison-20261117.json` / `.md`. The successful baseline retry replaced the short failed-test log; its error is recorded above.

## Terrain density buffer pooling experiment (2026-11-11)

- Inspect `TerrainGenerator.GenerateChunk`: each in-map chunk creates a temporary 16 x 17 x 16 float density array, used only until block classification completes. Reuse this scratch buffer with the built-in `ArrayPool<float>.Shared`; add an internal span-filling method to the existing `NoiseCalculator`, preserving its array-returning API. No new classes or scripts.
- Start from clean revision `9cd3797972f2a3120e035ed8721c947f31920ec2`. Run the complete suite (396 passed, zero failed/skipped), then the normal Measure job with `--case 'terrain/*' --case 'noise/*' --output artifacts/terrain-pool-baseline-20261111`. All ten cases completed before any engine edits. Keep the full output directory.
- Rent only for in-map chunks, pass an exactly sized span to native generation, and return the buffer in `finally`, including exception paths. Do not clear on return because generation overwrites every value before reading it. The final chunk block array remains independently owned. Classification, native inputs, and GPU behavior are unchanged.
- Add tests to existing classes: NaN-prefilled destinations and guard values for two seeds and several step sizes (including step normalization), too-short buffer rejection before native writes, retained chunk data after buffer reuse, and parallel generation with distinct coordinates/LODs. Full candidate suite: 402 passed, zero failed/skipped. The parallel test is regression coverage, not proof of general native-library thread safety.
- Commit the tested candidate, measure the identical ten cases into `artifacts/terrain-pool-candidate-20261111`, and run the existing comparator against the saved baseline. The noise-only cases still use the owning array API and act as controls. Record SHAs and all results in `benchmark-history.md`.
- Constraints/tradeoffs: the pool keeps reusable arrays in memory; fewer allocated bytes per operation does not imply an equal reduction in retained memory. Cold pool misses may allocate, and buffers can be larger than the requested 4,352 floats. The benchmark measures the warmed steady state. Default desktop background load and the unchanged power plan remain possible noise sources.
- Use the explicit SDK path `C:/Users/Edwin/.dotnet/dotnet.exe` outside the sandbox, without the unsuitable `-m:1` test argument identified in the previous experiment. No new execution failures occurred during baseline or candidate tests; existing compiler/analyzer warnings remain. Test logs: `artifacts/terrain-pool-baseline-tests-20261111.log` and `artifacts/terrain-pool-candidate-tests-20261111.log`; launcher logs use the run-directory names plus `.log`. Retain raw reports and manifests in each ignored result directory.
- Outcome: candidate `47638c272eecdebf204104aee36775e812701211` completed all ten cases with matching correctness fingerprints; comparator controls passed 4/4. Every in-map terrain case dropped from 25,688 to 8,256 B/op (67.86% less). All timing verdicts remain inconclusive, so retain the change for reduced allocations without claiming a runtime improvement. Full tables, SHAs, evidence paths and memory tradeoffs are in `benchmark-history.md`. Comparison command: `dotnet run --project BlockGame.Benchmarks -- compare artifacts/terrain-pool-baseline-20261111 artifacts/terrain-pool-candidate-20261111 artifacts/terrain-pool-comparison-20261111.json`.

## LOD0 neighbor lookup experiment (2026-11-11)

- Scope: replace conditional coordinate wrapping in the existing `Lod0Mesher.IsBlockNeighbor` with arithmetic shifts and masks. Face/AO callers only query coordinates in -1..16; shifting by four gives -1/0/1 and masking with 15 gives the local coordinate. No new classes or scripts; GPU work and buffer pooling are unchanged.
- Preserve the pre-existing AGENTS/script edits and document deletion in a separate baseline commit (`fb89622c1dd6ed26274f882bb2776390993ca6ad`), with user approval.
- Run the full test suite, then measure all seven existing `mesh/*` cases with the normal Measure job. Preserve `artifacts/lod0-neighbor-baseline-20261111` before editing engine code. Baseline: 388 tests passed, none skipped; all seven measurements completed.
- Extend the existing AO/diagonal test with eight cases at the top chunk corners, exercising negative and positive X/Z wrapping and positive Y wrapping. Check both diagonal orientations against explicit expected AO and index values.
- Run the full suite again, commit the candidate, then measure the same cases into `artifacts/lod0-neighbor-candidate-20261111`. Compare the saved directories with the existing `compare` command. Record final SHAs, results and interpretation in `benchmark-history.md`.
- Environment problems: `dotnet test -m:1` failed during MSBuild discovery inside the sandbox. Outside the sandbox that extra argument resulted in zero tests (exit 5); it is not appropriate for the current test runner. The documented plain `dotnet test` succeeded outside the sandbox. Use `C:/Users/Edwin/.dotnet/dotnet.exe` explicitly; do not modify PATH. Existing compiler/analyzer warnings remain. Run tests and measurements sequentially outside the sandbox, keeping the current power plan; normal desktop background load remains a possible source of noise.
- Logs: `artifacts/lod0-neighbor-baseline-20261111.log`, `artifacts/lod0-neighbor-candidate-tests.log`, and `artifacts/lod0-neighbor-candidate-20261111.log`. Raw BenchmarkDotNet reports, child correctness evidence and environment/revision manifests stay in their respective result directories and are not committed.
- First candidate (`82fa0b90492c5640cfb1406c15af10daf59d53f9`): 396 tests passed and seven Measure cases completed with matching correctness outputs. All comparator verdicts were inconclusive; small mean reductions did not clear the practical threshold plus uncertainty. Keep this experiment's evidence.
- Second iteration: additionally combine the three interior-coordinate comparisons in `IsBlock` into `(uint)(x | y | z) < 16u`. This is true exactly when all three signed coordinates are in 0..15; any negative or higher coordinate sets a bit outside the low four. Repeat the full tests, commit, and measure the unchanged seven cases into `artifacts/lod0-neighbor-candidate2-20261111`, comparing against the original baseline. Test/launcher logs use the same candidate2 prefix.
- Final outcome: candidate 2 (`a3bb1ff4e4c0b2f54d342a381725a8ffa7beb096`) passed 396 tests and all seven measurements. The comparator reports improved exposed-solid and floor, with five inconclusive rows and no regressions classified. All output fingerprints and allocations match. Retain the small implementation change; see `benchmark-history.md` for both result tables, evidence paths and limits. The null-air control rose by roughly 6.8 ns but remains below the declared timing floor; do not turn its percentage into a performance claim.

Use the .NET CLI directly to test, measure engine workloads with BenchmarkDotNet, and compare saved results. PowerShell scripts are optional shortcuts. GPU work and frame timing are outside these benchmarks.

## Manual workflow

Run from the repository root on Windows x64 with the SDK pinned in `global.json`.

Before a baseline measurement, finish and commit all changes: `git status --short` must be empty, including staged and untracked files. A local commit is sufficient; pushing is not required. Run the **entire** test suite successfully on that code before measuring. Do the same for the candidate. Failed tests must be fixed before collecting a performance baseline; historical test waivers are not part of this workflow.

```powershell
dotnet test
dotnet run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'mesh/*' --output artifacts/baseline

# Change the code, run the full tests, and commit.
dotnet test
git add <changed-files>
git commit -m "Optimize meshing"

dotnet run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'mesh/*' --output artifacts/candidate
dotnet run -c Release --project BlockGame.Benchmarks -- compare artifacts/baseline artifacts/candidate
```

Stop if a command fails. Save the complete baseline output directory **before** changing code. Keep the candidate results too. Existing nonempty result directories are never overwritten; choose fresh names for later experiments. Output under `artifacts/` is ignored by Git.

Measurement checks that the working tree is clean at startup. Running the full test suite and leaving the source unchanged during measurement are guidelines for the person or agent doing the work. There is no scripted test attestation, repeated Debug/Release preflight, source snapshot, result checksum, or session drift gate. The workload correctness checks still run in the benchmark processes; they do not replace `dotnet test`.

## Commands

```powershell
# Measure all engine cases; output defaults to a new artifacts/runs/<id> directory.
dotnet run -c Release --project BlockGame.Benchmarks

# Discover case IDs without running measurements.
dotnet run --project BlockGame.Benchmarks -- --list

# Quick iteration check; dirty working trees are allowed. Never use Smoke timings as a baseline.
dotnet run -c Release --project BlockGame.Benchmarks -- --mode Smoke --case 'mesh/*' --output artifacts/smoke

# Compare saved directories; comparison itself can run in Debug.
dotnet run --project BlockGame.Benchmarks -- compare artifacts/baseline artifacts/candidate

# Optionally choose the report path.
dotnet run --project BlockGame.Benchmarks -- compare artifacts/baseline artifacts/candidate artifacts/comparison.json
```

Repeat `--case` to select a union of patterns; every pattern must match. `Measure` uses BenchmarkDotNet's normal sampling; `Smoke` uses its Dry job. Benchmark execution requires Release and no attached debugger. Comparison writes JSON and Markdown to a new `artifacts/comparisons/<id>/` directory unless a report path is supplied.

## Results and environment

Keep BenchmarkDotNet's `results/` reports (Markdown, CSV, full JSON and raw measurements), logs, `manifest.json`, `children/` workload checks, and `benchmark-run.json` together. The last file is a comparison index derived from BenchmarkDotNet's measurements. No packing or hashing of result files is needed.

Each run records its Git revision and the operating system, processor, physical/logical core counts, architecture, SDK, runtime and benchmark job alongside its measurements. The comparison includes both environments. Use the same machine, OS, runtime and workload settings for baseline and candidate; incompatible environments are reported as not comparable. Run the full test suite on the measured revision on that machine. If you retain a test log, keep it alongside the corresponding results with the revision and environment. The comparator does not claim to have verified the full test suite.

Minimize background work: close unnecessary applications, avoid concurrent builds/benchmarks, and let the machine settle. Prefer the same power plan and power source for both runs. These are guidelines, not prerequisites enforced by scripts. BenchmarkDotNet leaves the current power plan unchanged. Background load and power-plan effects are treated as potential noise when reviewing variance and uncertainty; automation cannot remove a systematic bias from two runs. Repeat uncertain measurements under more consistent conditions.

## Comparing and interpreting

Keep the same benchmark workloads and settings across the engine change. Compare actual committed engine code, not a hand-copied reference implementation. Pair scenarios by ID, version, unit and input fingerprint, with matching correctness outputs. These internal scenario fingerprints identify the work being measured; they are not hashes of saved result files.

Each run records the Git tree ID of the committed `BlockGame.Benchmarks` directory (`git rev-parse HEAD:BlockGame.Benchmarks`). Engine changes outside that directory leave the tree ID unchanged. If the IDs differ, the comparison still shows its verdicts and warns in the report. Review the benchmark changes before attributing any timing difference to the engine.

The comparison is advisory. It reports timings and allocations, with `improved`, `regressed`, `inconclusive`, or `not comparable` verdicts. Its provisional policy uses a 5% practical threshold, 10 ns absolute threshold, 100 ns near-zero floor, 3% maximum CV, at least 10 samples, and a three-standard-error uncertainty allowance. Noisy, diagnostic and early-return results do not support speedup headlines. Review BenchmarkDotNet's statistics and warnings, not just the verdict.

Comparison uses result folders from the current run format (v4). Remeasure both revisions if you only have results from an older format.

## Optional scripts

These wrappers invoke the same commands and propagate failures:

```powershell
./scripts/verify-tests.ps1
./scripts/run-benchmarks.ps1 -Mode Measure -Case 'mesh/*' -OutputDirectory artifacts/baseline
./scripts/compare-benchmarks.ps1 -Baseline artifacts/baseline -Candidate artifacts/candidate
```

The run wrapper executes `dotnet test` once before benchmarking. Pass `-Dotnet <path>` if needed. The comparison wrapper accepts saved directories, not Git refs. Scripts do not create checkouts, enforce power plans, verify session drift, or pack/hash results.

## Adding scenarios

Define a stable ID, version, unit, timing class and serialized inputs in the workload catalog. Include independent correctness expectations and an output fingerprint. Register the benchmark case and check discovery and Smoke. Bump the version when inputs or semantic expectations change. Keep fresh-set and movement-path workloads distinct.

Comparator controls can be checked with:

```powershell
dotnet run --project BlockGame.Benchmarks -- validate-controls
```

## Scripted in-game frame benchmark

For `BlockGame --benchmark`, separate-process repeats, frame/update/streaming reports
and the throughput overlay, see [frame-benchmarking.md](frame-benchmarking.md).
