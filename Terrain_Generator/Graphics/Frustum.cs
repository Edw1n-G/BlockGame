using System.Numerics;

namespace Basics.Graphics;

public struct Frustum
{
    public Plane topFace;
    public Plane bottomFace;

    public Plane rightFace;
    public Plane leftFace;

    public Plane farFace;
    public Plane nearFace;
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