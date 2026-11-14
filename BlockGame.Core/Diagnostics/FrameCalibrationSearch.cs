namespace Basics.Diagnostics;

/// <summary>Increase load, then bisect the first crossing using whole chunk radii.</summary>
public sealed class FrameCalibrationSearch(int maximumDistance)
{
    public const double TargetMs = 16;
    public const double Tolerance = .10;
    private readonly Dictionary<int, double> _measured = new();
    public static bool OnTarget(double frameMs) => double.IsFinite(frameMs) &&
        Math.Abs(frameMs - TargetMs) <= TargetMs * Tolerance;

    public int? Record(int distance, double frameMs)
    {
        if (!double.IsFinite(frameMs) || frameMs <= 0) throw new ArgumentOutOfRangeException(nameof(frameMs));
        _measured[distance] = frameMs;
        if (OnTarget(frameMs)) return null;
        var below = _measured.Where(p => p.Value < TargetMs).Select(p => p.Key).ToArray();
        var above = _measured.Where(p => p.Value >= TargetMs).Select(p => p.Key).ToArray();
        if (above.Length == 0)
            return distance >= maximumDistance ? null : Math.Min(maximumDistance, distance + Math.Max(2, distance / 4));
        if (below.Length == 0) return null; // Starting radius is already too slow; never silently lower it.
        int low = below.Max(), high = above.Min();
        if (high - low <= 1) return null;
        int next = (low + high) / 2;
        return _measured.ContainsKey(next) ? null : next;
    }

    public int ClosestDistance => _measured.MinBy(p => Math.Abs(p.Value - TargetMs)).Key;
}
