using System.Numerics;
using Basics.Game.Input;
using Basics.Game.Logic;
using Basics.Game.Logic.Player;
using Silk.NET.Input;

namespace Basics.Game.Player;

/// <summary>
/// Verwaltet die Spielerbewegung basierend auf Eingabe und Kamera.
/// </summary>
public class PlayerMovement
{
    private readonly PlayerCharacter _player;
    private readonly InputManager _inputManager;
    private readonly GameSettings _settings;
    private Vector2 _lastMousePosition;
    private Camera _activeCamera;

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="PlayerMovement"/>.
    /// </summary>
    /// <param name="player">Der zu steuernde <see cref="PlayerCharacter"/>.</param>
    /// <param name="inputManager">Der <see cref="InputManager"/> zur Verarbeitung der Eingaben.</param>
    /// <param name="settings">Die optionalen <see cref="GameSettings"/> für Bewegungsparameter.</param>
    public PlayerMovement(PlayerCharacter player,InputManager inputManager, GameSettings? settings = null)
    {
        _player = player;
        
        // Mausbewegungen verfolgen
        _inputManager = inputManager;
        _inputManager.MouseMoved += LookUpdate;
        
        _settings = settings ?? EngineContext.Current?.Settings ?? GameSettings.Current;
        _activeCamera = player.Camera;
    }

    /// <summary>
    /// Legt die aktive Kamera für die Bewegung fest.
    /// </summary>
    /// <param name="camera">Die neue aktive <see cref="Camera"/>.</param>
    public void SetActiveCamera(Camera camera)
    {
        _activeCamera = camera;
        _lastMousePosition = default;
    }

    /// <summary>
    /// Setzt die Spielerkamera als aktive Kamera zurück.
    /// </summary>
    public void UsePlayerCamera()
    {
        _activeCamera = _player.Camera;
        _lastMousePosition = default;
    }

    /// <summary>
    /// Führt die Bewegungsaktualisierung basierend auf Tastatureingabe aus.
    /// </summary>
    /// <param name="deltaTime">Die Zeit in Sekunden seit dem letzten Frame.</param>
    public void MovementUpdate(double deltaTime)
    {
        Vector3 direction = Vector3.Zero;

        if (_inputManager.IsActionPressed(Actions.Forward))
            direction.Z += 1.0f;
        if (_inputManager.IsActionPressed(Actions.Backward))
            direction.Z -= 1.0f;

        if (_inputManager.IsActionPressed(Actions.Left))
            direction.X -= 1.0f;
        if (_inputManager.IsActionPressed(Actions.Right))
            direction.X += 1.0f;

        if (_inputManager.IsActionPressed(Actions.Up))
            direction.Y += 1.0f;
        if (_inputManager.IsActionPressed(Actions.Down))
            direction.Y -= 1.0f;

        if (direction == Vector3.Zero)
            return;

        direction = Vector3.Normalize(direction) * _settings.PlayerMoveSpeed * (float)deltaTime;
        
        // Nur neue Chunks wenn die Spieler Kamera und nicht die Debugkamera bewegt wird
        if (ReferenceEquals(_activeCamera, _player.Camera))
        {
            _player.Move(direction);
            return;
        }

        // Debugkamera bewegt sich frei, ohne Chunk-Streaming Event.
        MoveCamera(_activeCamera, direction);
    }

    /// <summary>
    /// Verarbeitet Mausbewegungen für die Kamerasteuerung.
    /// </summary>
    /// <param name="mousePosition">Die aktuelle Mausposition.</param>
    public void LookUpdate(Vector2 mousePosition)
    {
        if (_lastMousePosition == default)
            _lastMousePosition = mousePosition;

        float deltaX = mousePosition.X - _lastMousePosition.X;
        float deltaY = mousePosition.Y - _lastMousePosition.Y;

        _lastMousePosition = mousePosition;

        float yaw = deltaX * _settings.MouseSensitivity;
        float pitch = deltaY * _settings.MouseSensitivity;

        _activeCamera.Rotate(yaw, pitch);
    }

    /// <summary>
    /// Bewegt die Kamera unabhängig von der Spielerbewegung.
    /// Wird für die Debug-Kamera verwendet.
    /// </summary>
    /// <param name="camera">Die zu bewegende <see cref="Camera"/>.</param>
    /// <param name="direction">Die Bewegungsrichtung.</param>
    private static void MoveCamera(Camera camera, Vector3 direction)
    {
        Vector3 groundedFront = Vector3.Normalize(new Vector3(camera.Front.X, 0, camera.Front.Z));
        camera.Position += groundedFront * direction.Z;
        camera.Position += camera.Right * direction.X;
        camera.Position += camera.GlobalUp * direction.Y;
    }
}