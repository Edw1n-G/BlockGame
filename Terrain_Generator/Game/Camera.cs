using System;
using System.Numerics;
using Basics.Graphics;
using Basics.Utilities;

namespace Basics.Game;

public class Camera(Vector3 position)
{
    public Vector3 Position { get; set; } = position;
    public Vector3 Front { get; set; } = -Vector3.UnitZ; // Blickrichtung (nach vorne in Z-Minus)
    public Vector3 GlobalUp { get; set; } = Vector3.UnitY; //Zeigt immer +Y
    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Front, GlobalUp));
    public Vector3 Up => Vector3.Normalize(Vector3.Cross(Right, Front)); // Zeigt relativ zum _pitch der Kamera nach oben
    
    public float Yaw { get; set; } = -90f; 
    public float Pitch { get; set; } = 0f;
    
    //Parameter für Kamera einstellung und Frustum Culling
    public float nearPlane = 0.1f;
    public float farPlane = 1000f;
    public float fovY = 45f;
    public float AspectRatio = 16f / 9f; // Default-Wert, wird aber in Renderer gesetzt beim start
    
    // Event das gefeuert wird, wenn der Spieler einen neuen Chunk betritt
    public event Action<ChunkCoord>? OnChunkChanged;
    private ChunkCoord _currentChunkCoord = GetChunkCoord(position);

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
        int cy = (int)MathF.Floor(pos.Y / 32f);
        int cz = (int)MathF.Floor(pos.Z / 32f);
        return new ChunkCoord(cx, cy, cz, 0);//Die Camera ist immer im Lod0 System
    }
    
    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Position + Front, GlobalUp);
    }
    
    public void Move(Vector3 direction)
    {
        // Y aus dem Frontvektor entfernen, um auf den Boden zu bleiben
        Vector3 groundedFront = Vector3.Normalize(new Vector3(Front.X, 0, Front.Z));
        // Wir bewegen uns relativ zur Blickrichtung
        // direction.X ist Strafing (Seitwärts), direction.Z ist Vor/Zurück
        Position += groundedFront * direction.Z;
        Position += Right * direction.X;
        Position += GlobalUp * direction.Y; // Optional: Fliegen
        
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
        Yaw += deltayaw;
        Pitch -= deltapitch;
        Pitch = Math.Clamp(Pitch, -89.0f, 89.0f);
        // Yaw: Rotation um die Y-Achse (links/rechts)
        // Pitch: Rotation um die X-Achse (hoch/runter)
        
        Vector3 front;
        front.X = MathF.Cos(MathHelper.DegreesToRadians(Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(Pitch));
        front.Y = MathF.Sin(MathHelper.DegreesToRadians(Pitch));
        front.Z = MathF.Sin(MathHelper.DegreesToRadians(Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(Pitch));
        
        Front = Vector3.Normalize(front);
    }
    
    
    //<summary>
    //Erstellt ein Frustum für diese Kamera, das für Frustum Culling verwendet werden kann.
    //Müsste jedes Mal neu erstellt werden, wenn die einstellungen der Kamera sich in Runtime ändern
    //Aber da ich noch keine Einstellungen hab Problem für future me
    // Learn Opengl.com hat Planes benutzt und mit Kreuzprodukt gerechnet
    // Gemini hat mir aber gesagt ich die Matrixen benutzen was super geil ist und viel besser funktioniert
    //</summary>
    public Frustum CreateFrustum(Matrix4x4 view, Matrix4x4 projection)
    {
        //Matrizen kombinieren
        Matrix4x4 vp = view * projection;
        Frustum frustum = new Frustum();
    
        // Left Face (w + x)
        frustum.LeftFace = NormalizePlane(new Basics.Graphics.Plane(
            new Vector3(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31), vp.M44 + vp.M41));

        // Right Face (w - x)
        frustum.RightFace = NormalizePlane(new Basics.Graphics.Plane(
            new Vector3(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31), vp.M44 - vp.M41));

        // Bottom Face (w + y)
        frustum.BottomFace = NormalizePlane(new Basics.Graphics.Plane(
            new Vector3(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32), vp.M44 + vp.M42));

        // Top Face (w - y)
        frustum.TopFace = NormalizePlane(new Basics.Graphics.Plane(
            new Vector3(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32), vp.M44 - vp.M42));

        // Near Face (w + z)
        frustum.NearFace = NormalizePlane(new Basics.Graphics.Plane(
            new Vector3(vp.M13, vp.M23, vp.M33), vp.M43));

        // Far Face (w - z)
        frustum.FarFace = NormalizePlane(new Basics.Graphics.Plane(
            new Vector3(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33), vp.M44 - vp.M43));

        return frustum;
    }

// Hilfsmethode, um die Normalen auf eine Länge von 1 zu bringen
    private Basics.Graphics.Plane NormalizePlane(Basics.Graphics.Plane p)
    {
        float length = p.Normal.Length();
        // Verhindere Division durch 0
        if (length <= 0) return p; 
    
        return new Basics.Graphics.Plane(p.Normal / length, p.Distance / length);
    }
}