// Stand-in executable for orchestration failures. Never referenced by engine/tests/benchmarks.
if (args[0] is "restore" or "build") return 0;
if (args[0].Contains("BlockGame.Tests"))
{
    string mode = Environment.GetEnvironmentVariable("EDWINCRAFT_SCRIPT_TEST_RESULT");
    Console.WriteLine(mode switch {
        "zero" => "Total: 0, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0,",
        "skip" => "Total: 1, Errors: 0, Failed: 0, Skipped: 1, Not Run: 0,",
        _ => "Total: 1, Errors: 0, Failed: 1, Skipped: 0, Not Run: 0,"
    });
    return 0; // Deliberately successful process with invalid test summary.
}
if (args[0] == "spawn")
{
    var p = System.Diagnostics.Process.Start(Environment.ProcessPath!, "sleep");
    File.WriteAllText(args[1], p!.Id.ToString());
    Thread.Sleep(60_000); return 0;
}
if (args[0] == "sleep") { Thread.Sleep(60_000); return 0; }
if (args[0] == "lock")
{
    using var mutex = new Mutex(false, "Local\\EdwinCraft.CpuBenchmarks.v1");
    mutex.WaitOne(); File.WriteAllText(args[1], "locked");
    Thread.Sleep(60_000); return 0;
}
if (args[0] == "fail") return 7;
File.WriteAllText(Environment.GetEnvironmentVariable("EDWINCRAFT_SCRIPT_TEST_MARKER")!, "benchmark launched");
return 0;
