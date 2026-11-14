using Basics.Diagnostics;
using Basics.Input;
using Basics.Player;
using Basics.Settings;
using System.Numerics;

namespace BlockGame.Tests;

public class FrameBenchmarkTests
{
    [Fact]
    public void CalibrationIncreasesLoadThenRefinesTheCrossing()
    {
        var search = new FrameCalibrationSearch(30);
        Assert.Equal(15, search.Record(12, 2));
        Assert.Equal(18, search.Record(15, 8));
        Assert.Equal(16, search.Record(18, 24));
        Assert.Null(search.Record(16, 16.2));
        Assert.Equal(16, search.ClosestDistance);
        Assert.False(FrameCalibrationSearch.OnTarget(1));
        Assert.True(FrameCalibrationSearch.OnTarget(16));
    }

    [Fact]
    public void CalibrationDoesNotInventSuccessAtBoundsOrOvershoot()
    {
        Assert.Null(new FrameCalibrationSearch(12).Record(12, 3));
        Assert.Null(new FrameCalibrationSearch(30).Record(12, 30));
        Assert.False(FrameCalibrationSearch.OnTarget(double.NaN));
    }

    [Fact]
    public void WorldsHaveSeparateCalibrationDefaultsAndExactCheckerboardGraph()
    {
        var terrain = FrameBenchmarkOptions.Parse(["--benchmark"]);
        var checker = FrameBenchmarkOptions.Parse(["--benchmark", "--world", "checkerboard"]);
        Assert.True(terrain.Calibrate);
        Assert.True(checker.Calibrate);
        Assert.Equal(12, terrain.RenderDistance);
        Assert.Equal(2, checker.RenderDistance);
        Assert.Equal("Ag@BBABA==", checker.Settings().Worldtype);
        Assert.False(FrameBenchmarkOptions.Parse(["--benchmark", "--render-distance", "12"]).Calibrate);
        Assert.True(FrameBenchmarkOptions.Parse(["--benchmark", "calibrate", "--render-distance", "12"]).Calibrate);
    }

    [Fact]
    public void BaselineReplayFreezesWorldDurationAndDistance()
    {
        string path = Path.GetTempFileName();
        try
        {
            var baseline = new CalibrationResult(2, true, 8, 20, "checkerboard", "fly-sweep", 16, 16.1, "ok", [], "Ag@BBABA==");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(baseline));
            var replay = FrameBenchmarkOptions.Parse(["--benchmark", "--baseline", path]);
            Assert.False(replay.Calibrate);
            Assert.Equal(8, replay.RenderDistance);
            Assert.Equal(20, replay.Duration);
            Assert.Equal("checkerboard", replay.World);
            Assert.Throws<ArgumentException>(() => FrameBenchmarkOptions.Parse(["--benchmark", "--baseline", path, "--world", "terrain"]));
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(baseline with { WorldCode = "old-graph" }));
            Assert.Throws<ArgumentException>(() => FrameBenchmarkCalibration.Read(path));
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(baseline with { TargetReached = false }));
            Assert.Throws<ArgumentException>(() => FrameBenchmarkCalibration.Read(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void DownwardLookActuallyPointsFortyFiveDegreesBelowHorizon()
    {
        var camera = new Camera(new(0,150,0));
        camera.Rotate(0,45);
        Assert.Equal(-45, camera.Pitch);
        Assert.Equal(-Math.Sqrt(.5), camera.Front.Y, 6);
        Assert.Equal(150, camera.Position.Y);
    }

    [Fact]
    public void CheckerboardGraphProducesBothSolidAndAirDensities()
    {
        using var noise = new Basics.Terrain.Generation.Noise.NoiseCalculator("Ag@BBABA==", 1);
        var values = noise.GetNoiseValues(0,128,0,64,64,64);
        Assert.All(values, value => Assert.True(float.IsFinite(value)));
        Assert.Contains(values, value => value > 0);
        Assert.Contains(values, value => value <= 0);
    }

    [Fact]
    public void ScriptIntegratesYawAcrossPhaseBoundaryAndStops()
    {
        var script = new ScriptedMovementInput(10);
        Assert.Equal(new MovementSample(Vector3.UnitZ, Vector2.Zero), script.Read(4));
        Assert.Equal(24f, script.Read(3).LookDegrees.X);
        Assert.Equal(36f, script.Read(10).LookDegrees.X);
        Assert.Equal(Vector3.Zero, script.Read(1).Direction);
        Assert.Equal(10, script.Elapsed);
    }

    [Fact]
    public void ScriptYawIsIndependentOfUpdateFrequency()
    {
        float Integrate(double dt)
        {
            var script = new ScriptedMovementInput(30);
            float yaw = 0;
            for (int i = 0; i < (int)(30/dt); i++) yaw += script.Read(dt).LookDegrees.X;
            return yaw;
        }
        Assert.InRange(Integrate(.01), 179.99f, 180.01f);
        Assert.InRange(Integrate(.1), 179.99f, 180.01f);
    }

    [Fact]
    public void RealMouseInputAccumulatesAndResetAvoidsCameraJump()
    {
        using var input = new RealMovementInput(new InputManager(), new GameSettings { MouseSensitivity = .5f });
        input.LookUpdate(Vector2.Zero);
        input.LookUpdate(new(2,4));
        input.LookUpdate(new(4,6));
        Assert.Equal(new Vector2(2,3), input.Read(.1).LookDegrees);
        Assert.Equal(Vector2.Zero, input.Read(.1).LookDegrees);
        input.ResetLook();
        input.LookUpdate(new(200,300));
        Assert.Equal(Vector2.Zero, input.Read(.1).LookDegrees);
    }

    [Fact]
    public void MetricsIncludeTrailingUpdateStarvationAndDetectOverflow()
    {
        var metrics = new FrameMetrics(1,2,1);
        metrics.Update(.01); metrics.Update(.02);
        Assert.Equal(980, metrics.MaxUpdateGapMs(1), 6);
        metrics.Frame(new(1, 10, 2)); metrics.Frame(new(2,20,3));
        metrics.Update(.03);
        metrics.Sample(new()); metrics.Sample(new());
        Assert.True(metrics.Overflow);
        Assert.Equal(1, metrics.FrameCount);
        Assert.Equal(2, metrics.UpdateCount);
        Assert.Equal(1, metrics.StreamingCount);
    }

    [Fact]
    public void PercentilesAndSpreadUseInterpolatedMedian()
    {
        Assert.Equal(2.5, FrameMetrics.Percentile([4,1,3,2], .5));
        Assert.Equal(3.85, FrameMetrics.Percentile([4,1,3,2], .95), 8);
        Assert.Equal(.1, FrameMetrics.RelativeSpread([95,100,105]), 8);
        Assert.Equal(0, FrameMetrics.Percentile([], .99));
        Assert.Equal(double.PositiveInfinity, FrameMetrics.RelativeSpread([0,0,1]));
    }

    [Fact]
    public void RecorderDoesNotAllocateWhileRecording()
    {
        var metrics = new FrameMetrics(1001,1001,1001);
        metrics.Frame(default); metrics.Update(0); metrics.Sample(default);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { metrics.Frame(new(i,1,1)); metrics.Update(i); metrics.Sample(default); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static FrameBenchmarkSummary Summary() => new()
    {
        Workload = "test-v1", Revision = "a", Environment = "test", Graphics = "test",
        DurationSeconds = 10, MeasuredSeconds = 10, Updates = 600, FrameP50Ms = 10,
        Counts = new(100,100,100,100), InvalidReasons = []
    };

    [Fact]
    public void ComparisonFlagsDifferentWorkAndStarvedUpdates()
    {
        var baseline = Summary();
        var candidate = baseline with { Updates = 300, Counts = new(100,20,20,20) };
        var warnings = FrameBenchmarkReport.CompareWarnings(baseline, candidate);
        Assert.Contains(warnings, w => w.StartsWith("DEGRADED"));
        Assert.Contains(warnings, w => w.StartsWith("WORK DIFFERS"));
        Assert.Contains(FrameBenchmarkReport.CompareWarnings(baseline, candidate with { Workload = "different" }), w => w.StartsWith("INCOMPATIBLE"));
    }

    [Fact]
    public void RepeatsFlagNoiseAndComparisonHandlesZeroBase()
    {
        var a = Summary();
        Assert.Contains("NOISY", FrameBenchmarkReport.DescribeRepeats([a, a with { FrameP50Ms = 20 }, a]));
        Assert.Contains("n/a (zero baseline)", FrameBenchmarkReport.Compare([a], [a]));
    }

    [Theory]
    [InlineData("--duration", "NaN")]
    [InlineData("--duration", "0")]
    [InlineData("--render-distance", "61")]
    [InlineData("--script", "unknown")]
    [InlineData("--repeats", "0")]
    [InlineData("--typo", "1")]
    public void LaunchOptionsRejectInvalidInputs(string key, string value) =>
        Assert.ThrowsAny<ArgumentException>(() => FrameBenchmarkOptions.Parse(["--benchmark",key,value]));

    [Fact]
    public void SweepParsesDistinctFixedDistancesAndRejectsConflicts()
    {
        var sweep = FrameBenchmarkOptions.Parse(["--benchmark", "--sweep", "8, 12,16"]);
        Assert.Equal(new[] { 8, 12, 16 }, sweep.Sweep);
        Assert.False(sweep.Calibrate);
        Assert.Equal(8, sweep.RenderDistance);
        Assert.Throws<ArgumentException>(() => FrameBenchmarkOptions.Parse(["--benchmark", "--sweep", "8,8"]));
        Assert.Throws<ArgumentException>(() => FrameBenchmarkOptions.Parse(["--benchmark", "--sweep", "0,12"]));
        Assert.Throws<ArgumentException>(() => FrameBenchmarkOptions.Parse(["--benchmark", "--sweep", "61"]));
        Assert.Throws<ArgumentException>(() => FrameBenchmarkOptions.Parse(["--benchmark", "calibrate", "--sweep", "8"]));
    }

    [Fact]
    public void SweepReportListsEachDistanceAndFlagsProblems()
    {
        var good = Summary() with { FrameP50Ms = 10 };
        var bad = Summary() with { FrameP50Ms = 20, InvalidReasons = ["x"], DegradedReasons = ["y"] };
        string text = FrameBenchmarkReport.DescribeSweep(new[] { (8, new[] { good, good, good }), (16, new[] { good, bad }) });
        Assert.Contains("| 8 | 3/3 |", text);
        Assert.Contains("| 16 | 1/2 |", text);
        Assert.Contains("INVALID, DEGRADED, NOISY", text);
    }

    [Fact]
    public void SweepFolderCannotBeComparedAsOneRun()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "sweep.json"), "[]");
            Assert.Throws<ArgumentException>(() => FrameBenchmarkReport.ReadRuns(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void PipelineCountersRetainConcurrentWork()
    {
        var metrics = new ChunkPipelineMetrics();
        Parallel.For(0, 10000, _ => { metrics.Requested(); metrics.Generated(); metrics.Meshed(); metrics.Uploaded(); });
        Assert.Equal(new PipelineCounts(10000,10000,10000,10000), metrics.Snapshot());
    }

    [Fact]
    public void ScriptClipsMovementAtEndOfDuration()
    {
        var script = new ScriptedMovementInput(10);
        script.Read(9);
        Assert.Equal(.25f, script.Read(4).MovementFraction);
        Assert.Equal(0f, script.Read(1).MovementFraction);
    }

    [Fact]
    public void SchedulerQueueCountersResetWhenWaitingJobsAreDiscarded()
    {
        var scheduler = new Basics.Jobs.JobScheduler();
        using var provider = new Basics.Terrain.Chunks.ChunkProvider(Basics.Terrain.Chunks.NullChunkGpuMeshFactory.Instance);
        scheduler.EnqueueLow(new Basics.Jobs.ChunkLoadOrGenerateJob(new(0,0,0,0), 1));
        scheduler.EnqueueHigh(new Basics.Jobs.MeshGenerateJob(new(new(0,0,0,0), 1), provider));
        Assert.Equal((1,1), scheduler.QueueDepths);
        Assert.True(scheduler.Stop());
        Assert.Equal((0,0), scheduler.QueueDepths);
    }
}
