using System.Numerics;
using Basics.Graphics;
using Basics.Utilities;

namespace Basics.Game;

public class Camera
{
    public Vector3 Position { get; set; }
    public Vector3 Front { get; set; } = -Vector3.UnitZ; // Blickrichtung (nach vorne in Z-Minus)
    public Vector3 Up { get; set; } = Vector3.UnitY;
    
    private float _yaw = -90f; 
    private float _pitch = 0f;
    
    //Parameter für Kamera einstellung und Frustum Culling
    public float nearPlane = 0.1f;
    public float farPlane = 1000f;
    public float fovY = 45f;
    public float aspectRatio = 16f / 9f;
    
    // Event das gefeuert wird wenn der Spieler einen neuen Chunk betritt
    public event Action<ChunkCoord>? OnChunkChanged;
    private ChunkCoord _currentChunkCoord;
    
    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Front, Up));
    
    public Camera(Vector3 position)
    {
        Position = position;
        _currentChunkCoord = GetChunkCoord(position);
    }
    
    /// <summary>
    /// Feuert das OnChunkChanged Event manuell, z.B. beim Spielstart
    /// </summary>
    public void ForceChunkUpdate()
    {
        _currentChunkCoord = GetChunkCoord(Position);
        OnChunkChanged?.Invoke(_currentChunkCoord);
    }
    
    private static ChunkCoord GetChunkCoord(Vector3 pos)
    {
        // Integer-Division die auch für negative Koordinaten korrekt funktioniert
        int cx = (int)MathF.Floor(pos.X / 32f);
        int cy = 0; // Vorerst nur horizontale Chunks
        int cz = (int)MathF.Floor(pos.Z / 32f);
        return new ChunkCoord(cx, cy, cz);
    }
    
    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Position + Front, Up);
    }
    
    public void Move(Vector3 direction)
    {
        // Y aus dem Frontvektor entfernen, um auf den Boden zu bleiben
        Vector3 groundedFront = Vector3.Normalize(new Vector3(Front.X, 0, Front.Z));
        // Wir bewegen uns relativ zur Blickrichtung
        // direction.X ist Strafing (Seitwärts), direction.Z ist Vor/Zurück
        Position += groundedFront * direction.Z;
        Position += Right * direction.X;
        Position += Up * direction.Y; // Optional: Fliegen
        
        // Prüfen ob wir in einen neuen Chunk gewechselt haben
        ChunkCoord newChunk = GetChunkCoord(Position);
        if (newChunk != _currentChunkCoord)
        {
            _currentChunkCoord = newChunk;
            OnChunkChanged?.Invoke(_currentChunkCoord);
        }
    }
    
    public void Rotate(float deltayaw, float deltapitch)
    {
        _yaw += deltayaw;
        _pitch -= deltapitch;
        _pitch = Math.Clamp(_pitch, -89.0f, 89.0f);
        // Yaw: Rotation um die Y-Achse (links/rechts)
        // Pitch: Rotation um die X-Achse (hoch/runter)
        // Wir berechnen die neue Front-Vektor basierend auf den Yaw und Pitch Werten
        Vector3 front;
        front.X = MathF.Cos(MathHelper.DegreesToRadians(_yaw)) * MathF.Cos(MathHelper.DegreesToRadians(_pitch));
        front.Y = MathF.Sin(MathHelper.DegreesToRadians(_pitch));
        front.Z = MathF.Sin(MathHelper.DegreesToRadians(_yaw)) * MathF.Cos(MathHelper.DegreesToRadians(_pitch));
        
        Front = Vector3.Normalize(front);
    }
    
    
    //<summary>
    //Erstellt ein Frustum für diese Kamera, das für Frustum Culling verwendet werden kann.
    //Müsste jedes Mal neu erstellt werden, wenn die einstellungen der Kamera sich in Runtime ändern
    //Aber da ich noch keine Einstellungen hab Problem für future me
    //</summary>
    public Frustum CreateFrustum()
    {
        Frustum frustum;
        float halfVSide = farPlane * MathF.Tan(fovY * .5f);
        float halfHSide = halfVSide * aspectRatio;
        Vector3 frontMultFar = farPlane * Front;
        
        frustum.nearFace = new Basics.Graphics.Plane(Front, -Vector3.Dot(Front, Position + Front * nearPlane));
        frustum.farFace = new Basics.Graphics.Plane(-Front, Vector3.Dot(-Front, Position + frontMultFar));
        
        frustum.rightFace = new Basics.Graphics.Plane(
            Vector3.Normalize(Vector3.Cross(frontMultFar - Right * halfHSide, Up)),
            -Vector3.Dot(Vector3.Normalize(Vector3.Cross(frontMultFar - Right * halfHSide, Up)), Position));

        frustum.leftFace = new Basics.Graphics.Plane(
            Vector3.Normalize(Vector3.Cross(Up, frontMultFar + Right * halfHSide)),
            -Vector3.Dot(Vector3.Normalize(Vector3.Cross(Up, frontMultFar + Right * halfHSide)), Position));

        frustum.topFace = new Basics.Graphics.Plane(
            Vector3.Normalize(Vector3.Cross(Right, frontMultFar - Up * halfVSide)),
            -Vector3.Dot(Vector3.Normalize(Vector3.Cross(Right, frontMultFar - Up * halfVSide)), Position));

        frustum.bottomFace = new Basics.Graphics.Plane(
            Vector3.Normalize(Vector3.Cross(frontMultFar + Up * halfVSide, Right)),
            -Vector3.Dot(Vector3.Normalize(Vector3.Cross(frontMultFar + Up * halfVSide, Right)), Position));

        return frustum;
    }
}