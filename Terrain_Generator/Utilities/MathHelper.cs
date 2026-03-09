using System;
using System.Numerics;
using Silk.NET.Maths;

namespace Basics.Utilities;

/**
 * Hilfsklasse um Code zu vereinfachen
 */
public class MathHelper
{
    public static float DegreesToRadians(float degrees)
    {
        return MathF.PI / 180f * degrees;
    }
    
    public static Vector3 ToNumerics(Vector3D<float> v) 
        => new(v.X, v.Y, v.Z);
    
    public static Vector3D<float> ToGeneric(Vector3 v) 
        => new(v.X, v.Y, v.Z);
}