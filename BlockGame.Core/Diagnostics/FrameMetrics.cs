namespace Basics.Diagnostics;

public readonly record struct FrameSample(double Seconds, double IntervalMs, double RenderCpuMs);
public readonly record struct StreamingSample(double Seconds, PipelineCounts Counts, int GenerationQueue,
    int MeshQueue, int UploadQueue, int Target, int Resident, int Drawn)
{
    public double DrawnFraction => Target == 0 ? 0 : (double)Drawn / Target;
    public double ResidentFraction => Target == 0 ? 0 : (double)Resident / Target;
}

/// <summary>Single main-thread writer; storage allocated before warm-up, never grown.</summary>
public sealed class FrameMetrics
{
    private readonly FrameSample[] _frames;
    private readonly double[] _updates;
    private readonly StreamingSample[] _streaming;
    public int FrameCount { get; private set; }
    public int UpdateCount { get; private set; }
    public int StreamingCount { get; private set; }
    public bool Overflow { get; private set; }
    public double Distance { get; set; }
    public ReadOnlySpan<FrameSample> Frames => _frames.AsSpan(0, FrameCount);
    public ReadOnlySpan<double> Updates => _updates.AsSpan(0, UpdateCount);
    public ReadOnlySpan<StreamingSample> Streaming => _streaming.AsSpan(0, StreamingCount);
    public FrameMetrics(int frameCapacity, int updateCapacity, int sampleCapacity)
    {
        _frames = new FrameSample[frameCapacity]; _updates = new double[updateCapacity];
        _streaming = new StreamingSample[sampleCapacity];
    }
    public void Frame(FrameSample sample)
    {
        if (FrameCount == _frames.Length) { Overflow = true; return; }
        _frames[FrameCount++] = sample;
    }
    public void Update(double seconds)
    {
        if (UpdateCount == _updates.Length) { Overflow = true; return; }
        _updates[UpdateCount++] = seconds;
    }
    public void Sample(StreamingSample sample)
    {
        if (StreamingCount == _streaming.Length) { Overflow = true; return; }
        _streaming[StreamingCount++] = sample;
    }
    public double MaxUpdateGapMs(double elapsed)
    {
        double previous = 0, maximum = 0;
        foreach (double time in Updates) { maximum = Math.Max(maximum, time - previous); previous = time; }
        return Math.Max(maximum, elapsed - previous) * 1000;
    }
    public static double Percentile(IEnumerable<double> values, double percentile)
    {
        double[] sorted = values.Order().ToArray();
        if (sorted.Length == 0) return 0;
        double index = Math.Clamp(percentile, 0, 1) * (sorted.Length - 1);
        int lower = (int)index;
        return sorted[lower] + (sorted[Math.Min(lower+1, sorted.Length-1)] - sorted[lower]) * (index-lower);
    }
    public static double RelativeSpread(IEnumerable<double> values)
    {
        double[] samples = values.ToArray();
        double median = Percentile(samples, .5);
        if (samples.Length == 0) return 0;
        double range = samples.Max() - samples.Min();
        return median == 0 ? (range == 0 ? 0 : double.PositiveInfinity) : range / median;
    }
}
