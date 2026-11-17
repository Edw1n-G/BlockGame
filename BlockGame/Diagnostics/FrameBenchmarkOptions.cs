using System.Globalization;
using Basics.Player;
using Basics.Settings;

namespace Basics.Diagnostics;

public sealed record FrameBenchmarkOptions(int RenderDistance, double Duration, string Script, string Output, int Repeats, string World = "terrain", int[]? Sweep = null)
{
    public static FrameBenchmarkOptions Parse(string[] args)
    {
        int distance = 0, repeats = 3;
        bool explicitDistance = false;
        string world = "terrain";
        int[]? sweep = null;
        double duration = 30;
        string script = ScriptedMovementInput.ScriptName;
        string output = Path.GetFullPath(Path.Combine("artifacts", "frame-bench", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
        for (int i = 1; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length) throw new ArgumentException($"Missing value for {args[i]}");
            string value = args[i+1];
            switch (args[i])
            {
                case "--render-distance": distance = int.Parse(value, CultureInfo.InvariantCulture); explicitDistance = true; break;
                case "--duration": duration = double.Parse(value, CultureInfo.InvariantCulture); break;
                case "--script": script = value; break;
                case "--output": output = Path.GetFullPath(value); break;
                case "--repeats": repeats = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--world": world = value; break;
                case "--sweep":
                    sweep = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                    break;
                default: throw new ArgumentException($"Unknown benchmark option {args[i]}");
            }
        }
        if (explicitDistance && sweep != null) throw new ArgumentException("Use either --render-distance or --sweep.");
        if (!explicitDistance && sweep == null)
            throw new ArgumentException("Specify --sweep <distances> or --render-distance <distance>, e.g. --sweep 5,8,12.");
        if (sweep != null)
        {
            if (sweep.Length == 0 || sweep.Distinct().Count() != sweep.Length || sweep.Any(d => d is < GameSettings.MinimumRenderDistance or > 60))
                throw new ArgumentException("--sweep needs distinct render distances between 5 and 60, e.g. 8,12,16.");
            distance = sweep[0];
        }
        if (world is not ("terrain" or "checkerboard")) throw new ArgumentException("World must be terrain or checkerboard.");
        if (distance is < GameSettings.MinimumRenderDistance or > 60 || !double.IsFinite(duration) || duration is < 1 or > 600 || repeats is < 1 or > 20)
            throw new ArgumentException("Render distance: 5..60, duration: 1..600 seconds, repeats: 1..20.");
        if (script != ScriptedMovementInput.ScriptName) throw new ArgumentException($"Unknown script '{script}'. Available: fly-sweep");
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Output directory must be new or empty.");
        return new(distance, duration, script, output, repeats, world, sweep);
    }

    public GameSettings Settings() => new()
    {
        RenderDistance = RenderDistance, VerticalRenderDistance = RenderDistance,
        Worldtype = World == "checkerboard" ? "Ag@BBABA==" : new GameSettings().Worldtype,
        MapSize = Math.Max(100, (int)Math.Ceiling(Duration * 32 / 16) + RenderDistance + 2),
        Seed = 1, VSync = false, FrameRate = 1000, Msaa = 2, PlayerMoveSpeed = 32
    };
}
