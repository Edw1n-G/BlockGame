# What this is

The CPU benchmark harness checks correctness, measures individual engine workloads, and compares the same scenarios across clean Git revisions. It records managed allocations alongside timings. GPU work and frame timing are outside these workloads.

## Quick start

Run from the repository root with PowerShell 7, Windows x64, and the SDK pinned in `global.json`. Pass `-Dotnet` if the SDK is outside PATH.

```powershell
./scripts/run-benchmarks.ps1 -Mode Smoke -Preflight Family -Case 'selection/typical/*'
./scripts/run-benchmarks.ps1 -Mode Measure -Case 'selection/typical/*'
./scripts/compare-benchmarks.ps1 -Baseline BASELINE_SHA -Candidate CANDIDATE_SHA -Case 'selection/typical/*'
```

Use `-Case 'pattern1','pattern2'` to select a union. Every pattern must match. Custom output and checkout directories must be new. Logs and results default to `artifacts/`. Each comparison retains its clean sibling checkouts under `BlockGame.bench/` beside the repository. Keep source snapshots zipped and retain the ZIPs, their SHA-256 sidecars, and the comparison report.

For the selection comparison, list the two approved failures by exact test name. Both revisions run the full suite. Only the baseline may report these failures, and the bundle's `verification/result.json` records the allowance and the failures from each run.

```powershell
$legacyFailures = @(
    'BlockGame.Tests.IncrementalSelectionTests.DeltaMatchesReferenceForRandomMovesResizesTeleportsAndWraparound'
    'BlockGame.Tests.ChunkRequestorMovementTests.ResizingAtSamePositionReconcilesPreviouslyCommittedChunks'
)
./scripts/compare-benchmarks.ps1 -Baseline bench/selection-baseline -Candidate 'codex/test&refactor' `
    -Case 'selection/*','selectiondelta/*' -BaselineAllowedFailures $legacyFailures
```

## Modes

- **Smoke** uses BenchmarkDotNet's Dry job to prove the harness runs. Its timings are never used for comparison. Family preflight is available for iteration.
- **Measure** runs the Release benchmark in a child process. Comparison requires full correctness and matching harness, environment, runtime, dependencies, and correctness output evidence. The script runs both clean sibling checkouts sequentially using the same harness.

## Scenario contracts

A contract records the scenario ID, version, unit, canonical input fingerprint, and timing class. The input fingerprint covers the serialized workload inputs. Timing classes are `normal`, `early-return`, and `diagnostic`. Type and method names identify implementations independently of the scenario contract.

Fresh complete sets and closed movement paths have separate units. A movement path has two transitions, or nine for burst, and excludes provider calls, tasks, and locks.

### Adding a scenario

- Define a stable scenario ID and version in the shared workload catalog.
- Specify one unit and the timing class.
- Serialize all inputs, including path steps, radii, seed, and configuration where applicable.
- Add independent correctness expectations and preserve the output fingerprint.
- Register the benchmark case and verify discovery and Smoke.
- Bump the version when the inputs or semantic expectations change.

## Verdicts and policy

| Verdict | Meaning |
| --- | --- |
| `improved` | The candidate is faster beyond practical drift and sampling uncertainty. |
| `regressed` | The candidate is slower beyond practical drift and sampling uncertainty. |
| `inconclusive` | Timing is within the budget, noisy, near zero, or diagnostic. |
| `not comparable` | Required evidence is incompatible, invalid, missing, or ambiguous. |

| Policy | Value |
| --- | ---: |
| Practical threshold | 5% |
| Absolute threshold | 10 ns |
| Near-zero floor | 100 ns |
| Maximum CV | 3% |
| Minimum samples | 10 |
| Uncertainty multiplier | 3 |

The thresholds are provisional and advisory. Managed allocation changes are reported separately. Missing allocation data stays unavailable.

## Controls

Four checks use deterministic in-code fixtures. The null pair stays inconclusive across stable and diagnostic cases. The positive pair detects a large improvement. The early-return pair suppresses ratios and percentages. The reversed positive pair detects a regression.

```powershell
dotnet run --project BlockGame.Benchmarks -c Release -- validate-controls
```

## Seam policy

A revision can be measured only if it contains the engine members that the selected workloads call (the seam). When an optimization replaces code behind a seam, the baseline is not a copy of old code inside the harness. It is a branch named `bench/<topic>-baseline`, created from the candidate's tree, in which the old algorithm sits behind the same seam, in one commit. The comparison then runs the same harness commit against both trees. A revision that does not build with the harness is reported as such and is never worked around.

## Known noise

The historical outside-map controls were multimodal, reaching 8.69% CV, and are diagnostic only. Early-return scenarios never produce a percentage headline.

## Roadmap

- Calibrate the comparison policy on a designated quiet runner using repeated measurements and an explicit policy for diagnostic-only controls.
- Run correctness and Smoke in general Windows CI. Retain evidence for runs on the designated measurement machine and enable failure thresholds only after repeatability has been demonstrated.
- Resolve FastNoise provenance or document a mitigation before extending claims to shared-node worker scaling.
