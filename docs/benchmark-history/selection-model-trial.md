# Chunk selection worker trial

**Superseded optimization claim:** the copied double-precision model did not reproduce the actual float arithmetic or set preallocation at Git revision c2a6ab2. Percentages below compare models; they are retained only as comparator controls and do not establish that engine revision speedup. The new pipeline requires real Git revision builds.

6 November 2026. **The rapid-movement CPU target is met:** ordinary movement is 97.56% faster and the nine-transition burst is 92.97% faster in the common-harness experiment. Fresh typical `Calculate` is 18.03% faster and allocates 58.86% fewer managed bytes. This is a component-level algorithm trial on the current dirty working tree, with a frozen original reference and retained raw evidence.

## Target and measured operation

The working target was at least 70% less CPU time for ordinary movement and rapid movement sequences, exact selected-set equivalence, and no material teleport regression. The original requestor rebuilt the complete ellipsoid, scanned the new set for additions, added pending coordinates, then scanned the active set for removals. Repeated notifications could leave it finishing requests for an obsolete target.

The new planner compares Y intervals in X/Z columns, using at most two cached immutable shapes and reusable addition/removal buffers. It visits columns and changed chunks instead of rebuilding and comparing the entire volume. An unchanged target takes an early return. `Calculate` itself still returns a complete HashSet: scanlines skip rejected cells and preallocate the result; very small radii retain a bounded scan to avoid column-table overhead.

The requestor uses the planner, retains its active set, checks update/reset versions while planning and requesting, rolls back additions from canceled or failed plans, and commits removals/current region under its lock. The invariant is that the committed region describes the active set; explicit unload clears that region. Stop/join and main-thread GPU ownership are preserved. Requests already in flight cannot be undone before their provider call returns.

**Movement unit:** one closed path, including CPU selection, differences and active-set updates. Every path has two transitions (out and back, or unchanged twice), except burst, which has eight successive target changes plus the return (nine). Default radii are horizontal 20 / vertical 15; resize visits 24 / 18. Paths begin at chunk (-4,-2,3), use LOD0 and return to the initial set. Setup warms both implementations and checks repeated equivalent work. Provider calls, locks, tasks, native generation, GPU work and frame timing are outside this timed operation. The real requestor integration is tested separately.

**Fresh-Calculate unit:** one newly allocated complete HashSet, including any column-table construction. It is separate from the warmed incremental path.

## Common-harness results

BenchmarkDotNet 0.15.8, default adaptive sampling, .NET SDK 10.0.103 / actual runtime 10.0.3, x64 RyuJIT, concurrent workstation GC; Windows 11 on Ryzen 9 7900X (12 physical / 24 logical cores). Existing power plan was preserved. Desktop load was not controlled. Both algorithms used the same workload/harness/runtime/loaded assemblies in this run. Source was unchanged throughout all 22 selected cases.

### Movement paths

Times are microseconds per complete path; managed allocations are bytes per path. Warmed planner allocations are zero in the raw MemoryDiagnoser data, rather than missing data.

| Scenario | Original µs/path | Incremental µs/path | Time reduction | Original bytes/path | Incremental bytes/path |
|---|---:|---:|---:|---:|---:|
| burst | 11,757.11 | 826.80 | 92.97% | 14,024,720 | 0 |
| diagonal | 2,431.80 | 87.44 | 96.40% | 2,947,278 | 0 |
| resize | 4,512.55 | 354.87 | 92.14% | 7,218,882 | 0 |
| same | 2,121.48 | 0.0286 | >99.99% | 2,801,102 | 0 |
| teleport | 5,098.65 | 1,424.16 | 72.07% | 8,619,929 | 0 |
| vertical | 2,345.65 | 65.59 | 97.20% | 2,947,278 | 0 |
| walk-x | 2,329.78 | 56.88 | 97.56% | 2,947,278 | 0 |

The unchanged-target row measures skipped work, not end-to-end event latency. Real requestor tasks/cancellation delegates still allocate; first population, shape cache misses and capacity growth also have costs. Retained memory is bounded by two cached shapes and the largest list/HashSet capacities reached; teleports can increase that retained capacity. MemoryDiagnoser's zero steady-state allocation does not mean zero retained memory.

### Fresh complete-set calculation

Times are nanoseconds per call. This comparison uses the frozen original `Calculate` and the production `ChunkSelection.Calculate` through the existing selection workload.

| Scenario | Original ns/call | Current ns/call | Time reduction | Original bytes/call | Current bytes/call |
|---|---:|---:|---:|---:|---:|
| negative | 75.82 | 79.42 | -4.74% | 368 | 368 |
| typical | 520,211.06 | 426,438.91 | 18.03% | 1,400,491 | 576,128 |
| vertical | 63.62 | 63.25 | 0.58% | 368 | 368 |
| zero | 12.11 | 12.41 | -2.49% | 176 | 176 |

The small negative-center case regressed by 3.60 ns (4.74%); zero radii regressed by 0.30 ns (2.49%), with unchanged allocations. These results are retained. The optimization targets repeated movement, which now uses the incremental planner rather than repeated fresh sets. Further complexity to chase these tiny initialization differences was not justified by this trial. Fresh typical selection still improves by 18.03%, and allocation falls from 1,400,491 to 576,128 B/call.

## Correctness and iteration evidence

- Before engine changes, the seven original movement cases completed using the pipeline. The frozen implementation reproduces the original selection/difference/active-set work. Original source copies are retained under `out/selection-trial/original-ChunkSelection.cs.txt` and `original-ChunkRequestor.cs.txt`.
- The first incremental version completed all seven cases. It measured 55.98 µs for walk-x and 831.90 µs for burst, versus the initial original's 2.315 ms and 11.762 ms. The later common-harness run confirmed the large gains. The final refinement retained a tiny-radius direct scan for fresh calculations; timed incremental methods were unchanged.
- The common run passed **352 tests in 20 serial and 20 collection-parallel Debug repetitions**, plus Release execution, before measuring. Tests compare exact sets with the independent frozen original across 429 small-radius/center combinations, normal larger radii, negative clamping, coordinate seams, wrapped vertical intervals, 100 deterministic random movement/resize/teleport transitions and cancellation/retry.
- Controlled requestor tests cover 200 notifications behind a blocked request: only the unavoidable in-flight request and the final seven-chunk set are requested (eight calls total). Reset during a request stays empty unless notified again; same-position resizing converges; a provider failure after publication rolls back partial work and permits retry. Existing shutdown, stale-generation and stale-upload tests remain green.
- All 22 common-run child records have matching loaded engine/workload/harness assembly identities. Each row has real samples and managed-allocation data. The full source fingerprint is `034BF52DBC0D21395046A3F24C0265B3FDBA5B09CBD23BBBF06C875197DD986B`.
- Movement noise in this run was small relative to the improvement: the largest candidate CV was 2.62% for walk-x (25 samples), compared with a 97.56% reduction. No multimodal/zero-measurement/minimum-iteration warning was found in the common-run log. Confidence intervals and raw samples are retained. This does not calibrate a general regression threshold or prove FPS improvement.

The target is met without another algorithm iteration. Teleport improved 72.07%; ordinary movement and burst exceed the declared 70% reduction target. Correctness and cancellation behavior are required parts of acceptance, not replaced by timing results.

Final verification also passed: Debug and Release solution builds, MTP 352/352 with none skipped, and all 39 registered Release smoke cases after a fresh Debug serial/parallel and Release correctness preflight. The final smoke recorded unchanged source. Existing NuGet vulnerability-data access warnings remain in build logs; no new compiler error was reported. The measured source ZIP was reopened and every one of its 137 entry hashes verified against the common-run manifest.

## Pipeline experience and improvement data

| Priority | Observed friction | Evidence / consequence | Recommended follow-up |
|---|---|---|---|
| P1 | Existing selection benchmarks measured fresh sets only | A useful 18% fresh-call gain understates the 93–98% improvement in movement work; existing cases did not expose whole-set difference scans or obsolete requests | Keep a workload ladder: fresh calculation, retained movement/difference, cold requestor population, then real asynchronous/coalescing integration. Give each its own unit |
| P1 | No automatic baseline/candidate pairing or verdict | This trial required a frozen reference, new benchmark families and manual JSON joins; type-based IDs differ between algorithms | Implement milestone-three comparability checks and reports using a shared scenario contract plus algorithm ID. Include method/parameter identity, units, correctness, warnings, native/runtime/environment and uncertainty |
| P1 | Movement metadata needs richer scenario serialization | Child evidence records scenario name, initial radii and transition count; exact centers/resized radii currently require the archived workload source | Serialize the complete per-step path/configuration and a canonical input fingerprint in each case contract |
| P2 | Wrapper initially accepted only one case pattern | Selecting original + candidate + fresh controls together required several patterns | **Fixed in this trial:** `-Case` accepts multiple patterns; repeated CLI `--case` flags form a union. Every individual pattern must match; empty lists and mixed valid/invalid patterns are rejected. Failure-path tests pass |
| P2 | Case discovery/registration is manual and assumes one method per type | Added original/incremental/reference families had to be registered in Program; using multiple benchmark methods would collide with current type/scenario IDs | Derive discovery from BenchmarkDotNet cases and include method identity; test multiple methods/parameters without breaking existing stable IDs |
| P2 | Long launcher output is buffered | The 22-case run took about seven minutes; progress required reading the raw BenchmarkDotNet log manually | Stream/tee child output while retaining watchdog behavior; provide completed/remaining case status and artifact links |
| P2 | Selection-only runs still require native/content setup | The launcher always constructs WorkloadEnvironment, and the wrapper runs the full engine preflight | Make benchmark dependency preflight family-specific. Keep full correctness required for final acceptance; optionally offer a clearly labeled focused iteration preflight |
| P2 | Working tree must remain completely unchanged during measurement | The full-tree hash also covers docs; implementation/report edits during a run invalidate completion | Use isolated sibling checkouts for concurrent work. Retain a source archive automatically; distinguish compiled inputs from documentation while preserving complete provenance |
| P2 | Zero allocation can be misread as zero application memory | Warmed lists/sets cache capacity; task/delegate allocations and initial population are outside the movement benchmark | Keep explicit pool/warmup policies and add cold/retained-memory scenarios before making broader memory claims |
| P3 | Local SDK selection needed an explicit executable | Used `C:/Users/Edwin/.dotnet/dotnet.exe` consistently | Discover/validate the pinned SDK early and emit the exact reusable command |
| P3 | Statistical warnings are not fully represented in the manifest | Current `warnings` field contains validation messages; raw logs must also be reviewed | Export BenchmarkDotNet statistical conclusions with affected case IDs before automated performance verdicts |

The pipeline did preserve distinct results, preflight correctness, nonzero discovery, failure propagation, native/source identities and raw data. The multi-pattern fix was checked alongside failed/zero/skipped preflights, timeout tree termination, missing dependencies, no-overwrite and concurrency safeguards. Remaining observations are follow-up data; they do not expand this trial into the full revision comparator.

## Reproduce and inspect

```powershell
./run-benchmarks.ps1 -Dotnet C:/Users/Edwin/.dotnet/dotnet.exe -Mode Measure -Case 'selectionrebuild/*','selectiondelta/*','selection/*','selectionoriginal/*' -TestRepeats 20 -OutputDirectory out/selection-trial/repeat
```

Use a new output directory. Run on the same quiet machine for decision-grade confirmation. Default all-case smoke now covers 39 registered cases; movement cases are new contracts, and existing v1 inputs remain unchanged.

- [Initial original run](../../out/selection-trial/original/benchmarks/manifest.json): seven cases, 21:11:46–21:13:47 UTC.
- [First candidate run](../../out/selection-trial/candidate-1/benchmarks/manifest.json): seven cases, 21:18:24–21:20:41 UTC.
- [Common-harness manifest](../../out/selection-trial/common-harness/benchmarks/manifest.json): 22 cases, 21:25:05–21:32:04 UTC; [correctness](../../out/selection-trial/common-harness/verification/result.json), [raw results](../../out/selection-trial/common-harness/benchmarks/results), [comparison data](../../out/selection-trial/comparison.json).
- [Measured source archive](../../out/selection-trial/common-harness/measured-source.zip): 137 files, hashes checked against the run manifest when archived directly as ZIP. Later report/docs updates are outside this measured source snapshot; timed implementation is unchanged.
- [Pipeline safeguard checks](../../out/harness-checks/5b9427cbf5f043aaa42489d7470005dc/result.json).
- [Final all-case smoke](../../out/selection-trial/final-all-smoke/result.json), [final correctness](../../out/selection-trial/final-all-smoke/verification/result.json), [MTP](../../out/selection-trial/final-mtp.log), [Debug build](../../out/selection-trial/final-Debug-build.log), [Release build](../../out/selection-trial/final-Release-build.log).

Artifacts under `out/` are local and ignored by Git; preserve the bundles/archive before cleaning. The checked-in report does not substitute for portable raw evidence or a Git-revision comparison. No engine optimizations outside chunk selection/request coordination were introduced.

