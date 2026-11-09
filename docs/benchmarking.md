# CPU benchmarking

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

The comparison is advisory. It reports timings and allocations, with `improved`, `regressed`, `inconclusive`, or `not comparable` verdicts. Its provisional policy uses a 5% practical threshold, 10 ns absolute threshold, 100 ns near-zero floor, 3% maximum CV, at least 10 samples, and a three-standard-error uncertainty allowance. Noisy, diagnostic and early-return results do not support speedup headlines. Review BenchmarkDotNet's statistics and warnings, not just the verdict.

Full-suite testing is a manual prerequisite in the current run format (v3). Workload checks are recorded separately. Older v2 bundles retain their old validation rules and can still be read, but cannot be paired with v3 runs; remeasure both revisions with the current workflow. Optional `pack`/`verify` commands exist only for archival compatibility and are not used by the normal workflow.

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
