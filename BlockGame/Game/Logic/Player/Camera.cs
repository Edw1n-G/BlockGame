using System;
using System.Numerics;
using Basics.Game.Graphics;
using Basics.Game.Utilities;
using Basics.Graphics;
using Plane = Basics.Game.Graphics.Plane;

namespace Basics.Game;

/// <summary>
/// Implementiert eine 3D-Kamera mit Positions-, Rotations- und Ansichtsmatrizen.
/// Wird für das Rendern und Frustum-Culling verwendet.
/// </summary>
public class Camera(Vector3 position)
{
    /// <summary>Die aktuelle Position der Kamera in Weltkoordinaten.</summary>
    public Vector3 Position { get; set; } = position;
    /// <summary>Die Blickrichtung der Kamera (nach vorne in Z-Minus-Richtung).</summary>
    public Vector3 Front { get; set; } = -Vector3.UnitZ;
    /// <summary>Die globale Up-Richtung der Kamera (immer +Y).</summary>
    public Vector3 GlobalUp { get; set; } = Vector3.UnitY;
    /// <summary>Die Right-Richtung der Kamera (senkrecht zu Front und GlobalUp).</summary>
    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Front, GlobalUp));
    /// <summary>Die Up-Richtung der Kamera (relativ zum Pitch der Kamera).</summary>
    public Vector3 Up => Vector3.Normalize(Vector3.Cross(Right, Front));
    
    /// <summary>Die Yaw-Rotation um die Y-Achse (links/rechts), Standard: -90 Grad.</summary>
    public float Yaw { get; set; } = -90f; 
    /// <summary>Der Pitch um die X-Achse (hoch/runter), Standard: 0 Grad.</summary>
    public float Pitch { get; set; } = 0f;
    
    /// <summary>Die nahe Schnittebene für das Frustum.</summary>
    public float nearPlane = 0.1f;
    /// <summary>Die ferne Schnittebene für das Frustum.</summary>
    public float farPlane = 1500f;
    /// <summary>Der vertikale Sichtwinkel (Field of View) in Grad.</summary>
    public float fovY = 45f;
    /// <summary>Das Aspect Ratio (Breite/Höhe) des Sichtfelds.</summary>
    public float AspectRatio = 16f / 9f; // Default-Wert, wird aber in Renderer gesetzt beim start
    
    /// <summary>
    /// Berechnet die Chunk-Koordinaten für die gegebene Weltposition.
    /// </summary>
    /// <param name="pos">Die Weltposition.</param>
    /// <returns>Die <see cref="ChunkCoord"/> für die gegebene Position im LOD0-System.</returns>
    public ChunkCoord GetChunkCoord(Vector3 pos)
    {
        // Integer-Division die auch für negative Koordinaten korrekt funktioniert
        int cx = (int)MathF.Floor(pos.X / 16f);
        int cy = (int)MathF.Floor(pos.Y / 16f);
        int cz = (int)MathF.Floor(pos.Z / 16f);
        return new ChunkCoord(cx, cy, cz, 0);//Die Camera ist immer im Lod0 System
    }
    
    /// <summary>
    /// Berechnet die Ansichtsmatrix für diese Kamera.
    /// </summary>
    /// <returns>Die 4x4-View-Matrix als <see cref="Matrix4x4"/>.</returns>
    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Position + Front, GlobalUp);
    }
    
    /// <summary>
    /// Drehung der Kamera um Yaw und Pitch.
    /// </summary>
    /// <param name="deltayaw">Die Änderung des Yaw-Winkels (links/rechts).</param>
    /// <param name="deltapitch">Die Änderung des Pitch-Winkels (hoch/runter).</param>
    /// <remarks>
    /// Yaw: Rotation um die Y-Achse (links/rechts)
    /// Pitch: Rotation um die X-Achse (hoch/runter)
    /// Der Pitch wird auf -89 bis +89 Grad begrenzt, um ÜberRotation zu verhindern.
    /// </remarks>
    public void Rotate(float deltayaw, float deltapitch)
    {
        Yaw += deltayaw;
        Pitch -= deltapitch;
        Pitch = Math.Clamp(Pitch, -89.0f, 89.0f);
        
        Vector3 front;
        front.X = MathF.Cos(MathHelper.DegreesToRadians(Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(Pitch));
        front.Y = MathF.Sin(MathHelper.DegreesToRadians(Pitch));
        front.Z = MathF.Sin(MathHelper.DegreesToRadians(Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(Pitch));
        
        Front = Vector3.Normalize(front);
    }
    
    /// <summary>
    /// Erstellt ein Frustum für diese Kamera, das für Frustum Culling verwendet werden kann.
    /// </summary>
    /// <param name="view">Die View-Matrix.</param>
    /// <param name="projection">Die Projektionsmatrix.</param>
    /// <returns>Das erstellte <see cref="Frustum"/> für das Culling.</returns>
    public Frustum CreateFrustum(Matrix4x4 view, Matrix4x4 projection)
    {
        //Matrizen kombinieren
        Matrix4x4 vp = view * projection;
        Frustum frustum = new Frustum();
    
        // Left Face (w + x)
        frustum.LeftFace = NormalizePlane(new Plane(
            new Vector3(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31), vp.M44 + vp.M41));

        // Right Face (w - x)
        frustum.RightFace = NormalizePlane(new Plane(
            new Vector3(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31), vp.M44 - vp.M41));

        // Bottom Face (w + y)
        frustum.BottomFace = NormalizePlane(new Plane(
            new Vector3(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32), vp.M44 + vp.M42));

        // Top Face (w - y)
        frustum.TopFace = NormalizePlane(new Plane(
            new Vector3(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32), vp.M44 - vp.M42));

        // Near Face (w + z)
        frustum.NearFace = NormalizePlane(new Plane(
            new Vector3(vp.M13, vp.M23, vp.M33), vp.M43));

        // Far Face (w - z)
        frustum.FarFace = NormalizePlane(new Plane(
            new Vector3(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33), vp.M44 - vp.M43));

        return frustum;
    }

    /// <summary>
    /// Normalisiert eine Ebene, sodass die Normale die Länge 1 hat.
    /// </summary>
    /// <param name="p">Die zu normalisierende <see cref="Plane"/>.</param>
    /// <returns>Die normalisierte <see cref="Plane"/>.</returns>
    private Plane NormalizePlane(Plane p)
    {
        float length = p.Normal.Length();
        // Verhindere Division durch 0
        if (length <= 0) return p; 
    
        return new Plane(p.Normal / length, p.Distance / length);
    }
}