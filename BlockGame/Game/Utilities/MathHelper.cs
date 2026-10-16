using System.Numerics;
using Silk.NET.Maths;

namespace Basics.Game.Utilities;

/// <summary>
/// Hilfsklasse für gängige mathematische Umrechnungen und Konvertierungen.
/// </summary>
public static class MathHelper
{
    /// <summary>
    /// Wandelt Grad in Bogenmaß um.
    /// </summary>
    /// <param name="degrees">Winkel in Grad.</param>
    /// <returns>Winkel in Bogenmaß.</returns>
    public static float DegreesToRadians(float degrees)
    {
        return MathF.PI / 180f * degrees;
    }
    
    /// <summary>
    /// Wandelt einen Silk.NET <see cref="Vector3D{T}"/> in einen <see cref="Vector3"/> um.
    /// </summary>
    /// <param name="v">Der umzuwandelnde Vektor.</param>
    /// <returns>Der konvertierte <see cref="Vector3"/>.</returns>
    public static Vector3 ToNumerics(Vector3D<float> v) 
        => new(v.X, v.Y, v.Z);
    
    /// <summary>
    /// Wandelt einen <see cref="Vector3"/> in einen Silk.NET <see cref="Vector3D{T}"/> um.
    /// </summary>
    /// <param name="v">Der umzuwandelnde Vektor.</param>
    /// <returns>Der konvertierte <see cref="Vector3D{float}"/>.</returns>
    public static Vector3D<float> ToGeneric(Vector3 v) 
        => new(v.X, v.Y, v.Z);
}