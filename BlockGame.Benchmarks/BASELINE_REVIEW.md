# Initial CPU baseline review

Reviewed 6 November 2026. **The measurement implementation is verified locally. These desktop runs are a provisional baseline, not an automatic regression gate.** Outside-map timing controls remain diagnostic-only; controlled-runner calibration is still open before milestone-three performance verdicts.

## Verified scope

The provider/requestor split on `c2a6ab2c8539e19d7a5151a5ed21f6c89471ce33`, branch `codex/test&refactor`, remained uncommitted during review. Shared v1 fixtures cover seven LOD0 meshes, eight terrain inputs, two noise grids and four pure-selection inputs. Selection excludes requestor tasks, events, generation and unloading. No GL or worker-pipeline performance is measured.

The final correctness command passed 331 tests across 20 Debug serial and 20 Debug collection-parallel runs (four threads), plus Release collection-parallel execution. MTP separately passed 331/331. All 21 generated Release child-process smoke cases passed. Failure-path checks covered failed/zero/skipped preflight despite a zero process exit, invalid repetition/worker limits, existing output, concurrency lock, nonzero child exits, watchdog termination of a process tree, invalid filters and isolated missing native/content dependencies.

## Reproduction identity

- Windows 11 `10.0.26200.9457`; AMD Ryzen 9 7900X, 12 physical / 24 logical cores.
- SDK 10.0.103; actual generated-child runtime .NET 10.0.3; x64 RyuJIT; concurrent workstation GC; BenchmarkDotNet 0.15.8 default adaptive sampling.
- Existing ChrisTitus Ultimate Power Plan preserved. Unrelated desktop load was not controlled; builds/tests finished before timing and there were no concurrent agent measurement jobs.
- Complete measured-source fingerprint: `65BA666A5A671D0DC05D0CC99D88939B77F6388F55436C1125FA45FAFE4C8A0F`. Both complete runs and the targeted control repeat reported unchanged source. Their source/workload/native identities match, and every loaded engine/workload/harness child assembly hash matches between complete runs.
- Native SHA-256: `BBB9741E55EF11596FA18E163548D40D853AEDF920338298651D9CA35D5D1050`; feature-set value 16382. Binary build provenance remains unresolved; this is not evidence for safe shared-node worker scaling.
- [Measured source snapshot](../out/benchmark-implementation/baseline-a/source-snapshot.zip): all 130 source entries verified against manifest hashes. It includes the dirty split and harness as measured. Later documentation updates and a debug-build guard warning cleanup are outside that snapshot; engine/workload code and timed benchmark methods are unchanged. Final Release smoke execution is rechecked after that cleanup.

## Two complete measurements

A ran 20:03:58–20:11:55 UTC; B ran 20:13:20–20:21:17 UTC. Each completed all 21 cases. The table shows mean **ns per operation**, B/A mean drift, and managed bytes/op, which matched exactly between runs. Drift measures repeatability of identical code, not an engine improvement or regression. Confidence intervals, original samples, GC metrics and outliers remain in full JSON/raw reports.

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

The maximum absolute mean drift in the complete pair was **3.29%**, with no code change. This does not establish a universal 3.29% threshold: two desktop runs cannot calibrate a regression policy, and within-run confidence intervals do not account for every source of between-process drift. Managed allocation results were repeatable; they exclude native allocations and VRAM. Meshing still allocates 432 managed bytes/op despite reusing its vertex/index buffers.

### Noisy controls and retained limitations

The first run's BenchmarkDotNet log reported a multimodal distribution for `terrain/outside-s1/v1` (mValue 3.2, identified from the raw samples with Perfolizer). In the complete pair, outside-map controls reached 6.51% within-run coefficient of variation. A third targeted run of both controls completed with unchanged source: outside-s1 mean 2.894 ns, 85 samples, CV 8.69%, another multimodal warning; outside-s42 mean 2.630 ns, 15 samples, CV 2.44%. Both still allocated 40 B/op.

These are sub-3-ns early-return/allocation controls, separate from the real terrain/noise workload. More sampling did not make outside-s1 precise enough for a small-change verdict. Keep their timing rows **diagnostic-only/inconclusive for automatic regression decisions**; retain correctness and allocation checks. Do not tune engine code or hide warnings to make the baseline look stable. The other 19 rows provide an initial local CPU baseline, with the observed between-run variation above. GPU/frame time, native memory, multiworker scaling and automated revision comparison remain unmeasured.

Read the raw BenchmarkDotNet log for statistical warnings; the manifest's `warnings` array currently contains validation messages only. An empty array does not certify a unimodal or precise distribution. Milestone three must incorporate statistical diagnostics and controlled-machine repeat evidence before issuing performance verdicts.

## Retained evidence

- [A bundle manifest](../out/benchmark-implementation/baseline-a/benchmarks/manifest.json), [A correctness](../out/benchmark-implementation/baseline-a/verification/result.json), [A raw results](../out/benchmark-implementation/baseline-a/benchmarks/results).
- [B bundle manifest](../out/benchmark-implementation/baseline-b-retry/benchmarks/manifest.json), [B correctness](../out/benchmark-implementation/baseline-b-retry/verification/result.json), [B raw results](../out/benchmark-implementation/baseline-b-retry/benchmarks/results).
- [Targeted outside controls](../out/benchmark-implementation/outside-controls-check/benchmarks/manifest.json), [repeatability data](../out/benchmark-implementation/repeatability-data.json).
- [Repeated correctness and final smoke](../out/benchmark-implementation/gate6-final-smoke/result.json), [MTP log](../out/benchmark-implementation/final-mtp.log), [failure-path checks](../out/harness-checks/fa96aaf96d484b6abb4e15408b74345e/result.json).
- [Final Release smoke after Debug-guard cleanup](../out/benchmark-implementation/final-release-smoke/result.json): 331-test Debug serial/parallel and Release preflight, all 21 generated cases completed, unchanged source. Final Debug/Release solution builds also passed; Debug benchmark execution was explicitly rejected as required.

Artifacts under `out/` are local and Git-ignored. Preserve/archive them before cleaning the workspace; a clean checkout does not contain these evidence files. The checked-in review records their interpretation, not portable CI proof.

One failed repeat attempt is retained at `out/benchmark-implementation/baseline-b/`. An unpacked source snapshot introduced a duplicate benchmark project into BenchmarkDotNet's recursive project discovery. No cases ran; the wrapper/manifest correctly recorded failure. The snapshot was zipped, its temporary unpacked directory removed after checking the target path, and the repeat succeeded in a new bundle. Keep source snapshots zipped or outside the repository's project-discovery tree.

## Next acceptance gate

Steps 0–6 of the implementation draft are complete; task 7's local collection/review is complete with the limitations above. Before decision-grade automated comparisons, calibrate on a designated quiet runner, retain repeated measurements, and explicitly exclude or separately calibrate the diagnostic-only controls. Then implement the milestone-three revision comparator using identical reviewed harness/workload inputs and sibling clean checkouts, validating comparability before calculating any verdict. See [the updated draft](../BENCHMARK_MILESTONE_DRAFT.md) and [commands](README.md).

