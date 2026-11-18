using System.Diagnostics;
using BlockGame.Benchmarks.Comparison;

namespace BlockGame.Benchmarks.Harness;

/// <summary>Measure two immutable Git revisions sequentially in isolated worktrees.</summary>
public static class RevisionComparison
{
    internal delegate string Command(string executable, string directory, string? log, int timeoutSeconds, params string[] args);
    public static int Run(string[] args) => Run(args, Execute);
    internal static int Run(string[] args, Command execute)
    {
        try
        {
            string? baseline = null, candidate = null;
            string output = Path.GetFullPath(Path.Combine("artifacts", "revisions", $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"));
            string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            int timeoutSeconds = 1800;
            string Invoke(string executable, string directory, string? log, params string[] command) =>
                execute(executable, directory, log, timeoutSeconds, command);
            var cases = new List<string>();
            for (int i = 1; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for {args[i]}");
                switch (args[i])
                {
                    case "--baseline": baseline = args[i + 1]; break;
                    case "--candidate": candidate = args[i + 1]; break;
                    case "--case": cases.Add(args[i + 1]); break;
                    case "--output": output = Path.GetFullPath(args[i + 1]); break;
                    case "--dotnet": dotnet = args[i + 1]; break;
                    case "--timeout-seconds": timeoutSeconds = int.Parse(args[i + 1]); break;
                    default: throw new ArgumentException($"Unknown option {args[i]}");
                }
            }
            if (baseline == null || candidate == null) throw new ArgumentException("Use revisions --baseline <ref> --candidate <ref> [--case pattern] [--output directory] [--dotnet executable]");
            if (timeoutSeconds is < 1 or > 86400) throw new ArgumentException("Timeout must be 1..86400 seconds per command.");
            string root = Invoke("git", Environment.CurrentDirectory, null, "rev-parse", "--show-toplevel").Trim();
            // Resolve both before creating worktrees: branch names may move during a run.
            string Resolve(string revision) => Invoke("git", root, null, "rev-parse", "--verify", "--end-of-options", revision + "^{commit}").Trim();
            string before = Resolve(baseline), after = Resolve(candidate);
            if (Directory.Exists(output) || File.Exists(output)) throw new ArgumentException("Output must be a new directory.");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "revisions.txt"), $"baseline={before}\ncandidate={after}\n");
            var checkouts = new Dictionary<string, string>();
            try
            {
                foreach (var (name, revision) in new[] { ("baseline", before), ("candidate", after) })
                {
                    string checkout = Path.Combine(output, name + "-source");
                    Invoke("git", root, Path.Combine(output, name + "-checkout.log"), "worktree", "add", "--detach", checkout, revision);
                    checkouts.Add(name, checkout);
                    Console.WriteLine($"{name}: {revision}; testing full solution...");
                    Invoke(dotnet, checkout, Path.Combine(output, name + "-tests.log"), "test", "--solution", "Rendering.sln", "-c", "Release");
                    if (Invoke("git", checkout, null, "status", "--porcelain").Length != 0)
                        throw new InvalidOperationException($"{name} tests changed the source tree; measurement stopped.");
                }
                var codes = new List<int>();
                File.WriteAllText(Path.Combine(output, "order.txt"), "pair-1: baseline,candidate\npair-2: candidate,baseline\n");
                for (int pair = 1; pair <= 2; pair++)
                {
                    string pairOutput = Path.Combine(output, $"pair-{pair}");
                    Directory.CreateDirectory(pairOutput);
                    foreach (string name in pair == 1 ? new[] { "baseline", "candidate" } : new[] { "candidate", "baseline" })
                    {
                        var command = new List<string> { "run", "-c", "Release", "--project", "BlockGame.Benchmarks", "--", "--mode", "Measure", "--output", Path.Combine(pairOutput, name) };
                        foreach (string pattern in cases) command.AddRange(["--case", pattern]);
                        Console.WriteLine($"Pair {pair}: {name}; measuring...");
                        Invoke(dotnet, checkouts[name], Path.Combine(pairOutput, name + "-measure.log"), command.ToArray());
                    }
                    codes.Add(ComparisonCli.Run(["compare", Path.Combine(pairOutput, "baseline"), Path.Combine(pairOutput, "candidate"), Path.Combine(pairOutput, "comparison.json")]));
                }
                int code = codes.Contains(1) ? 1 : codes.Contains(2) ? 2 : codes.All(c => c == 0) ? 0 : 3;
                EvidenceJson.Write(Path.Combine(output, "comparison.json"), new { Baseline = before, Candidate = after, GateExitCode = code, PairExitCodes = codes });
                File.WriteAllText(Path.Combine(output, "comparison.md"), $"# Revision comparison\n\nGate exit code: {code}. Both pairs must pass; any regression fails. Samples are not pooled.\n\n[Pair 1](pair-1/comparison.md) · [Pair 2](pair-2/comparison.md)\n");
                Console.WriteLine($"Overall gate exit: {code}; reports: {output}");
                return code;
            }
            finally
            {
                var failures = new List<Exception>();
                foreach (var (name, checkout) in checkouts)
                {
                    try
                    {
                        string resolved = Path.GetFullPath(checkout);
                        if (!resolved.StartsWith(Path.GetFullPath(output) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Checkout is outside the run output directory.");
                        // No --force: preserve unexpected user changes and report cleanup failure.
                        Invoke("git", root, Path.Combine(output, name + "-cleanup.log"), "worktree", "remove", resolved);
                    }
                    catch (Exception ex) { failures.Add(ex); }
                }
                if (failures.Count > 0) throw new AggregateException("Worktree cleanup failed; see cleanup logs. Remaining worktrees were preserved.", failures);
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }

    internal static string Execute(string executable, string directory, string? log, int timeoutSeconds, params string[] args)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Cannot start {executable}");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        bool timedOut = !process.WaitForExit(checked(timeoutSeconds * 1000));
        if (timedOut)
        {
            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(10000)) throw new TimeoutException($"{executable} did not exit after termination.");
        }
        if (!Task.WaitAll([stdout, stderr], 10000)) throw new TimeoutException($"{executable} output pipes did not close.");
        if (log != null) File.WriteAllText(log, stdout.Result + stderr.Result);
        if (timedOut) throw new TimeoutException($"{executable} {args[0]} exceeded {timeoutSeconds} seconds; process tree terminated. See {log}");
        if (process.ExitCode != 0) throw new InvalidOperationException($"{executable} {args[0]} failed with exit {process.ExitCode}. {(log == null ? stderr.Result : $"See {log}")}");
        return stdout.Result;
    }
}
