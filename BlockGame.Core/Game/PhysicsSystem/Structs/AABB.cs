namespace Basics.PhysicsSystem.Structs;

/// <summary>
/// Axis-Aligned Bounding Box (AABB) für Kollisionsabfragen.
/// </summary>
public struct AABB
{
    /// <summary>Die minimale X-Koordinate der Box.</summary>
    public float MinX;
    /// <summary>Die minimale Y-Koordinate der Box.</summary>
    public float MinY;
    /// <summary>Die minimale Z-Koordinate der Box.</summary>
    public float MinZ;
    /// <summary>Die maximale X-Koordinate der Box.</summary>
    public float MaxX;
    /// <summary>Die maximale Y-Koordinate der Box.</summary>
    public float MaxY;
    /// <summary>Die maximale Z-Koordinate der Box.</summary>
    public float MaxZ;

    /// <summary>
    /// Prüft, ob diese AABB mit einer anderen AABB schneidet.
    /// </summary>
    /// <param name="other">Die zu prüfende andere AABB.</param>
    /// <returns><c>true</c>, wenn sich die Boxen schneiden, andernfalls <c>false</c>.</returns>
    public bool Intersects(AABB other)
    {
        return (MinX < other.MaxX && MaxX > other.MinX) &&
               (MinY < other.MaxY && MaxY > other.MinY) &&
               (MinZ < other.MaxZ && MaxZ > other.MinZ);
    }
}