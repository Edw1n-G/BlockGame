using System.Numerics;

namespace Basics.Player;

public readonly record struct MovementSample(Vector3 Direction, Vector2 LookDegrees, float MovementFraction = 1);

public interface IMovementInput
{
    MovementSample Read(double deltaSeconds);
    void ResetLook();
}

/// <summary>Forward flight, then forward flight with a 12 degree/second yaw sweep.</summary>
public sealed class ScriptedMovementInput(double duration) : IMovementInput
{
    public const string ScriptName = "fly-sweep";
    public const int Version = 2;
    public const double YawDegreesPerSecond = 12;
    /// <summary>Single source for the benchmark start pose and nominal update rate (also used by the workload label).</summary>
    public static readonly Vector3 StartPosition = new(0, 150, 0);
    public const float StartYawDegrees = -90;
    public const float StartPitchDegrees = -45;
    public const int NominalUpdatesPerSecond = 60;
    public double Elapsed { get; private set; }
    public MovementSample Read(double deltaSeconds)
    {
        double end = Math.Min(duration, Elapsed + Math.Max(0, deltaSeconds));
        double sweep = Math.Max(0, end - duration / 2) - Math.Max(0, Elapsed - duration / 2);
        var sample = new MovementSample(end > Elapsed ? Vector3.UnitZ : Vector3.Zero,
            new Vector2((float)(sweep * YawDegreesPerSecond), 0),
            deltaSeconds > 0 ? (float)((end - Elapsed) / deltaSeconds) : 0);
        Elapsed = end;
        return sample;
    }
    public void ResetLook() { }
}
