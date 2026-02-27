using System.Numerics;
using Basics.Game;

namespace Basics.Input;

/**
 * Statische Klasse, die sich um die Bewegung der Kamera kümmert.
 * Bewegungsrichtung und geschwindigkeit wird berechnet.
 * Die Position und Rotation der Kamera aktualisiert dann die Camera.cs selbst.
 */
public class Movement
{
    private static Vector2 _lastMousePosition;
    private static Camera camera;
    
    private const float Speed = 12f;
    private const float Sensitivity = 0.1f; // Empfindlichkeit der Mausbewegung
    
    public static void SetPlayerCamera(Camera playerCamera)
    {
        camera = playerCamera;
    }

    public static void MovementUpdate(double deltaTime)
    {
        if (camera == null) return;
        
        Vector3 direction = Vector3.Zero;
        
        
        // Z-Achse: Vor/Zurück
        if (InputManager.IsActionPressed(Actions.Forward))
            direction.Z += 1.0f;
        if (InputManager.IsActionPressed(Actions.Backward))
            direction.Z -= 1.0f;

        // X-Achse: Links/Rechts
        if (InputManager.IsActionPressed(Actions.Left))
            direction.X -= 1.0f;
        if (InputManager.IsActionPressed(Actions.Right))
            direction.X += 1.0f;

        // Y-Achse: Hoch/Runter
        if (InputManager.IsActionPressed(Actions.Up))
            direction.Y += 1.0f;
        if (InputManager.IsActionPressed(Actions.Down))
            direction.Y -= 1.0f;
        
       
        
        // --- Bewegung ausführen ---
        if (direction != Vector3.Zero)
        {
            direction = Vector3.Normalize(direction) * Speed * (float)deltaTime;
            camera.Move(direction);
        }
    }
    
    public static void LookUpdate(Vector2 Mouseposition)
    {
        if (camera == null) return;
        if (_lastMousePosition == default) { _lastMousePosition = Mouseposition; }
        
        float deltaX = Mouseposition.X - _lastMousePosition.X;
        float deltaY = Mouseposition.Y - _lastMousePosition.Y;
        
        _lastMousePosition = Mouseposition;
        
        float yaw = deltaX * Sensitivity;
        float pitch = deltaY * Sensitivity;

        camera.Rotate(yaw, pitch);
    }

}