using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Basics.Player;
using Basics.Settings;
using Basics.Terrain.Chunks;
using Basics.Terrain.Selection;

namespace Basics.Diagnostics;

/// <summary>One cold process, one game entry. All methods run on the window thread.</summary>
public sealed class FrameBenchmarkRun
{
    public static FrameBenchmarkOptions? Options { get; set; }
    private readonly FrameBenchmarkOptions _options;
    private readonly FrameMetrics _metrics;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ChunkSelectionShape _shape;
    private readonly string _revision;
    private readonly string _graphics;
    private readonly string _workload;
    private double _start = -1, _lastUpdate, _lastFrame, _nextSample = 1;
    private int _stableFrames;
    private PipelineCounts _baseline;
    public ScriptedMovementInput Input { get; }
    public bool Finished { get; private set; }
    public bool Started => _start >= 0;
    public FrameBenchmarkRun(FrameBenchmarkOptions options, GameSettings settings, string graphics)
    {
        _options = options; _graphics = graphics;
        Input = new(options.Duration);
        _metrics = new((int)(options.Duration * 2000 + 100), (int)(options.Duration * ScriptedMovementInput.NominalUpdatesPerSecond * 4 + 100), (int)options.Duration + 3);
        _shape = ChunkSelectionShape.Create(settings.RenderDistance, settings.VerticalRenderDistance)!;
        _revision = ReadGit("rev-parse", "HEAD");
        if (ReadGit("status", "--porcelain").Length != 0) _revision += " (dirty)";
        _workload = FormattableString.Invariant($"{options.Script}/v{ScriptedMovementInput.Version}; duration={options.Duration}; seed={settings.Seed}; start={ScriptedMovementInput.StartPosition.X},{ScriptedMovementInput.StartPosition.Y},{ScriptedMovementInput.StartPosition.Z}; yaw={ScriptedMovementInput.StartYawDegrees}; pitch={ScriptedMovementInput.StartPitchDegrees}; speed={settings.PlayerMoveSpeed}; yawRate={ScriptedMovementInput.YawDegreesPerSecond}; rd={settings.RenderDistance}; vertical={settings.VerticalRenderDistance}; msaa={settings.Msaa}; vsync=false; cap=1000; updates={ScriptedMovementInput.NominalUpdatesPerSecond}; size=800x600; warmup=initial-streaming-settled-30-consecutive-under-50ms; worldName={options.World}; mapSize={settings.MapSize}; world={settings.Worldtype}");
    }
    private static string ReadGit(params string[] args)
    {
        try
        {
            var info = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = AppContext.BaseDirectory };
            foreach (string arg in args) info.ArgumentList.Add(arg);
            using var p = Process.Start(info)!;
            string output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return p.ExitCode == 0 ? output : "unknown";
        }
        catch { return "unknown"; }
    }

    internal ChunkSelectionRegion Region(Camera camera) => new(camera.GetChunkCoord(camera.Position), _shape);

    public void Update(PlayerMovement movement, Camera camera)
    {
        if (!Started || Finished) return;
        double elapsed = _clock.Elapsed.TotalSeconds - _start;
        // No catch-up after the deadline: a starved loop must not fabricate movement.
        if (elapsed >= _options.Duration) return;
        _metrics.Update(elapsed);
        double dt = elapsed - _lastUpdate;
        _lastUpdate = elapsed;
        Vector3 before = camera.Position;
        movement.MovementUpdate(dt);
        _metrics.Distance += Vector3.Distance(before, camera.Position);
    }

    public void EndFrame(double interval, double renderCpuMs, ChunkProvider provider, int resident, int drawn, bool requestorFailed, bool selectionSettled)
    {
        if (Finished) return;
        double now = _clock.Elapsed.TotalSeconds;
        if (!Started)
        {
            var queues = provider.JobScheduler!.QueueDepths;
            var counts = provider.Metrics.Snapshot();
            bool settled = selectionSettled && counts.Requested == counts.Generated && queues == (0, 0) &&
                provider.PendingMeshRequests.IsEmpty && provider.QueuedForMeshing.IsEmpty && provider.UploadQueue.IsEmpty;
            _stableFrames = settled && interval > 0 && interval < .05 ? _stableFrames + 1 : 0;
            if (_stableFrames >= 30)
            {
                _start = now; _baseline = provider.Metrics.Snapshot();
                _metrics.Sample(Sample(0, provider, resident, drawn));
            }
            else if (now >= 60) Finish(provider, resident, drawn, requestorFailed, "Warm-up timed out before initial streaming settled and 30 consecutive frames under 50 ms");
            return;
        }
        double elapsed = now - _start;
        _metrics.Frame(new(elapsed, (elapsed - _lastFrame) * 1000, renderCpuMs));
        _lastFrame = elapsed;
        if (elapsed >= _nextSample)
        {
            _metrics.Sample(Sample(elapsed, provider, resident, drawn));
            _nextSample = Math.Floor(elapsed) + 1;
        }
        if (elapsed >= _options.Duration) Finish(provider, resident, drawn, requestorFailed);
    }

    private StreamingSample Sample(double elapsed, ChunkProvider provider, int resident, int drawn)
    {
        var queues = provider.JobScheduler!.QueueDepths;
        return new(elapsed, provider.Metrics.Snapshot() - _baseline, queues.Generation,
            queues.Mesh + provider.PendingMeshRequests.Count, provider.UploadQueue.Count, _shape.Count, resident, drawn);
    }

    public void Abort(ChunkProvider provider) { if (!Finished) Finish(provider, 0, 0, false, "Run interrupted before completion"); }

    private void Finish(ChunkProvider provider, int resident, int drawn, bool requestorFailed, string? failure = null)
    {
        Finished = true;
        double elapsed = Started ? _clock.Elapsed.TotalSeconds - _start : 0;
        if (Started) _metrics.Sample(Sample(elapsed, provider, resident, drawn));
        var reasons = new List<string>();
        var degraded = new List<string>();
        if (failure != null) reasons.Add(failure);
        if (_metrics.Overflow) reasons.Add("Preallocated recording buffer overflow");
        if (_metrics.FrameCount < 30) reasons.Add("Fewer than 30 measured frames");
        if (_metrics.UpdateCount < _options.Duration * ScriptedMovementInput.NominalUpdatesPerSecond * .9) degraded.Add("Degraded: fewer than 90% of expected 60 Hz updates");
        double expected = provider.Settings.PlayerMoveSpeed * _options.Duration;
        if (Math.Abs(_metrics.Distance - expected) > expected * .05) reasons.Add("Path distance differs from speed × duration by over 5%");
        if (elapsed > _options.Duration * 1.05) reasons.Add("Wall-clock duration overshot by over 5%");
        if (requestorFailed || provider.JobScheduler!.Failures.Count > 0) reasons.Add("Chunk pipeline failure");
        var frames = _metrics.Frames.ToArray();
        var streaming = _metrics.Streaming.ToArray();
        if (!streaming.Any(s => s.Drawn > 0)) reasons.Add("No target terrain was drawn; empty-scene timing is not rendering performance evidence");
        double maxGap = _metrics.MaxUpdateGapMs(elapsed);
        var summary = new FrameBenchmarkSummary
        {
            Workload = _workload, Revision = _revision,
            RenderDistance = _options.RenderDistance, World = _options.World,
            Environment = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}; {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}; CPUs={System.Environment.ProcessorCount}; runtime={System.Environment.Version}; CPU={System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")}",
            Graphics = _graphics, DurationSeconds = _options.Duration, MeasuredSeconds = elapsed,
            WarmupSeconds = Started ? _start : _clock.Elapsed.TotalSeconds,
            Frames = frames.Length, Updates = _metrics.UpdateCount,
            FrameP50Ms = FrameMetrics.Percentile(frames.Select(f => f.IntervalMs), .5),
            FrameP95Ms = FrameMetrics.Percentile(frames.Select(f => f.IntervalMs), .95),
            FrameP99Ms = FrameMetrics.Percentile(frames.Select(f => f.IntervalMs), .99),
            FrameMaxMs = frames.Length == 0 ? 0 : frames.Max(f => f.IntervalMs),
            RenderCpuP95Ms = FrameMetrics.Percentile(frames.Select(f => f.RenderCpuMs), .95),
            MaxUpdateGapMs = maxGap, LongestUpdateHitchMs = Math.Max(0, maxGap - 1000d / ScriptedMovementInput.NominalUpdatesPerSecond),
            Distance = _metrics.Distance, ExpectedDistance = expected,
            Counts = Started ? provider.Metrics.Snapshot() - _baseline : default,
            MeanDrawnFraction = streaming.Length == 0 ? 0 : streaming.Average(s => s.DrawnFraction),
            MeanResidentFraction = streaming.Length == 0 ? 0 : streaming.Average(s => s.ResidentFraction),
            InvalidReasons = reasons.ToArray(), DegradedReasons = degraded.ToArray()
        };
        Directory.CreateDirectory(_options.Output);
        File.WriteAllText(Path.Combine(_options.Output, "summary.json"), JsonSerializer.Serialize(summary, FrameBenchmarkReport.JsonOptions));
        using (var csv = new StreamWriter(Path.Combine(_options.Output, "frames.csv")))
        {
            csv.WriteLine("seconds,interval_ms,render_cpu_ms");
            foreach (var f in frames) csv.WriteLine(FormattableString.Invariant($"{f.Seconds:R},{f.IntervalMs:R},{f.RenderCpuMs:R}"));
        }
        using (var csv = new StreamWriter(Path.Combine(_options.Output, "updates.csv")))
        {
            csv.WriteLine("seconds");
            foreach (double t in _metrics.Updates) csv.WriteLine(t.ToString("R", CultureInfo.InvariantCulture));
        }
        using (var csv = new StreamWriter(Path.Combine(_options.Output, "streaming.csv")))
        {
            csv.WriteLine("seconds,requested,generated,meshed,uploaded,generation_queue,mesh_queue,upload_queue,target,resident,drawn,resident_fraction,drawn_fraction,selection_commits,selection_rollbacks");
            foreach (var s in streaming) csv.WriteLine(FormattableString.Invariant($"{s.Seconds:R},{s.Counts.Requested},{s.Counts.Generated},{s.Counts.Meshed},{s.Counts.Uploaded},{s.GenerationQueue},{s.MeshQueue},{s.UploadQueue},{s.Target},{s.Resident},{s.Drawn},{s.ResidentFraction:R},{s.DrawnFraction:R},{s.Counts.SelectionCommits},{s.Counts.SelectionRollbacks}"));
        }
        File.WriteAllText(Path.Combine(_options.Output, "summary.md"),
            $"# Frame benchmark\n\nVerdict: **{(!summary.Valid ? "INVALID" : degraded.Count > 0 ? "DEGRADED" : "VALID")}**\n\n{string.Join("\n", reasons.Concat(degraded).Select(r => "- " + r))}\n\n" +
            $"Revision: {summary.Revision}\n\n{summary.Environment}\n\nGPU: {summary.Graphics}\n\nSettings: `{summary.Workload}`\n\n" +
            FormattableString.Invariant($"Warm-up: {summary.WarmupSeconds:F2}s; measured: {elapsed:F2}s; frames: {summary.Frames}; updates: {summary.Updates}.\n\nPath: {_metrics.Distance:F2} / {expected:F2} blocks. Longest update hitch above 16.67 ms: {summary.LongestUpdateHitchMs:F2} ms. Max frame: {summary.FrameMaxMs:F2} ms; CPU render p95: {summary.RenderCpuP95Ms:F2} ms.\n\n") +
            FrameBenchmarkReport.DescribeRepeats([summary]) + "\nDrawn fraction includes frustum culling and excludes empty chunks. Resident fraction includes empty uploaded meshes. Neither is a GPU timer. Queue samples exclude running jobs.\n");
        Console.WriteLine($"Frame benchmark: {_options.Output} ({(!summary.Valid ? "invalid" : degraded.Count > 0 ? "degraded" : "valid")})");
        if (!summary.Valid) System.Environment.ExitCode = 2;
    }
}
