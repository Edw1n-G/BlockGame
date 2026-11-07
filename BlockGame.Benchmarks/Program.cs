using BlockGame.Benchmarks.Comparison;
using BlockGame.Benchmarks.Harness;

namespace BlockGame.Benchmarks;

public static class Program
{
    public static int Main(string[] args) =>
        args.ElementAtOrDefault(0) is "compare" or "pack" or "verify" or "validate-controls"
            ? ComparisonCli.Run(args)
            : HarnessRunner.Run(args);
}
