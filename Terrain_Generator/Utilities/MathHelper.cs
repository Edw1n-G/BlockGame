using System;

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
}