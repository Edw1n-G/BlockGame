# CPU performance measurements

Requirements: Windows x64, PowerShell 7, .NET SDK selected by `global.json` (10.0.103/latestPatch), bundled native DLLs and production content. BenchmarkDotNet is pinned to 0.15.8. No GL context or debugger is used. Generation, noise, LOD0 meshing and pure chunk selection are separate families; this does not measure FPS, GPU upload, native memory, VRAM or requestor/scheduler throughput.

From the repository root:

```powershell
# Full correctness repetitions, with bounded process execution and retained logs
./verify-tests.ps1 -Repeats 20 -ParallelThreads 4

# Restore, Debug/Release correctness, Release build and all registered generated-process cases
./run-benchmarks.ps1 -Mode Smoke

# Actual measurements, then repeat with the same source/environment in a new bundle
./run-benchmarks.ps1 -Mode Measure -OutputDirectory out/benchmarks/baseline-a
./run-benchmarks.ps1 -Mode Measure -OutputDirectory out/benchmarks/baseline-b

# Narrow a run by stable case ID (PowerShell wildcard syntax: * and ?)
./run-benchmarks.ps1 -Mode Smoke -Case 'meshing/*/v1'

# Compare frozen original and incremental chunk-selection paths in one run
./run-benchmarks.ps1 -Mode Measure -Case 'selectionrebuild/*','selectiondelta/*','selection/*','selectionoriginal/*'

# Orchestration failure-path checks
./scripts/Test-BenchmarkHarness.ps1
```

Use `-Dotnet C:/Users/Edwin/.dotnet/dotnet.exe` when the pinned SDK is installed outside PATH. Generated builds use that host runtime's dotnet installation. List IDs after building with `dotnet BlockGame.Benchmarks/bin/Release/net10.0/BlockGame.Benchmarks.dll --list`. Unknown/empty selections fail. The direct executable requires `--output <new-empty-directory>` and `--mode Smoke|Measure`; use the wrapper to attach correctness evidence and acquire the machine-session measurement lock. Existing wrapper bundles are never overwritten. Builds/tests finish before measurement. Per-stage preflight timeout defaults to 300 seconds and whole benchmark timeout to 7200; both are configurable. Timeout terminates the owned process tree and keeps diagnostic logs. It does not dispose a native node under live workers.

`-Case` accepts an array of patterns. The direct CLI accepts repeated `--case` flags. Patterns form a union; each pattern must match at least one case, and empty arrays are rejected. The [chunk-selection worker trial](../BENCHMARK_WORKER_TRIAL.md) records the optimization, complete comparison, scope limits and pipeline improvement data. Movement rows measure one closed path (two transitions, nine for burst), including differences and active-set updates; they exclude provider calls, locks and tasks. They use warmed bounded scratch storage. Original reference code remains in the workload project and does not ship as an engine dependency.

`Smoke` uses BenchmarkDotNet Dry and is execution verification only. `Measure` uses the default adaptive warmup, pilot and measurement behavior, x64 RyuJIT and workstation GC. The current power plan is preserved and recorded. Shut down competing work before a decision-grade run and describe the environment with `-LoadNote`; the default explicitly records uncontrolled desktop load. The local mutex prevents other wrapper commands in this Windows session, not unrelated applications or direct benchmark launches.

Each wrapper bundle contains `result.json`, restore/build logs, `verification/result.json` and per-run correctness logs, plus `benchmarks/manifest.json`, BenchmarkDotNet logs, child evidence and `results/`. Reports retain Markdown, full JSON (including statistics, managed allocations and measurements) and raw measurements CSV. Nanoseconds/op and managed bytes/op have different meanings; missing data is unavailable. Each operation is one method call. Meshing includes CPU construction, a cheap summary and disposal returning a warmed buffer pair; generation includes density/block arrays; noise includes its array; selection includes HashSet construction. Content/native setup, fixture assertions, checksums, event handling and GPU work are outside timing. Meshing cleanup checks source fingerprints and pool/queue bounds.

The manifest records source commit plus dirty-file hashes (including untracked split files), harness/workload hashes, content and native identities, command and job, runtime, machine and power/load notes. Each generated child records its actual loaded assemblies/runtime, input parameters and native feature set where applicable. Source changes during a run invalidate completion. Workload definitions and frozen-terrain provenance are in [the workload README](../BlockGame.Workloads/README.md).

`suitableForComparison` remains false until a human or the future milestone-three workflow reviews correctness, identities, repeatability and environment. A completed measurement is not automatically an improvement/regression verdict. Compare only the same scenario/version/unit and harness on the same controlled machine; retain both raw bundles. See [the ordered plan and gate evidence](../BENCHMARK_MILESTONE_DRAFT.md).

The [initial baseline review](BASELINE_REVIEW.md) records two complete runs and a targeted noise investigation. Outside-map timing controls are diagnostic-only: extra sampling still produced a multimodal distribution and up to 8.69% coefficient of variation. Their correctness and managed allocation checks remain useful. Read raw logs for statistical warnings; the manifest warning array records validation messages only. Keep retained source snapshots zipped or outside this repository; unpacked duplicate benchmark projects can break BenchmarkDotNet discovery. Preserve a commit or source archive when measuring dirty work so later workers can reconstruct the recorded hashes.
