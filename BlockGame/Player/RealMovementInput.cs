using Basics.Input;
using Basics.Settings;
using System.Numerics;

namespace Basics.Player;

public sealed class RealMovementInput : IMovementInput, IDisposable
{
    private readonly InputManager _input;
    private readonly GameSettings _settings;
    private Vector2? _last;
    private Vector2 _look;
    public RealMovementInput(InputManager input, GameSettings settings)
    {
        _input = input; _settings = settings;
        input.MouseMoved += LookUpdate;
    }
    public void LookUpdate(Vector2 position)
    {
        if (_last is { } last) _look += (position - last) * _settings.MouseSensitivity;
        _last = position;
    }
    public MovementSample Read(double deltaSeconds)
    {
        float Axis(Actions positive, Actions negative) =>
            (_input.IsActionPressed(positive) ? 1 : 0) - (_input.IsActionPressed(negative) ? 1 : 0);
        var result = new MovementSample(new Vector3(Axis(Actions.Right, Actions.Left),
            Axis(Actions.Up, Actions.Down), Axis(Actions.Forward, Actions.Backward)), _look);
        _look = default;
        return result;
    }
    public void ResetLook() { _last = null; _look = default; }
    public void Dispose() => _input.MouseMoved -= LookUpdate;
}
