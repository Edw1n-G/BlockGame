# Scripted frame and streaming benchmark

Run from the repository root with .NET 10. The application enters the game through
the deferred state transition, flies forward from `(0,150,0)` at 32 blocks/s while looking down 45°, and
adds a 12 degrees/s yaw sweep halfway through the run. Seed is 1. Each repeat uses
a separate process and a new engine instance. This is a cold process, not a cold
OS filesystem or GPU driver cache.

```powershell
dotnet test
# Baseline: increase radius until median frame time is near 16 ms.
dotnet run -c Release --project BlockGame -- --benchmark --world terrain --output artifacts/frame-bench/base-terrain
dotnet run -c Release --project BlockGame -- --benchmark --world checkerboard --output artifacts/frame-bench/base-checkerboard
# Candidate: reuse the baseline workload; NEVER recalibrate the candidate.
dotnet run -c Release --project BlockGame -- --benchmark --baseline artifacts/frame-bench/base-terrain --output artifacts/frame-bench/candidate-terrain
dotnet run -c Release --project BlockGame -- --benchmark compare artifacts/frame-bench/base-terrain artifacts/frame-bench/candidate-terrain
# Fixed-distance development run (no calibration):
dotnet run -c Release --project BlockGame -- --benchmark --world checkerboard --render-distance 4 --duration 5 --repeats 1
```

Use `C:/Users/Edwin/.dotnet/dotnet.exe` if the inherited PATH points at an old SDK.
The existing CPU microbenchmark workflow remains in [benchmarking.md](benchmarking.md).
Before actual base/candidate measurements, pass the full suite and commit source
changes so each measurement names an actual clean Git revision. Keep workloads,
machine, power plan and background load comparable. Preserve the original result
directories. Record measured SHAs and tables in `benchmark-history.md`. Development
smoke runs may be dirty; their results are functional checks, not performance claims.

Options after `--benchmark`:

| Option | Default | Meaning |
|---|---|---|
| `--render-distance` | terrain `12`, checkerboard `2` | Explicit value selects fixed radius; omit to calibrate. Horizontal and vertical radius, 1-60 |
| `--duration` | `30` | Measured wall-clock seconds, 1–600 |
| `--script` | `fly-sweep` | Version 2 downward-looking forward flight then forward flight with yaw |
| `--output` | `artifacts/frame-bench/<UTC timestamp>/` | New or empty directory; existing results are never overwritten |
| `--repeats` | `3` | Separate processes; calibration verification always uses at least 3 |
| `--world` | `terrain` | `terrain` or `checkerboard` (`Ag@BBABA==`) |
| `--max-render-distance` | `60` | Calibration radius ceiling |
| `--baseline` | none | Successful calibration folder/file; freezes world, duration, script and radius |

The frame cap remains 1000 Hz (1 ms), far above the 16 ms target, VSync is off, MSAA is 2, the initial window is 800×600,
and updates remain at 60 Hz. UI FPS and frame ms are measured one-second averages; a reading near 999 FPS is real cap-limited rendering, not 999 ms or a fixed display value. Settings controls and physical movement/block editing
are disabled in benchmark mode; Escape still aborts. Do not resize, minimize or
interact with the window during measurements. The summary records workload,
revision (including a dirty marker), OS, runtime, CPU identification and GL device/driver.

Warm-up waits for the initial selection, generation, meshing and uploads to settle, then for 30 consecutive render intervals below 50 ms. The script remains
stationary during warm-up, while terrain streams. A 60 second warm-up timeout writes
an invalid result. This avoids calibrating against an empty or partly uploaded startup scene. The unmeshable boundary shell is expected and does not prevent warm-up completion.

Each run writes:

- `summary.md`: readable settings, verdict, frame p50/p95/p99/max, CPU render p95,
  update count/max gap/hitch, path length, throughput and coverage.
- `summary.json`: versioned comparison input, including all validity reasons.
- `frames.csv`: elapsed time, end-to-end render-callback interval and CPU render duration.
- `updates.csv`: raw update timestamps relative to script start.
- `streaming.csv`: approximately one sample per second, plus initial/final state:
  cumulative request/generation/mesh/upload counts, generation/mesh/upload queue
  depths, target/resident/drawn counts and fractions, selection commits/rollbacks.

With repeats, each child writes into `run-01`, `run-02`, etc. The parent writes
`repeats.md` (median/min/max/spread for each metric) and `repeats.json` (all summaries).
Compare accepts individual `summary.json` files, individual run directories or repeat
directories. It prints percentage deltas of medians; positive frame-time deltas are
slowdowns, negative throughput deltas are regressions. A zero base gives `n/a`.

Validity and interpretation:

- Path length sums actual camera displacement per update. It must be within 5% of
  speed × requested duration. Updates use wall-clock elapsed time; a delayed update
  can catch up in distance but still fail update health. Updates after the deadline
  do not fabricate catch-up movement.
- Fewer than 90% of the expected 60 Hz updates marks the run **degraded**, separately
  from validity. This is recorded even for a calibrated baseline; comparisons also
  flag candidates with over 10% fewer updates than that baseline. Path failure, duration overshoot over
  5%, recording overflow, pipeline failure, fewer than 30 frames, interrupted runs,
  no sampled drawn terrain, or warm-up timeout make the run invalid.
  The maximum update gap includes the final interval without updates. Longest hitch
  is that maximum gap minus the nominal 16.67 ms, clamped at zero.
- Compared runs with over 10% fewer updates are explicitly degraded. Different
  settings/schema are incompatible; different environment is flagged. Work counts
  or distance differing by over 5%, or coverage by over five percentage points, are
  flagged. Frame deltas alone cannot establish speedup when work differs.
- Repeat spread is `(max - min) / median`. Any metric over 5% is marked `NOISY`.
  Three samples are a useful warning, not a confidence interval. Short or frame-cap
  limited runs cannot support strong performance conclusions.
- The target is the full requested ellipsoid at the current player position,
  including its unmeshable boundary shell. Resident coverage includes empty meshes.
  Drawn coverage counts nonempty target meshes submitted after frustum culling.
  Drawn coverage is normally much less than 100%; it is not occlusion visibility or
  completeness of visible terrain. Means are arithmetic means of recorded samples.
- Generation and meshing count completed CPU work, including results later discarded
  as stale. Upload counts GPU upload calls, not unique chunks. Requests count newly
  admitted generation requests. Queue depths exclude running jobs; mesh depth includes
  both pending dispatch and scheduler jobs. Selection rollbacks count canceled/failed
  plans. Counter snapshots are approximate across concurrent workers.
- The metrics recorder uses fixed preallocated arrays (2000 frames/s and 240 updates/s
  capacity) and allocation-free recording methods. Engine/UI allocations still occur;
  the once-per-second overlay formats its display text. The overlay is present in both
  normal gameplay and benchmark mode and shows generated/meshed/uploaded chunks/s plus
  current queue depths.
- CPU render duration includes world rendering and UI, but not buffer swap. Frame
  intervals include loop scheduling, updates and prior swap. Neither is GPU elapsed
  time. GPU timer queries and CI integration are deferred.

Exit codes: 0 for a completed valid run, 2 for a written invalid run or unsuccessful calibration, 1 for
launch/runtime/report errors. Repeat orchestration retains nonzero child status and
continues when a child wrote an invalid summary; a child without a summary stops it.
Comparison warnings are advisory and comparison does not change process exit status.

## Calibration and workload version 2

A plain `--benchmark` now calibrates the selected world. `--benchmark calibrate
--render-distance 12` explicitly calibrates starting at 12; `--benchmark
--render-distance 12` keeps 12 fixed. Each probe is a new process running the same
full-duration script as the eventual baseline. Increase by at least two chunks
(roughly 25% at larger radii), then bisect the first crossing in whole chunk radii.
The target is the median frame interval, **16 ms ±10%** (14.4–17.6 ms), not CPU-only
render time and not p95. The selected distance is verified with at least three more
fresh processes. Whole-chunk distance steps may not produce a value in tolerance.
Invalid paths, pipeline failure, timeout, exceeding the radius bound or process memory
limit is reported as calibration failure, never as a successful 16 ms baseline.

`calibration.json` and `calibration.md` retain every attempted radius, timings,
validity and the verified choice. `probes/` contains exploratory runs; `runs/`
contains the verification baseline. Comparisons automatically exclude probes when
passed a calibration folder. Only a successful current-version calibration with the same encoded world graph can
be used by `--baseline`. It locks world, duration, script and radius; conflicting
workload overrides are rejected. Calibration is separate for each world and each
baseline machine. Candidates must reuse the original workload so a slowdown is
not hidden by lowering their render distance. Update-rate degradation remains prominently flagged and does not alone block calibration. Repeat spread over 5% is still noisy.

Parent-launched runs stop if their resident process memory exceeds half the runtime-reported available physical memory (at least 1 GiB) or their
elapsed time exceeds duration + 120 seconds. Logs are retained beside each child
folder. The limit bounds CPU residency, not dedicated GPU memory. Explicit
single-process runs do not have this parent watchdog. Increasing radius raises
chunk count approximately cubically; the checkerboard starts at radius 2.

`checkerboard` passes the exact supplied FastNoise tree `Ag@BBABA==` to the existing
terrain generator. It is not an alternative hand-coded generator. The original
terrain graph remains available as `terrain`. World name and encoded tree are
recorded in the workload. Start altitude is 150, yaw is -90°, pitch is -45° and
flight remains horizontal at that altitude. Map bounds expand to cover the entire
script plus render radius, avoiding an artificial air-only boundary on long runs.
These changes bump script and summary versions to 2, so earlier horizontal-looking
runs are incompatible and must not be used as the new baseline.
