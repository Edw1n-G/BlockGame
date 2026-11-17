using System.Diagnostics;
using BlockGame.Benchmarks.Comparison;

namespace BlockGame.Benchmarks.Harness;

/// <summary>Measure two immutable Git revisions sequentially in isolated worktrees.</summary>
public static class RevisionComparison
{
    internal delegate string Command(string executable, string directory, string? log, params string[] args);
    public static int Run(string[] args) => Run(args, Execute);
    internal static int Run(string[] args, Command execute)
    {
        try
        {
            string? baseline = null, candidate = null;
            string output = Path.GetFullPath(Path.Combine("artifacts", "revisions", $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"));
            string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
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
                    default: throw new ArgumentException($"Unknown option {args[i]}");
                }
            }
            if (baseline == null || candidate == null) throw new ArgumentException("Use revisions --baseline <ref> --candidate <ref> [--case pattern] [--output directory] [--dotnet executable]");
            string root = execute("git", Environment.CurrentDirectory, null, "rev-parse", "--show-toplevel").Trim();
            // Resolve both before creating worktrees: branch names may move during a run.
            string Resolve(string revision) => execute("git", root, null, "rev-parse", "--verify", "--end-of-options", revision + "^{commit}").Trim();
            string before = Resolve(baseline), after = Resolve(candidate);
            if (Directory.Exists(output) || File.Exists(output)) throw new ArgumentException("Output must be a new directory.");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "revisions.txt"), $"baseline={before}\ncandidate={after}\n");
            foreach (var (name, revision) in new[] { ("baseline", before), ("candidate", after) })
            {
                string checkout = Path.Combine(output, name + "-source");
                execute("git", root, Path.Combine(output, name + "-checkout.log"), "worktree", "add", "--detach", checkout, revision);
                Console.WriteLine($"{name}: {revision}; testing full solution...");
                execute(dotnet, checkout, Path.Combine(output, name + "-tests.log"), "test", "--solution", "Rendering.sln", "-c", "Release");
                if (execute("git", checkout, null, "status", "--porcelain").Length != 0)
                    throw new InvalidOperationException($"{name} tests changed the source tree; measurement stopped.");
                var command = new List<string> { "run", "-c", "Release", "--project", "BlockGame.Benchmarks", "--", "--mode", "Measure", "--output", Path.Combine(output, name) };
                foreach (string pattern in cases) command.AddRange(["--case", pattern]);
                Console.WriteLine($"{name}: measuring...");
                execute(dotnet, checkout, Path.Combine(output, name + "-measure.log"), command.ToArray());
            }
            return ComparisonCli.Run(["compare", Path.Combine(output, "baseline"), Path.Combine(output, "candidate"), Path.Combine(output, "comparison.json")]);
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }

    private static string Execute(string executable, string directory, string? log, params string[] args)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Cannot start {executable}");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdout, stderr);
        if (log != null) File.WriteAllText(log, stdout.Result + stderr.Result);
        if (process.ExitCode != 0) throw new InvalidOperationException($"{executable} {args[0]} failed with exit {process.ExitCode}. {(log == null ? stderr.Result : $"See {log}")}");
        return stdout.Result;
    }
}
