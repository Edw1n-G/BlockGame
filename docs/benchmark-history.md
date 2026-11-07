# First iteration

| Scenario | Original µs/path | Incremental µs/path | Time reduction | Original bytes/path | Incremental bytes/path |
|---|---:|---:|---:|---:|---:|
| burst | 11,757.11 | 826.80 | 92.97% | 14,024,720 | 0 |
| teleport | 5,098.65 | 1,424.16 | 72.07% | 8,619,929 | 0 |
| walk-x | 2,329.78 | 56.88 | 97.56% | 2,947,278 | 0 |

| Scenario | Original ns/call | Current ns/call | Time reduction | Original bytes/call | Current bytes/call |
|---|---:|---:|---:|---:|---:|
| typical | 520,211.06 | 426,438.91 | 18.03% | 1,400,491 | 576,128 |

Lessons

- **Superseded optimization claim:** the copied double-precision model did not reproduce the actual float arithmetic or set preallocation at Git revision c2a6ab2. The new pipeline requires real Git revision builds.
- Pipeline experience and improvement data

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

Lessons

- The maximum absolute mean drift in the complete pair was **3.29%**, with no code change.
- Meshing still allocates 432 managed bytes/op despite reusing its vertex/index buffers.
- In the complete pair, outside-map controls reached 6.51% within-run coefficient of variation. A third targeted run of both controls completed with unchanged source: outside-s1 mean 2.894 ns, 85 samples, CV 8.69%, another multimodal warning; outside-s42 mean 2.630 ns, 15 samples, CV 2.44%. Both still allocated 40 B/op.

## Second iteration

| Scenario | Unit | Result | Baseline ns | Candidate ns | Reason |
|---|---|---|---:|---:|---|
| selection-full/typical | one-fresh-complete-set | inconclusive | 457941.023 | 455707.331 | Within practical drift/uncertainty budget |

Lessons

- The clean null baseline and candidate comparison passed.
- Baseline: 907f124c13f46a50d0db4028066b9faf76938cd6; candidate: 907f124c13f46a50d0db4028066b9faf76938cd6.
- Policy: provisional 5% practical budget, 100 ns timing floor; no automatic performance gate.
- Inconclusive: -0.49%; equal output and allocation fingerprints
- At horizontal radius 2 and vertical radius 0, the original `c2a6ab2` float scan selected 25 chunks because `0f / 0f` is `NaN` and `NaN > 1f` is false. The new planner fixed this and selects 13. The legacy baseline keeps the original scan and explicitly waives the resize regression test and the incremental planner reference test.
