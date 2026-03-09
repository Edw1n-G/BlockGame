namespace Basics.PhysicsSystem.Structs;

public struct AABB
{
    public float MinX, MinY, MinZ;
    public float MaxX, MaxY, MaxZ;

    public bool Intersects(AABB other)
    {
        return (MinX < other.MaxX && MaxX > other.MinX) &&
               (MinY < other.MaxY && MaxY > other.MinY) &&
               (MinZ < other.MaxZ && MaxZ > other.MinZ);
    }
}