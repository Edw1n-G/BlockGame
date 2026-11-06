# Worker benchmark operating guide

Use this guide for the next independent worker trial. Detailed history is optional in [docs/benchmark-history](../docs/benchmark-history).

## Before changing code

1. Save the baseline commit SHA. Keep benchmark/workload definitions unchanged during an engine optimization.
2. Use Windows x64, PowerShell 7 and the SDK pinned in `global.json`. Set `-Dotnet` if it is outside PATH.
3. List cases: `dotnet run -c Release --project BlockGame.Benchmarks -- --list`.
4. Run a focused execution check:

```powershell
./run-benchmarks.ps1 -Mode Smoke -Preflight Family -Case 'selection/typical/*'
```

Selection-only dependency preflight does not load FastNoise/content. Full engine correctness still requires the bundled native DLLs. Focused preflight is iteration evidence; full correctness is required for final revision comparisons.

## Measure an engine change

Commit the candidate, then supply the saved baseline SHA and candidate SHA:

```powershell
./compare-benchmarks.ps1 -Baseline BASELINE_SHA -Candidate CANDIDATE_SHA -Case 'selection/typical/*'
```

Replace the two SHA placeholders. The command makes clean sibling engine checkouts and one captured common harness, runs both revisions sequentially with full correctness, exports hashed bundles and writes an advisory JSON/Markdown comparison. The current working tree is preserved. Checkouts and evidence are retained.

The harness currently supports engine interfaces present at `907f124` and later compatible revisions. Older interfaces need a reviewed adapter/contract. Build failure is not permission to copy/reimplement the old algorithm as a baseline.

Use `-Mode Smoke` for a quick checkout-wiring check; smoke can never produce a performance verdict. Use `-Case 'pattern1','pattern2'` for a union; every pattern must match. `-OutputDirectory` and `-CheckoutDirectory` must be new. Do not unpack duplicate projects inside a benchmark repository.

## Read the result

- Pairing uses scenario ID, version, unit and canonical input fingerprint, independently of type/method/implementation/revision. Complete movement paths and radius changes are serialized.
- Correctness output fingerprints must match. An output-order/semantic change needs review before timing comparison.
- Fresh-set and closed-path units never pair. Movement paths contain two transitions, or nine for burst; provider calls, tasks and locks are excluded.
- `improved` / `regressed` require the declared practical budget plus sampling uncertainty. `inconclusive` is expected for null/noisy controls.
- Early-return, diagnostic and sub-100-ns rows have no speedup/percentage headline. Allocation differences are reported separately; missing is not zero.
- Policy defaults (5% practical drift, 3% maximum within-run CV, 100 ns floor) are provisional advisory settings validated by controls, not universal calibrated gates.
- Different harness/input/environment/runtime/native identities, failed correctness, missing/ambiguous cases, dirty engine revisions and historical model/control runs are `not comparable`.

## Portable evidence

Each successful run creates a ZIP plus SHA-256 sidecar, with inventory hashes, summary, contracts, source archive, correctness logs, child identities and raw JSON/CSV samples. The summary refers to relative paths. Case progress is streamed; full logs update while the process runs.

```powershell
dotnet BlockGame.Benchmarking/bin/Release/net10.0/BlockGame.Benchmarking.dll verify RUN.zip
dotnet BlockGame.Benchmarking/bin/Release/net10.0/BlockGame.Benchmarking.dll compare BASELINE.zip CANDIDATE.zip report.json
```

Review `comparison.md`, then preserve/attach the two ZIPs, sidecars and a short findings summary. Do not rely on local ignored `out/` paths alone.

Maintainers: [control validation](../docs/benchmarks/VALIDATION.md); `scripts/Test-BenchmarkHarness.ps1` checks orchestration failures. A new worker optimization has not been assigned by this pipeline change.
