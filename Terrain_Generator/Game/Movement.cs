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
    private static Vector2 LastMousePosition;
    
    private const float Speed = 12f;
    private const float sensitivity = 0.1f; // Empfindlichkeit der Mausbewegung

    public static void MovementUpdate(double deltaTime, Camera camera)
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
    
    public static void LookUpdate(Vector2 Mouseposition, Camera camera)
    {
        if (camera == null) return;
        if (LastMousePosition == default) { LastMousePosition = Mouseposition; }
        
        float deltaX = Mouseposition.X - LastMousePosition.X;
        float deltaY = Mouseposition.Y - LastMousePosition.Y;
        
        LastMousePosition = Mouseposition;
        
        float yaw = deltaX * sensitivity;
        float pitch = deltaY * sensitivity;

        camera.Rotate(yaw, pitch);
    }

}