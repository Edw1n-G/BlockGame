using System.Numerics;
using Basics.Game.Utilities;
using System.Runtime.CompilerServices;

namespace Basics.Game.Graphics;

/// <summary>
/// Stellt ein Frustum zur Verfügung, das zum Culling von Chunks verwendet wird.
/// Prüft, ob Chunks innerhalb oder außerhalb des View-Frustums liegen.
/// </summary>
public struct Frustum
{
    /// <summary>
    /// Die obere Begrenzungsebene des Frustum.
    /// </summary>
    public Plane TopFace;
    
    /// <summary>
    /// Die untere Begrenzungsebene des Frustum.
    /// </summary>
    public Plane BottomFace;

    /// <summary>
    /// Die rechte Begrenzungsebene des Frustum.
    /// </summary>
    public Plane RightFace;
    
    /// <summary>
    /// Die linke Begrenzungsebene des Frustum.
    /// </summary>
    public Plane LeftFace;

    /// <summary>
    /// Die ferne Begrenzungsebene des Frustum.
    /// </summary>
    public Plane FarFace;
    
    /// <summary>
    /// Die nahe Begrenzungsebene des Frustum.
    /// </summary>
    public Plane NearFace;
    
    /// <summary>
    /// Prüft, ob ein Chunk innerhalb des Frustum liegt.
    /// </summary>
    /// <param name="chunk">Der zu prüfende Chunk.</param>
    /// <returns><c>true</c>, wenn der Chunk sichtbar ist; andernfalls <c>false</c>.</returns>
    public bool IsInFrustum(ChunkCoord chunk)
    {
        int scale = 1 << chunk.LodLevel;
        int halfSize = 8 * scale;
        
        int x = halfSize + 16 * scale * chunk.X;
        int y = halfSize + 16 * scale * chunk.Y;
        int z = halfSize + 16 * scale * chunk.Z;
        
        Vector3 center = new Vector3(x, y, z);
        
        Vector3 extents = new Vector3(halfSize, halfSize, halfSize);

        return IsOnOrForwardPlane(this.NearFace, center, extents) &&
               IsOnOrForwardPlane(this.LeftFace, center, extents) &&
               IsOnOrForwardPlane(this.RightFace, center, extents) &&
               IsOnOrForwardPlane(this.FarFace, center, extents) &&
               IsOnOrForwardPlane(this.BottomFace, center, extents) &&
               IsOnOrForwardPlane(this.TopFace, center, extents);
    }
    
    /// <summary>
    /// Prüft, ob eine Box im Frustum liegt oder davor steht (nicht dahinter).
    /// </summary>
    /// <param name="plane">Die zu prüfende Ebene.</param>
    /// <param name="center">Das Zentrum der Box.</param>
    /// <param name="extents">Die halben Ausdehnungen der Box.</param>
    /// <returns><c>true</c>, wenn die Box vor oder auf der Ebene liegt; andernfalls <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] 
    private bool IsOnOrForwardPlane(Plane plane, Vector3 center, Vector3 extents)
    {
        float r = Vector3.Dot(extents, plane.AbsNormal);

        float distance = plane.GetDistanceToPoint(center);

        return distance >= -r;
    }
}

/// <summary>
/// Stellt eine Ebene im 3D-Raum dar, die durch eine Normalen- und eine Distanz-Variable definiert ist.
/// Wird für Frustum-Culling verwendet.
/// </summary>
public struct Plane
{
    /// <summary>
    /// Die Normale der Ebene.
    /// </summary>
    public Vector3 Normal;
    
    /// <summary>
    /// Der Abstand der Ebene vom Ursprung entlang der Normalen.
    /// </summary>
    public float Distance;
    
    /// <summary>
    /// Der Betrag der Normalen, verwendet für die Berechnung des Box-Radius.
    /// </summary>
    public Vector3 AbsNormal;

    /// <summary>
    /// Initialisiert eine neue Instanz des <see cref="Plane"/>-structs.
    /// </summary>
    /// <param name="normal">Die Normale der Ebene.</param>
    /// <param name="distance">Der Abstand der Ebene vom Ursprung.</param>
    public Plane(Vector3 normal, float distance)
    {
        Normal = normal;
        Distance = distance;
        AbsNormal = Vector3.Abs(normal);
    }
    
    /// <summary>
    /// Berechnet den Abstand eines Punkts zur Ebene.
    /// </summary>
    /// <param name="point">Der Punkt, dessen Abstand berechnet werden soll.</param>
    /// <returns>Der Abstand des Punkts zur Ebene. Positive Werte bedeuten, der Punkt liegt vor der Ebene.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetDistanceToPoint(Vector3 point)
    {
        return Vector3.Dot(Normal, point) + Distance;
    }
}