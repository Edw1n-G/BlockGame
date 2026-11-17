using System.Globalization;
using Basics.Player;
using Basics.Settings;

namespace Basics.Diagnostics;

public sealed record FrameBenchmarkOptions(int RenderDistance, double Duration, string Script, string Output, int Repeats, string World = "terrain", bool Calibrate = false, int MaxRenderDistance = 60, int[]? Sweep = null)
{
    public static FrameBenchmarkOptions Parse(string[] args)
    {
        int distance = 12, repeats = 3, maximum = 60;
        bool explicitDistance = false, calibrate = args.Length > 1 && args[1] == "calibrate";
        string world = "terrain";
        string? baseline = null;
        int[]? sweep = null;
        bool workloadOverride = false;
        double duration = 30;
        string script = ScriptedMovementInput.ScriptName;
        string output = Path.GetFullPath(Path.Combine("artifacts", "frame-bench", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
        for (int i = calibrate ? 2 : 1; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length) throw new ArgumentException($"Missing value for {args[i]}");
            string value = args[i+1];
            switch (args[i])
            {
                case "--render-distance": distance = int.Parse(value, CultureInfo.InvariantCulture); explicitDistance = true; workloadOverride = true; break;
                case "--duration": workloadOverride = true; duration = double.Parse(value, CultureInfo.InvariantCulture); break;
                case "--script": workloadOverride = true; script = value; break;
                case "--output": output = Path.GetFullPath(value); break;
                case "--repeats": repeats = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--world": world = value; workloadOverride = true; break;
                case "--max-render-distance": maximum = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--baseline": baseline = value; break;
                case "--sweep":
                    sweep = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                    workloadOverride = true; break;
                default: throw new ArgumentException($"Unknown benchmark option {args[i]}");
            }
        }
        if (baseline != null)
        {
            if (workloadOverride || calibrate) throw new ArgumentException("--baseline cannot be combined with world, duration, script, distance or calibration overrides.");
            var saved = FrameBenchmarkCalibration.Read(baseline);
            distance = saved.RenderDistance; duration = saved.Duration; world = saved.World; script = saved.Script;
            explicitDistance = true;
        }
        else if (!explicitDistance && world == "checkerboard") distance = 2;
        if (sweep != null)
        {
            if (calibrate) throw new ArgumentException("--sweep cannot be combined with calibration.");
            if (sweep.Length == 0 || sweep.Distinct().Count() != sweep.Length || sweep.Any(d => d is < 1 or > 60))
                throw new ArgumentException("--sweep needs distinct render distances between 1 and 60, e.g. 8,12,16.");
            distance = sweep[0]; explicitDistance = true;
        }
        if (world is not ("terrain" or "checkerboard")) throw new ArgumentException("World must be terrain or checkerboard.");
        if (maximum is < 1 or > 60 || maximum < distance) throw new ArgumentException("Maximum render distance must be between the starting distance and 60.");
        if (distance is < 1 or > 60 || !double.IsFinite(duration) || duration is < 1 or > 600 || repeats is < 1 or > 20)
            throw new ArgumentException("Render distance: 1..60, duration: 1..600 seconds, repeats: 1..20.");
        if (script != ScriptedMovementInput.ScriptName) throw new ArgumentException($"Unknown script '{script}'. Available: fly-sweep");
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Output directory must be new or empty.");
        return new(distance, duration, script, output, repeats, world, calibrate || !explicitDistance, maximum, sweep);
    }

    public GameSettings Settings() => new()
    {
        RenderDistance = RenderDistance, VerticalRenderDistance = RenderDistance,
        Worldtype = World == "checkerboard" ? "Ag@BBABA==" : new GameSettings().Worldtype,
        MapSize = Math.Max(100, (int)Math.Ceiling(Duration * 32 / 16) + RenderDistance + 2),
        Seed = 1, VSync = false, FrameRate = 1000, Msaa = 2, PlayerMoveSpeed = 32
    };
}
