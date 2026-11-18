# Benchmarking

Use the SDK pinned in `global.json` on Windows x64. Run from the repository root.
Benchmark results, measured SHAs, tables and result-specific limitations belong in
[benchmark-history.md](benchmark-history.md). Raw output stays under ignored `artifacts/`.

## Compare Git revisions

```powershell
dotnet run -c Release --project BlockGame.Benchmarks -- revisions --baseline <ref> --candidate <ref> --case 'mesh/*' --output artifacts/revision-comparison
```

The command resolves both refs to commit SHAs, creates separate detached worktrees,
runs the full Release test suite on each, and measures two pairs in opposite order:
baseline/candidate, then candidate/baseline. This balances first/second position but
does not eliminate machine drift. Each pair is compared independently without pooling
samples; both must pass. Any regression fails; otherwise insufficient evidence returns 3.
It stops on test or measurement failure. Both revisions must contain a compatible
benchmark harness and support `dotnet test --solution Rendering.sln -c Release`.
No reference implementation is substituted for either engine revision.

Repeat `--case` to select multiple patterns; omit it for all workloads. `--dotnet <executable>`
selects the SDK host for child commands. The output path must not already exist.
The output retains test/measurement logs, resolved SHAs, execution order, complete
results under `pair-1`/`pair-2`, and aggregate `comparison.json`/`comparison.md`.
Temporary worktrees are removed on success and failure; logs/results remain outside
them. Cleanup does not force removal of unexpected changes: it reports an error and
preserves that worktree for inspection. The current checkout is not switched or edited.
`--timeout-seconds` bounds each child command (default 1800, range 1–86400). On timeout
the process tree is terminated, output is retained, and the run returns 1.

## Manual workflow

Pass the full suite and commit source before each Measure run. Preserve baseline
results before editing the engine. Use fresh output directories; never overwrite evidence.

```powershell
dotnet test --solution Rendering.sln -c Release
dotnet run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'mesh/*' --output artifacts/baseline
# Implement the candidate, run the full suite, and commit it.
dotnet test --solution Rendering.sln -c Release
dotnet run -c Release --project BlockGame.Benchmarks -- --mode Measure --case 'mesh/*' --output artifacts/candidate
dotnet run -c Release --project BlockGame.Benchmarks -- compare artifacts/baseline artifacts/candidate
```

Measure requires a clean Git tree, Release build and no debugger. Comparison accepts
saved result directories and an optional third argument specifying `report.json`.
The full test suite is a manual prerequisite for saved-directory comparison; the
revision command runs it automatically. Stop if any command fails.

```powershell
# Discover workloads or perform a functional smoke check.
dotnet run --project BlockGame.Benchmarks -- --list
dotnet run -c Release --project BlockGame.Benchmarks -- --mode Smoke --case 'mesh/*' --output artifacts/smoke
dotnet run --project BlockGame.Benchmarks -- validate-controls
```

Smoke allows dirty source and uses BenchmarkDotNet's Dry job. Its timings cannot
establish performance. Measure uses normal sampling. Optional wrappers under
`scripts/` invoke the same CLI and propagate its exit status.

## Comparison gate

| Exit | Meaning |
|---|---|
| 0 | Pass: comparable evidence with no detected regression; not proof of speedup |
| 1 | Command, test, measurement or report error |
| 2 | Fail: timing regression or increased managed bytes per operation |
| 3 | Indeterminate: incompatible, missing, noisy or insufficient evidence |

CPU comparisons pair scenario ID/version/unit/input fingerprint and correctness
output. They reject dirty/non-revision/Smoke runs and incompatible environments.
A changed benchmark harness tree blocks the gate pending a comparison with matching
workloads; per-row results remain visible. Full reports and warnings are retained.

The timing policy uses a 5% practical threshold, 10 ns absolute threshold, 100 ns
near-zero floor, maximum 3% CV, at least 10 samples, and a three-standard-error
uncertainty allowance. Within-budget normal rows can pass the regression gate.
Early-return/diagnostic and near-zero rows cannot establish speedup or pass a gate
alone. Noisy rows make the gate indeterminate. An established regression fails it.
This policy detects regressions in the measured workloads; it cannot establish
end-to-end FPS gains or eliminate systematic measurement bias.

Retain BenchmarkDotNet reports, raw measurements, `manifest.json`, `children/`
correctness checks and `benchmark-run.json` together. Run format v4 records engine
revision, benchmark tree, OS, CPU/core counts, architecture, SDK/runtime and job.
Use the same machine, runtime, workload, power plan and power source; avoid concurrent
builds or other heavy work. Repeat uncertain results. Older formats require fresh runs.

## Fixed-distance frame benchmarks

Frame benchmarks run the real game, including streaming and GPU submission. They
use fixed distances, never calibration. Distances must be supplied with `--sweep`
or `--render-distance`; there is no default distance list. Each distance uses three
fresh processes by default. Keep the exact same distance list, world, duration and script across revisions.
Pass the full tests and commit source before collecting comparison evidence.

```powershell
dotnet run -c Release --project BlockGame -- --benchmark --sweep 5,8,12 --world terrain --output artifacts/frame-base
# Run the same command on the candidate with a fresh output directory.
dotnet run -c Release --project BlockGame -- --benchmark --sweep 5,8,12 --world terrain --output artifacts/frame-candidate
dotnet run -c Release --project BlockGame -- --benchmark compare artifacts/frame-base/rd-08 artifacts/frame-candidate/rd-08
```

Compare each matching `rd-NN` separately; sweep roots are rejected because they mix
workloads. `--render-distance 8` selects one fixed distance instead of a sweep.
Both axes accept 5–60. Other options: `--duration` (30 seconds, range 1–600),
`--repeats` (3, range 1–20), `--world terrain|checkerboard`, `--script fly-sweep`,
and `--output` (new or empty directory). Calibration, `--baseline` replay and
`--max-render-distance` are no longer supported.

The script starts at `(0,150,0)`, yaw -90°, pitch -45°, flies horizontally at
32 blocks/s, and adds 12°/s yaw halfway through. Seed is 1, VSync off, frame cap
1000 Hz, MSAA 2, window 800×600, nominal updates 60 Hz. Checkerboard uses the exact
FastNoise tree `Ag@BBABA==`. World graph and script settings are recorded in the workload.
Do not resize, minimize or interact with the window. Escape aborts.

Warm-up waits for streaming to settle and 30 consecutive render intervals below
50 ms; a 60-second timeout invalidates the run. The unmeshable outer shell is expected.
Parents stop children after duration + 120 seconds or resident memory above half the
runtime-reported available memory (minimum limit 1 GiB). A single-process run has no
parent watchdog. Larger distances may exceed memory limits, especially for checkerboard.

Each repeat writes `summary.json`, `summary.md`, `frames.csv`, `updates.csv` and
`streaming.csv`. Parents write `repeats.json`/`.md`; sweeps write `sweep.json`/`.md`.
Reports include frame p50/p95/p99, update gaps, streaming throughput/queues and coverage,
plus revision, OS/runtime, CPU and GL device/driver. CPU render duration excludes swap;
frame intervals include loop scheduling, updates and prior swap. Neither is GPU timer time.

Frame comparison uses the same exit meanings, but gates median frame p50/p95/p99:
a slowdown over 5% fails. It requires at least three valid repeats per side, matching
workloads/environments, clean revisions, no update degradation, comparable work and
coverage, and at most 5% repeat spread in those frame metrics. Otherwise it returns 3.
This is a regression budget, not a statistical confidence interval. Cap-limited
results cannot support strong speedup claims. Invalid benchmark runs return 2;
launch/runtime errors return 1.

Path must be within 5% of speed × duration. Fewer than 90% of nominal updates is
degraded; pipeline failure, recording overflow, duration overshoot, interrupted runs,
fewer than 30 frames, absent drawn terrain or warm-up timeout invalidates a run.
Work counts/distance differing by over 5%, or coverage by over five percentage points,
block comparison. Resident coverage includes empty meshes; drawn coverage counts
nonempty target meshes submitted after frustum culling, not visible-terrain completeness.
Counters include completed work later discarded as stale and are approximate across workers.

## Adding workloads

Define stable IDs, versions, units, timing class, serialized inputs and independent
correctness expectations/output fingerprints. Register discovery and Smoke checks.
Bump versions when inputs or semantics change. Keep fresh-set and movement workloads
separate. Commit harness changes before collecting both baseline and candidate runs.

## Open follow-ups

| Priority | Follow-up | Reason |
|---|---|---|
| P1 | Add cold requestor population and asynchronous request-coalescing benchmarks with separate units. | Warm movement cases exclude provider calls, tasks and locks. |
| P2 | Measure retained capacity after a warm movement path. | Zero allocated bytes does not measure storage retained by lists and sets. |
| P3 | Print a copyable benchmark command with the resolved .NET executable. | The pinned SDK and optional executable override do not expose the resolved command in reports. |
| P3 | Normalize additional case-level BenchmarkDotNet statistical warnings. | Raw reports contain warnings beyond the currently normalized diagnostics. |
