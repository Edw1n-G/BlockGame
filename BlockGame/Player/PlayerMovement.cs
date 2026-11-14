using Basics.Engine;
using Basics.Input;
using Basics.Settings;
using System.Numerics;

namespace Basics.Player;

/// <summary>
/// Verwaltet die Spielerbewegung basierend auf Eingabe und Kamera.
/// </summary>
public class PlayerMovement : IDisposable
{
    private readonly PlayerCharacter _player;
    private readonly IMovementInput _input;
    private readonly GameSettings _settings;
    private Camera _activeCamera;

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="PlayerMovement"/>.
    /// </summary>
    /// <param name="player">Der zu steuernde <see cref="PlayerCharacter"/>.</param>
    /// <param name="inputManager">Der <see cref="InputManager"/> zur Verarbeitung der Eingaben.</param>
    /// <param name="settings">Die optionalen <see cref="GameSettings"/> für Bewegungsparameter.</param>
    public PlayerMovement(PlayerCharacter player, InputManager inputManager, GameSettings? settings = null)
        : this(player, new RealMovementInput(inputManager, settings ?? GameSettings.Current), settings) { }

    public PlayerMovement(PlayerCharacter player, IMovementInput input, GameSettings? settings = null)
    {
        _player = player; _input = input;
        _settings = settings ?? EngineContext.Current?.Settings ?? GameSettings.Current;
        _activeCamera = player.Camera;
    }

    public void Dispose() { if (_input is IDisposable disposable) disposable.Dispose(); }

    /// <summary>
    /// Legt die aktive Kamera für die Bewegung fest.
    /// </summary>
    /// <param name="camera">Die neue aktive <see cref="Camera"/>.</param>
    public void SetActiveCamera(Camera camera)
    {
        _activeCamera = camera;
        _input.ResetLook();
    }

    /// <summary>
    /// Setzt die Spielerkamera als aktive Kamera zurück.
    /// </summary>
    public void UsePlayerCamera()
    {
        _activeCamera = _player.Camera;
        _input.ResetLook();
    }

    /// <summary>
    /// Führt die Bewegungsaktualisierung basierend auf Tastatureingabe aus.
    /// </summary>
    /// <param name="deltaTime">Die Zeit in Sekunden seit dem letzten Frame.</param>
    public void MovementUpdate(double deltaTime)
    {
        var sample = _input.Read(deltaTime);
        _activeCamera.Rotate(sample.LookDegrees.X, sample.LookDegrees.Y);
        Vector3 direction = sample.Direction;

        if (direction == Vector3.Zero)
            return;

        direction = Vector3.Normalize(direction) * _settings.PlayerMoveSpeed * (float)deltaTime * sample.MovementFraction;
        
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
        if (_input is RealMovementInput real) real.LookUpdate(mousePosition);
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