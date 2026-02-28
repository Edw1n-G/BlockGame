using System.Numerics;
using Basics.Utilities;

namespace Basics.Graphics;

public struct Frustum
{
    public Plane TopFace;
    public Plane BottomFace;

    public Plane RightFace;
    public Plane LeftFace;

    public Plane FarFace;
    public Plane NearFace;
    
    private const int CHUNK_WIDTH = 32;
    private const int CHUNK_DEPTH = 32;
    private const int CHUNK_HEIGHT = 32;
    
    public bool isInFrustum(ChunkCoord chunk, Frustum frustum)
    {
        //Mittelpunkt (Center) des Chunks berechnen
        // Chunkcoord gibt die KOordinaten aus dem Chunk grid an
        // 16 + 32*x/y geht gut
        
        int x = 16 + 32*chunk.X;
        int y = 16 + 32*chunk.Y;
        int z = 16 + 32*chunk.Z;
        
        Vector3 center = new Vector3(x, y, z);

        //Halbe Ausdehnung (Extents) für die Box berechnen
        Vector3 extents = new Vector3(
            CHUNK_WIDTH / 2f,
            CHUNK_HEIGHT / 2f,
            CHUNK_DEPTH / 2f
        );

        //Prüfen, ob die Box VOR allen 6 Ebenen liegt
        return IsOnOrForwardPlane(this.LeftFace, center, extents) &&
               IsOnOrForwardPlane(this.RightFace, center, extents) &&
               IsOnOrForwardPlane(this.FarFace, center, extents) &&
               IsOnOrForwardPlane(this.NearFace, center, extents) &&
               IsOnOrForwardPlane(this.TopFace, center, extents) &&
               IsOnOrForwardPlane(this.BottomFace, center, extents);
    }
    
    
    private bool IsOnOrForwardPlane(Plane plane, Vector3 center, Vector3 extents)
    {
        // Wir projizieren die halbe Größe der Box auf die Normale der Ebene.
        // Das ergibt den "Radius" der Box aus Sicht der Ebene.
        float r = extents.X * System.Math.Abs(plane.Normal.X) +
                  extents.Y * System.Math.Abs(plane.Normal.Y) +
                  extents.Z * System.Math.Abs(plane.Normal.Z);

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

    public Plane(Vector3 normal, float distance)
    {
        Normal = normal;
        Distance = distance;
    }

    public float GetDistanceToPoint(Vector3 point)
    {
        return Vector3.Dot(Normal, point) + Distance;
    }
}