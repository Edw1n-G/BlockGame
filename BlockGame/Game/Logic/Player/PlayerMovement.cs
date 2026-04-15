using System.Numerics;
using Basics.Game.Logic;
using Basics.Input;

namespace Basics.Game.Player;

public class PlayerMovement
{
    private readonly PlayerCharacter _player;
    private Vector2 _lastMousePosition;
    private Camera _activeCamera;

    public PlayerMovement(PlayerCharacter player)
    {
        _player = player;
        _activeCamera = player.Camera;
    }

    public void SetActiveCamera(Camera camera)
    {
        _activeCamera = camera;
        _lastMousePosition = default;
    }

    public void UsePlayerCamera()
    {
        _activeCamera = _player.Camera;
        _lastMousePosition = default;
    }

    public void MovementUpdate(double deltaTime)
    {
        Vector3 direction = Vector3.Zero;

        if (InputManager.IsActionPressed(Actions.Forward))
            direction.Z += 1.0f;
        if (InputManager.IsActionPressed(Actions.Backward))
            direction.Z -= 1.0f;

        if (InputManager.IsActionPressed(Actions.Left))
            direction.X -= 1.0f;
        if (InputManager.IsActionPressed(Actions.Right))
            direction.X += 1.0f;

        if (InputManager.IsActionPressed(Actions.Up))
            direction.Y += 1.0f;
        if (InputManager.IsActionPressed(Actions.Down))
            direction.Y -= 1.0f;

        if (direction == Vector3.Zero)
            return;

        direction = Vector3.Normalize(direction) * GameSettings.PlayerMoveSpeed * (float)deltaTime;
        
        //Nur neue Chunks wenn die Spieler Kamera und nicht die Debugkamera bewegt wird
        if (ReferenceEquals(_activeCamera, _player.Camera))
        {
            _player.Move(direction);
            return;
        }

        // Debugkamera bewegt sich frei, ohne Chunk-Streaming Event.
        MoveCamera(_activeCamera, direction);
    }

    public void LookUpdate(Vector2 mousePosition)
    {
        if (_lastMousePosition == default)
            _lastMousePosition = mousePosition;

        float deltaX = mousePosition.X - _lastMousePosition.X;
        float deltaY = mousePosition.Y - _lastMousePosition.Y;

        _lastMousePosition = mousePosition;

        float yaw = deltaX * GameSettings.MouseSensitivity;
        float pitch = deltaY * GameSettings.MouseSensitivity;

        _activeCamera.Rotate(yaw, pitch);
    }

    private static void MoveCamera(Camera camera, Vector3 direction)
    {
        Vector3 groundedFront = Vector3.Normalize(new Vector3(camera.Front.X, 0, camera.Front.Z));
        camera.Position += groundedFront * direction.Z;
        camera.Position += camera.Right * direction.X;
        camera.Position += camera.GlobalUp * direction.Y;
    }
}