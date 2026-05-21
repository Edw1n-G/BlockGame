using System.Numerics;
using Basics.Game.Utilities;
using System.Runtime.CompilerServices;

namespace Basics.Game.Graphics;

public struct Frustum
{
    public Plane TopFace;
    public Plane BottomFace;

    public Plane RightFace;
    public Plane LeftFace;

    public Plane FarFace;
    public Plane NearFace;
    
    
    
    public bool IsInFrustum(ChunkCoord chunk)
    {
        //Mittelpunkt (Center) des Chunks berechnen
        // Chunkcoord gibt die Koordinaten aus dem Chunk grid an
        // LOD-Level bestimmt den Skalierungsfaktor: LOD 0 = 1, LOD 1 = 2, LOD 2 = 4
        int scale = 1 << chunk.LodLevel;
        int halfSize = 8 * scale;
        
        int x = halfSize + 16 * scale * chunk.X;
        int y = halfSize + 16 * scale * chunk.Y;
        int z = halfSize + 16 * scale * chunk.Z;
        
        Vector3 center = new Vector3(x, y, z);
        
        // Axis-aligned extents for the chunk bounds
        Vector3 extents = new Vector3(halfSize, halfSize, halfSize);

        //Prüfen, ob die Box VOR allen 6 Ebenen liegt
        return IsOnOrForwardPlane(this.NearFace, center, extents) &&
               IsOnOrForwardPlane(this.LeftFace, center, extents) &&
               IsOnOrForwardPlane(this.RightFace, center, extents) &&
               IsOnOrForwardPlane(this.FarFace, center, extents) &&
               IsOnOrForwardPlane(this.BottomFace, center, extents) &&
               IsOnOrForwardPlane(this.TopFace, center, extents);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)] 
    private bool IsOnOrForwardPlane(Plane plane, Vector3 center, Vector3 extents)
    {
        // Wir projizieren die halbe Größe der Box auf die Normale der Ebene.
        // Das ergibt den "Radius" der Box aus Sicht der Ebene.
        float r = Vector3.Dot(extents, plane.AbsNormal);

        // Abstand vom Zentrum zur Ebene (+ = vor der Ebene, - = hinter)
        float distance = plane.GetDistanceToPoint(center);

        // Wenn der Abstand kleiner ist als der negative Radius, ist die Box komplett dahinter.
        return distance >= -r;
    }
}

public struct Plane
{
    public Vector3 Normal;
    public float Distance;
    public Vector3 AbsNormal;

    public Plane(Vector3 normal, float distance)
    {
        Normal = normal;
        Distance = distance;
        AbsNormal = Vector3.Abs(normal);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetDistanceToPoint(Vector3 point)
    {
        return Vector3.Dot(Normal, point) + Distance;
    }
}