# Pipeline validation — 7 November 2026

The comparator is advisory and validates its packaged controls before every real comparison. Its current provisional policy is 5% practical drift plus sampling uncertainty, maximum 3% within-run CV, and a 100 ns timing floor. These are explicit policy settings, not universal calibrated performance gates.

| Control | Expected / verified outcome |
|---|---|
| Historical identical-code runs, 21 cases, up to 3.29% drift | All inconclusive |
| Historical walk-x model effect, about 97% | Improved in calibration mode only; refused as a real Git baseline |
| Noisy outside-map cases | Inconclusive; no ratio/percentage headline |
| Early-return `same`, about 28 ns | Inconclusive; absolute change/allocation retained, speedup suppressed |
| Measured deliberate extra consumed calculation | Regressed: about 781 µs versus 452 µs; doubled managed allocation |
| Actual clean `907f124` versus `907f124`, common captured harness | Inconclusive: -0.49%; equal output and allocation fingerprints |

## Provenance correction

Git revision `c2a6ab2` used float arithmetic and preallocated its chunk set. The trial's copied reference used doubles and different allocation behavior. The historical optimization percentage does **not** establish improvement over that revision. Those samples are retained as comparator fixtures; legacy-model benchmarks are excluded from normal discovery and cannot be production baselines. The current harness requires the selection interfaces present at `907f124`; older revisions fail explicitly until a reviewed adapter exists.

## Portable evidence

- [Control suite ZIP](../../artifacts/benchmark-controls-v1.zip) and [checksum](../../artifacts/benchmark-controls-v1.zip.sha256). Raw samples, original JSON and hashed inventories also live in `BlockGame.Tests/TestData/BenchmarkControls/` for repeatable tests.
- [Clean-null baseline](../../artifacts/clean-null-baseline-v1.zip), [candidate](../../artifacts/clean-null-candidate-v1.zip), adjacent checksum files, [comparison](../../artifacts/clean-null-v1-report.md), [JSON](../../artifacts/clean-null-v1-report.json) and [automatic control validation](../../artifacts/clean-null-v1-report.controls.json).
- Source archives, contracts, full correctness logs, generated-child identities and raw measurements are inside each run bundle. ZIP integrity and relocation are tested. Reports identify revisions, contract/unit, policy and comparator binary fingerprint.

## Acceptance checks

Tests cover mismatched units/versions/inputs/harness/environment, failed/smoke/focused/dirty/model runs, missing/ambiguous cases, absent allocations, near-zero values, changed correctness outputs, noisy/multimodal data, method/parameter discovery, and tampered bundles. Native/content preflight is selected by family. Streamed logging is checked before process exit and with both pipes flooded; watchdog tree termination and existing failure guards remain required.

Final checks passed: 380 tests in three serial and three collection-parallel Debug runs plus Release and MTP; 28 revision-eligible benchmark smoke cases with contracts/output evidence; Debug/Release builds; orchestration failure/streaming/dependency checks; clean-sibling end-to-end wiring and portable hash verification. Historical/control cases are excluded from normal discovery, explaining the reduction from the earlier 39-case count.

End-to-end testing caught generated `Runnable_N` child identities being mistaken for declared benchmark identities. Attribution now uses the declared type and full BenchmarkDotNet benchmark name, including method and parameters; missing output evidence fails normalization instead of producing a verdict.

Use the [worker operating guide](../../BlockGame.Benchmarks/README.md). The next independent worker trial remains separate. No new engine optimization was performed in this pipeline change.
