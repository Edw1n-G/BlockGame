# Chunk block storage in RAM — 2026-11-18

Generated in-bounds air chunks previously retained a 4096-element `ushort[]` despite
`ChunkData` already supporting implicit air through a null array. The final candidate
checks the interior density samples before allocating block storage. It skips the
extra Y plane used for surface detection and returns a normal, present air chunk.
Nonempty chunks retain the existing array layout, full ushort IDs and direct block
reads. No mesher, renderer, GPU, scheduler or provider code changes.

**Accepted on 2026-11-18.** The project retained the memory savings with the measured
1.36–2.50% slowdown in solid-s42 generation. Both final CPU comparisons and the RTX
frame comparison passed. Per 16,384 generated air chunks, Gen 0 collections fell
from eight to zero. Solid and surface cases stayed at eight. All measured cases
recorded zero Gen 1 and Gen 2 collections. All 469 tests passed again before acceptance,
including allocation of editable storage on the first non-air write.

## Revisions and correctness

- Baseline: `35f13626ec1529c7b99636862d781a182d2b2ee2`; 467 tests passed.
- First candidate: `d89431e01f6830644cc2cf8dda462a5258da6613`; 468 tests passed.
  Allocating lazily inside the material loop added a per-solid-voxel null check.
  Opposite-order CPU pairs returned 0 then 2: surface-s42 regressed 5.28% in pair 2
  (53.307 to 56.123 us/chunk). Rejected despite the first pair passing.
- Final candidate: `e079662399f58a66ad53ed319a9c9e96d58bda7e`; 469 tests passed,
  zero failures/skips. Moves solid detection ahead of the material loop.
- Identical benchmark tree: `b53d90836fd7f41c6d97136cd6889783a9f3bb36` for all three
  measured revisions. All measured source trees were clean. Terrain contracts are
  version 2: logical voxel values are hashed, so null air and explicit zero arrays
  compare equally. The harness/tests were committed before engine changes.
- Tests compare generated voxels to density/material rules across seeds and LODs,
  preserve independent edits and all ushort IDs, and check every interior density
  position plus positive halo-only data, zero/negative values and NaNs. Existing
  meshing, movement, shutdown and concurrency tests remain in the full suite.
- All six timed workloads match scenario identity, inputs and logical outputs;
  all three retained regions have matching whole-region logical fingerprints.

## Retained block payload

Seed 1, frozen terrain graph (also the current default), radius-12 selections of
7,153 chunks each. Centers are chunk coordinates at LOD 0. MiB means 1,048,576 bytes.
These are exact array payload counts, **not total process RAM**; chunk objects,
array headers, provider structures, mesh buffers and GPU storage are excluded.

| Region / center | Air chunks | Baseline MiB | Candidate MiB | Saved MiB | Payload reduction |
|---|---:|---:|---:|---:|---:|
| Spawn `(0,9,0)` | 7,153 | 55.883 | 0.000 | 55.883 | 100.00% |
| Surface `(0,-58,0)` | 575 | 55.883 | 51.391 | 4.492 | 8.04% |
| Deep `(0,-72,0)` | 115 | 55.883 | 54.984 | 0.898 | 1.61% |

The spawn sample is entirely air; its 100% figure must not be generalized to a
whole world. Nonempty block arrays are not compressed. Each eliminated array saves
8,192 payload bytes plus its managed header. BenchmarkDotNet reports generation
allocation falling from **8,256 to 40 B/chunk** for air (99.52% less); solid and
surface cases remain **8,256 B/chunk**. Forced-GC heap deltas are retained as
approximate diagnostics, not the basis of these exact payload claims. A profiler
snapshot was unnecessary for counting these directly owned arrays.

## Final CPU comparisons

Ryzen 9 7900X, 12 physical / 24 logical cores; Windows 11 build 26200.9457;
.NET SDK 10.0.103, x64 .NET 10.0.3, RyuJIT, workstation GC; BenchmarkDotNet 0.15.8.
Power plan preserved. Runs were sequential, without concurrent frame benchmarks.
Unit: one generated chunk; means in microseconds. Positive change means slower.

| Scenario | Base A us | Final A us | Change A | Base B us | Final B us | Change B |
|---|---:|---:|---:|---:|---:|---:|
| air-s1 | 50.476 | 48.843 | -3.23% | 50.836 | 48.296 | -5.00% |
| air-s42 | 50.773 | 47.683 | -6.09% | 50.539 | 47.422 | -6.17% |
| solid-s1 | 53.464 | 53.545 | +0.15% | 53.676 | 54.149 | +0.88% |
| solid-s42 | 53.418 | 54.755 | +2.50% | 53.685 | 54.417 | +1.36% |
| surface-s1 | 53.490 | 53.182 | -0.58% | 53.345 | 53.816 | +0.88% |
| surface-s42 | 53.278 | 53.486 | +0.39% | 53.307 | 54.217 | +1.71% |

Both final comparisons return **0**. Air-s42 clears the improvement threshold in
both; all other timing verdicts are inconclusive within the practical budget and
sampling uncertainty. The small repeated solid-s42 cost is still disclosed for the
user's stricter no-speed-loss requirement. No samples were pooled. Baselines A/B
come from the opposite-order first-candidate trial; the final candidate was then
measured twice in fresh runs. These final comparisons therefore do **not** constitute
an independently interleaved opposite-order trial of the final candidate; machine
drift and broader workload coverage remain limitations.

## RTX 5070 Ti frame check

Three fresh 30-second flights per revision, checkerboard world, distance 5,
800x600, MSAA 2, VSync off, 1000 FPS cap, identical script. All six final/baseline
runs valid, with healthy update rates and comparable streaming work/coverage.
Actual renderer: NVIDIA GeForce RTX 5070 Ti, driver 610.62. Gate exit **0**.

| Metric | Baseline median | Final median | Reported change |
|---|---:|---:|---:|
| Frame p50 | 1.001 ms | 1.001 ms | 0.0% |
| Frame p95 | 1.071 ms | 1.076 ms | +0.4% |
| Frame p99 | 1.917 ms | 1.916 ms | 0.0% (rounded) |
| Updates in 30 s | 1,792 | 1,792 | 0.0% |

The median is cap-limited. This check detects no frame regression in this workload;
it cannot prove unchanged performance at all distances/worlds or establish an FPS
speedup. The checkerboard frame workload is a load/regression check, not the terrain
memory census. Earlier integrated-AMD-GPU runs had degraded updates and were not
compared against RTX results. The first candidate's RTX gate also passed, but its
CPU regression disqualified it.

## Retained evidence

Workflow and command options remain in `docs/benchmarking.md`. Local output under
`artifacts/chunk-storage/` is ignored and not committed:

- `base-memory-v2/`, `candidate-memory/`, `candidate2-memory/`: exact storage census,
  approximate managed heap deltas, identities and logical hashes.
- `cpu-replicated/`: first-candidate opposite-order trial, complete BDN reports,
  raw measurements, manifests, child evidence, test logs and pair comparisons.
- `candidate2-cpu-v2/`, `candidate2-cpu-repeat/`: complete final CPU measurements;
  `candidate2-comparison-1.json`/`.md`, `candidate2-comparison-2.json`/`.md`: gates.
- `gpu-base-frame/`, `gpu-candidate2-frame/`, `gpu-candidate2-comparison.md`: RTX
  baseline/final frame evidence; `gpu-candidate-frame/` contains the rejected
  first candidate's RTX measurements.
- `candidate2-tests.log`: final full-suite output. Baseline and first-candidate
  full test logs also live under `cpu-replicated/`.
- Preliminary `base-memory/` has invalid cross-region GC deltas and missing center
  serialization; superseded by `base-memory-v2/`. The first CPU baseline was stopped
  after correcting that harness. `candidate-cpu/` was interrupted at the user's GPU
  connection request. `candidate2-cpu/` failed project discovery because a nested
  temporary checkout duplicated the benchmark project; it contains no timing evidence.
  The clean temporary checkout was removed before the successful retry. All trial
  worktrees were removed after use; original logs remain preserved.

# World block access conversion — 2026-11-17

Optimize the shared `ChunkData.WorldToLocal` helper, previously unoptimized in this history, by replacing double remainder with low-bit masks for 16-block chunks. Actual provider reads are measured; the prior mesher-private lookup is not changed.

- Harness/baseline: `877135d575dffbdab3790c103337675324a9db37`; full suite 431 passed, zero failed/skipped.
- Candidate: `8b8f60f992ea54701b6cd68ccbd0f38631da238c`; full suite 432 passed, zero failed/skipped.
- Both measured trees clean; identical harness tree `eb6e53a1217d6e43a01b2143466c672150db3eff`. New family discovery and all four Smoke cases passed before baseline measurement. Comparator controls passed 4/4.
- All four scenario IDs/versions/units/input fingerprints and output fingerprints match. Per-read correctness uses independent floor division; candidate tests also cover integer extremes and chunk boundaries.
- AMD Ryzen 9 7900X, 12 physical / 24 logical cores; Windows 11 25H2 build 26200.9457; SDK 10.0.103; x64 .NET 10.0.3, RyuJIT, workstation GC; BenchmarkDotNet 0.15.8. Sequential runs, power plan preserved.

All scenarios are version 1. Unit: `one-batch-of-256-world-block-reads`, including provider lookup and checksum. Times below are ns per **batch**, not per single read. Negative change means less elapsed time.

| Scenario | Baseline ns/batch | Candidate ns/batch | Time change | Baseline / candidate B/batch | Advisory verdict |
|---|---:|---:|---:|---:|---|
| block-access/missing | 947.422 | 997.062 | +5.24% | 0 / 0 | inconclusive |
| block-access/mixed | 1916.803 | 1501.584 | -21.66% | 0 / 0 | improved |
| block-access/negative | 1956.799 | 1583.270 | -19.09% | 0 / 0 | improved |
| block-access/positive | 1851.319 | 1430.448 | -22.73% | 0 / 0 | improved |

Retained the optimization for the three loaded-chunk improvements. The missing-chunk control bypasses the conversion and rose 49.64 ns/batch, within the comparator's combined practical/uncertainty budget; its timing is not proven unchanged. Both runs have no normalized statistical diagnostics; raw reports retain outlier removals. One pair over warmed, fixed inputs is not replicated evidence, a raycast benchmark, or an FPS estimate. No claim is made about concurrent or cold access.

Evidence: `artifacts/block-access-baseline-20261117/`, `artifacts/block-access-candidate-20261117/`, and `artifacts/block-access-comparison-20261117.{json,md,log}`.

# LOD0 face vertex reservation — 2026-11-17

Baseline: `3f42e9a9e2765132988b28c69faa062ca72d3d4f`.
Candidate: `0e1df0079a4f8bc01d7561ab3f79c5d16d6f7752`.
Unchanged benchmark tree: `a2c571cefff70eb671cb784fee37cc3ccbcced29`.

Reserve vertex storage once per quad instead of once per vertex. Full suites: baseline 425/425, candidate 427/427, none skipped; comparator controls 4/4. All seven scenario contracts and output fingerprints match. Both measured revisions were clean.

Environment: AMD Ryzen 9 7900X (12 physical / 24 logical cores), Windows 11 25H2 build 26200.9457, x64 .NET 10.0.3, SDK 10.0.103, BenchmarkDotNet 0.15.8, RyuJIT, concurrent workstation GC. Measurements ran sequentially and left the power plan unchanged.

All rows use scenario version 1 and unit `one-lod0-build-and-return-buffers`. Means are nanoseconds per operation; negative change means less time. Allocation is 408 B/op in both revisions for every row.

| Scenario | Baseline ns | Candidate ns | Time change | Advisory verdict |
|---|---:|---:|---:|---|
| mesh/allocated-air | 1334.095 | 1335.226 | +0.08% | inconclusive |
| mesh/checkerboard | 445363.062 | 385629.736 | -13.41% | improved |
| mesh/exposed-solid | 88007.264 | 78886.721 | -10.36% | improved |
| mesh/floor | 53436.054 | 48330.193 | -9.56% | improved |
| mesh/frozen-terrain | 17864.839 | 18061.420 | +1.10% | inconclusive |
| mesh/null-air | 25.303 | 26.075 | suppressed: below timing floor | inconclusive |
| mesh/occluded-solid | 12507.148 | 12607.185 | +0.80% | inconclusive |

Retained the candidate for the three measured geometry improvements. One baseline/candidate pair is not replicated evidence or an FPS estimate; frozen terrain shows no demonstrated benefit. The null-air control cannot support percentage claims. BenchmarkDotNet reported outlier removals; the comparator classified no regressions.

Reports: `artifacts/face-batch-baseline-20261117/`, `artifacts/face-batch-candidate-20261117/`, and `artifacts/face-batch-comparison-20261117.{json,md}`.

# First iteration

| Scenario | Original µs/path | Incremental µs/path | Time reduction | Original bytes/path | Incremental bytes/path |
|---|---:|---:|---:|---:|---:|
| burst | 11,757.11 | 826.80 | 92.97% | 14,024,720 | 0 |
| teleport | 5,098.65 | 1,424.16 | 72.07% | 8,619,929 | 0 |
| walk-x | 2,329.78 | 56.88 | 97.56% | 2,947,278 | 0 |

| Scenario | Original ns/call | Current ns/call | Time reduction | Original bytes/call | Current bytes/call |
|---|---:|---:|---:|---:|---:|
| typical | 520,211.06 | 426,438.91 | 18.03% | 1,400,491 | 576,128 |

Result notes

- **Superseded optimization claim.** The copied double-precision model did not reproduce the actual float arithmetic or set preallocation at Git revision `c2a6ab2`. Use the [current workflow for comparing Git revisions](benchmarking.md#manual-workflow).

## Terrain density buffer pooling — 2026-11-11

Goal: eliminate the short-lived density array allocated by every in-map `TerrainGenerator.GenerateChunk` call. The existing generator now rents a scratch array from `ArrayPool<float>.Shared`, fills an exactly sized span through the existing `NoiseCalculator`, and returns it in `finally`. The public owning-array API and block classification remain unchanged. No new classes or scripts.

- Baseline: `9cd3797972f2a3120e035ed8721c947f31920ec2`; full suite **396 passed**, zero failures/skips.
- Candidate: `47638c272eecdebf204104aee36775e812701211`; full suite **402 passed**, zero failures/skips.
- Both measured trees were clean. Both runs completed all ten existing `terrain/*` and `noise/*` cases with the normal Measure job. Identical benchmark harness tree: `a2c571cefff70eb671cb784fee37cc3ccbcced29`.
- Environment: AMD Ryzen 9 7900X, 12 physical / 24 logical cores, Windows 11 10.0.26200.9457, SDK 10.0.103, runtime .NET 10.0.3, BenchmarkDotNet 0.15.8, x64 RyuJIT, workstation GC. Runs were sequential, with the power plan unchanged; desktop background load remains uncontrolled.

Times are mean ns/op. Terrain unit: one chunk generation; noise unit: one grid generation including an owned result array. All runtime verdicts are **inconclusive**; the allocation verdict is separate.

| Scenario | Baseline ns | Candidate ns | Baseline B/op | Candidate B/op | Allocation verdict | Timing verdict |
|---|---:|---:|---:|---:|---|---|
| noise/grid-s1 | 48815.984 | 47242.241 | 17432 | 17432 | unchanged | inconclusive |
| noise/grid-s42 | 47132.379 | 47556.492 | 17432 | 17432 | unchanged | inconclusive |
| terrain/air-s1 | 53702.559 | 51166.194 | 25688 | 8256 | decreased | inconclusive |
| terrain/air-s42 | 51364.035 | 50520.520 | 25688 | 8256 | decreased | inconclusive |
| terrain/outside-s1 | 2.768 | 3.206 | 40 | 40 | unchanged | inconclusive: early return |
| terrain/outside-s42 | 2.932 | 3.055 | 40 | 40 | unchanged | inconclusive: early return |
| terrain/solid-s1 | 54041.480 | 53327.472 | 25688 | 8256 | decreased | inconclusive |
| terrain/solid-s42 | 54535.691 | 54298.378 | 25688 | 8256 | decreased | inconclusive |
| terrain/surface-s1 | 54497.735 | 54977.970 | 25688 | 8256 | decreased | inconclusive |
| terrain/surface-s42 | 54464.323 | 54328.680 | 25688 | 8256 | decreased | inconclusive |

The comparator passed **4/4 controls**, matched scenario IDs/versions/units/input fingerprints and output fingerprints for all ten cases, and reported no harness mismatch. The six in-map terrain cases each save **17,432 B/op (67.86%)**, exactly the removed float-array allocation. Reported Gen0 collections per 1,000 operations fall from 1.5259 to 0.4883 in those rows. The final candidate has no normalized statistical diagnostics. Outlier handling is preserved in the raw reports; the baseline outside-s42 control has about 3.83% CV and cannot support a timing claim in any event.

The density buffer pooling change was retained as an **allocation optimization**, with no claimed runtime or FPS gain. In-map timing differences range from roughly -4.72% to +0.88%, within the declared practical/uncertainty budget. Noise-only control timings also drift in both directions. Outside-map timings are early-return diagnostics, so ratios are suppressed. A warmed pool reduces allocation churn but retains reusable arrays; cold misses may allocate, and this experiment does not measure retained memory or whole-game GC pauses.

Additional tests cover complete writes into NaN-prefilled destinations, guard preservation, too-short buffer rejection, retained outputs after reuse, step normalization and concurrent generation at different coordinates/LODs. The concurrency test is not proof of general FastNoise thread safety.

Retained local evidence (ignored, not committed):

- `artifacts/terrain-pool-baseline-20261111/` and `artifacts/terrain-pool-candidate-20261111/`: full manifests, child correctness evidence, normalized results and raw BenchmarkDotNet reports/measurements.
- `artifacts/terrain-pool-comparison-20261111.json`, `.md` and `.log`: comparison results, including allocation deltas in JSON.
- `artifacts/terrain-pool-baseline-tests-20261111.log` and `artifacts/terrain-pool-candidate-tests-20261111.log`: complete test logs. Launcher logs use the result-directory names plus `.log`.

## LOD0 coordinate lookup optimization — 2026-11-11

Measured actual committed engine revisions using the unchanged existing seven `mesh/*` workloads and BenchmarkDotNet Measure defaults:

- Baseline: `fb89622c1dd6ed26274f882bb2776390993ca6ad` (388/388 full-suite tests passed).
- Candidate 1, neighbor wrapping with shifts/masks: `82fa0b90492c5640cfb1406c15af10daf59d53f9` (396/396 tests passed).
- Candidate 2, additionally combine the interior coordinate checks: `a3bb1ff4e4c0b2f54d342a381725a8ffa7beb096` (396/396 tests passed).
- All three measured trees were clean. Benchmark harness tree was identical: `a2c571cefff70eb671cb784fee37cc3ccbcced29`. No new classes or scripts were added.
- Environment: AMD Ryzen 9 7900X, 12 physical / 24 logical cores, Windows 11 10.0.26200.9457, SDK 10.0.103, runtime .NET 10.0.3, BenchmarkDotNet 0.15.8, x64 RyuJIT, workstation GC. Power plan unchanged. Measurements ran sequentially; ordinary desktop background load was not controlled.

Unit: one LOD0 build and return of pooled buffers; all times are mean ns/op. Both candidate verdicts compare against the same preserved baseline. Managed allocation was **408 B/op in every row of all three runs**.

| Scenario | Baseline ns | Candidate 1 ns | C1 verdict | Candidate 2 ns | C2 mean change | C2 verdict |
|---|---:|---:|---|---:|---:|---|
| mesh/allocated-air | 1355.742 | 1353.833 | inconclusive | 1357.601 | +0.14% | inconclusive |
| mesh/checkerboard | 480364.262 | 458112.584 | inconclusive | 455706.198 | -5.13% | inconclusive |
| mesh/exposed-solid | 94270.444 | 89551.228 | inconclusive | 88031.875 | -6.62% | improved |
| mesh/floor | 56497.081 | 53534.940 | inconclusive | 52794.102 | -6.55% | improved |
| mesh/frozen-terrain | 19644.352 | 19207.755 | inconclusive | 18853.233 | -4.03% | inconclusive |
| mesh/null-air | 26.671 | 25.710 | inconclusive | 33.516 | suppressed | inconclusive: near-zero |
| mesh/occluded-solid | 13170.599 | 12919.569 | inconclusive | 12832.978 | -2.56% | inconclusive |

Both comparisons passed the four comparator controls, matched scenario ID/version/unit/input and correctness output fingerprints for all seven rows, and reported no harness mismatch. All three runs completed all seven workloads. BenchmarkDotNet removed outliers as recorded in its retained reports; the final run has no normalized statistical diagnostics. Candidate 2's exposed-solid mean/SD is 88031.875/255.814 ns and floor mean/SD is 52794.102/134.530 ns; corresponding baseline values are 94270.444/702.332 ns and 56497.081/478.412 ns.

Candidate 2 was retained. Exposed-solid and floor were the only cases to clear the advisory 5% practical threshold plus sampling uncertainty. Checkerboard's 5.13% mean reduction does not clear that combined budget. Frozen terrain does not establish a practical improvement. Null-air increased by about 6.8 ns in the final run; its early return is below the 100 ns floor and bypasses the changed lookups, so no ratio or speedup claim is appropriate. These CPU workload results do not establish an FPS improvement or a general improvement across machines.

The eight new AO cases verify known lighting values and diagonal choices across top chunk corners.

Retained local evidence (ignored, not committed):

- `artifacts/lod0-neighbor-baseline-20261111/`, `artifacts/lod0-neighbor-candidate-20261111/`, `artifacts/lod0-neighbor-candidate2-20261111/`: complete manifests, normalized results, child correctness evidence, raw measurements and BenchmarkDotNet reports.
- `artifacts/lod0-neighbor-comparison-20261111.json` / `.md` and `artifacts/lod0-neighbor-comparison2-20261111.json` / `.md`: both comparisons.
- Launcher logs next to those run directories; full candidate test logs in `artifacts/lod0-neighbor-candidate-tests.log` and `artifacts/lod0-neighbor-candidate2-tests.log`. The successful baseline test summary was observed in the command output (388 passed, zero failed/skipped), not saved as a separate log.

## Baseline review

| Stable case ID | A mean ns/op | B mean ns/op | Mean drift | Managed bytes/op |
|---|---:|---:|---:|---:|
| `meshing/allocated-air/v1` | 1,365.31 | 1,352.75 | -0.92% | 432 |
| `meshing/checkerboard/v1` | 1,446,538.24 | 1,443,872.03 | -0.18% | 432 |
| `meshing/exposed-solid/v1` | 214,649.58 | 211,736.61 | -1.36% | 432 |
| `meshing/floor/v1` | 136,871.28 | 136,360.46 | -0.37% | 432 |
| `meshing/frozen-terrain/v1` | 31,837.79 | 31,188.25 | -2.04% | 432 |
| `meshing/null-air/v1` | 26.46 | 27.33 | 3.29% | 432 |
| `meshing/occluded-solid/v1` | 13,025.79 | 13,222.30 | 1.51% | 432 |
| `noise/grid-s1/v1` | 47,459.57 | 47,388.68 | -0.15% | 17,432 |
| `noise/grid-s42/v1` | 47,620.26 | 47,619.17 | -0.00% | 17,432 |
| `selection/negative/v1` | 78.37 | 78.68 | 0.40% | 368 |
| `selection/typical/v1` | 538,381.35 | 528,139.20 | -1.90% | 1,400,491 |
| `selection/vertical/v1` | 65.50 | 64.31 | -1.81% | 368 |
| `selection/zero/v1` | 12.65 | 12.65 | 0.02% | 176 |
| `terrain/air-s1/v1` | 51,203.67 | 51,995.27 | 1.55% | 25,688 |
| `terrain/air-s42/v1` | 52,011.21 | 51,300.96 | -1.37% | 25,688 |
| `terrain/outside-s1/v1` | 2.73 | 2.77 | 1.59% | 40 |
| `terrain/outside-s42/v1` | 2.80 | 2.78 | -0.90% | 40 |
| `terrain/solid-s1/v1` | 54,066.61 | 54,765.25 | 1.29% | 25,688 |
| `terrain/solid-s42/v1` | 55,010.45 | 55,284.18 | 0.50% | 25,688 |
| `terrain/surface-s1/v1` | 54,941.62 | 54,906.00 | -0.06% | 25,688 |
| `terrain/surface-s42/v1` | 54,408.05 | 55,597.32 | 2.19% | 25,688 |

Result notes

- The maximum absolute mean drift in the complete pair was **3.29%**, with no code change.
- In this baseline pair, meshing allocated 432 managed bytes/op with vertex and index buffer reuse.
- In the complete pair, outside-map controls reached 6.51% within-run coefficient of variation. A third targeted run of both controls completed with unchanged source: outside-s1 mean 2.894 ns, 85 samples, CV 8.69%, another multimodal warning; outside-s42 mean 2.630 ns, 15 samples, CV 2.44%. Both still allocated 40 B/op.

## Second iteration

| Scenario | Unit | Result | Baseline ns | Candidate ns | Reason |
|---|---|---|---:|---:|---|
| selection-full/typical | one-fresh-complete-set | inconclusive | 457941.023 | 455707.331 | Within practical drift/uncertainty budget |

Result notes

- The clean null baseline and candidate comparison passed.
- Baseline: 907f124c13f46a50d0db4028066b9faf76938cd6; candidate: 907f124c13f46a50d0db4028066b9faf76938cd6.
- Historical comparison policy: 5% practical budget, 100 ns timing floor; advisory at the time.
- Inconclusive: -0.49%; equal output and allocation fingerprints
- At horizontal radius 2 and vertical radius 0, the original `c2a6ab2` float scan selected 25 chunks because `0f / 0f` is `NaN` and `NaN > 1f` is false. The new planner fixed this and selects 13. That legacy comparison used the original scan and waived the resize regression test and the incremental planner reference test.
