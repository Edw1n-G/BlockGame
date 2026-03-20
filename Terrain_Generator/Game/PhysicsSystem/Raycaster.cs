using Basics.Game.TerrainManaging;
using Basics.PhysicsSystem.Structs;
using Silk.NET.Maths;

namespace Basics.PhysicsSystem;

public class Raycaster
{
    private readonly ChunkProvider _chunkProvider;

    public Raycaster(ChunkProvider chunkProvider)
    {
        _chunkProvider = chunkProvider;
    }

    /// <summary>
    /// Gemini 3.1 hat diesen algorithmus gecookt.
    /// Einfach hoffen, dass es first try funktioniert und ich hier nie was debuggen muss
    /// </summary>
    public BlockResult CastBlockRay(Vector3D<float> start, Vector3D<float> direction, float maxDistance)
    {
        direction = Vector3D.Normalize(direction);

        // Start-Blockkoordinate (abgerundet, wichtig für negative Werte!)
        int x = (int)MathF.Floor(start.X);
        int y = (int)MathF.Floor(start.Y);
        int z = (int)MathF.Floor(start.Z);

        // In welche Richtung gehen wir auf den Achsen? (+1 oder -1)
        int stepX = MathF.Sign(direction.X);
        int stepY = MathF.Sign(direction.Y);
        int stepZ = MathF.Sign(direction.Z);

        // Wie weit muss der Strahl fliegen, um 1 Block auf einer Achse zu überqueren?
        float tDeltaX = stepX != 0 ? MathF.Abs(1.0f / direction.X) : float.PositiveInfinity;
        float tDeltaY = stepY != 0 ? MathF.Abs(1.0f / direction.Y) : float.PositiveInfinity;
        float tDeltaZ = stepZ != 0 ? MathF.Abs(1.0f / direction.Z) : float.PositiveInfinity;

        // Wie weit ist es bis zur ALLERERSTEN Block-Grenze?
        float tMaxX = stepX > 0
            ? (MathF.Floor(start.X) + 1.0f - start.X) * tDeltaX
            : (start.X - MathF.Floor(start.X)) * tDeltaX;
        float tMaxY = stepY > 0
            ? (MathF.Floor(start.Y) + 1.0f - start.Y) * tDeltaY
            : (start.Y - MathF.Floor(start.Y)) * tDeltaY;
        float tMaxZ = stepZ > 0
            ? (MathF.Floor(start.Z) + 1.0f - start.Z) * tDeltaZ
            : (start.Z - MathF.Floor(start.Z)) * tDeltaZ;

        Vector3D<int> hitNormal = new Vector3D<int>(0, 0, 0);
        float distanceTravelled = 0.0f;

        // Der Strahl wandert von Block zu Block!
        while (distanceTravelled <= maxDistance)
        {
            // === DEINE NEUE ARCHITEKTUR IN AKTION ===
            // Wir holen die Block ID an der aktuellen Raycast-Position
            byte blockId = _chunkProvider.GetBlockAt(x, y, z);

            // Ist der Block solide? (0 = Luft, vielleicht hast du noch Wasser etc.)
            if (blockId != 0)
            {
                return new BlockResult
                {
                    Hit = true, HitPosition = new Vector3D<int>(x, y, z), HitNormal = hitNormal
                };
            }

            // Springe zum nächsten Blockraster
            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    x += stepX;
                    distanceTravelled = tMaxX;
                    tMaxX += tDeltaX;
                    hitNormal = new Vector3D<int>(-stepX, 0, 0); // X-Wand getroffen
                }
                else
                {
                    z += stepZ;
                    distanceTravelled = tMaxZ;
                    tMaxZ += tDeltaZ;
                    hitNormal = new Vector3D<int>(0, 0, -stepZ); // Z-Wand getroffen
                }
            }
            else
            {
                if (tMaxY < tMaxZ)
                {
                    y += stepY;
                    distanceTravelled = tMaxY;
                    tMaxY += tDeltaY;
                    hitNormal = new Vector3D<int>(0, -stepY, 0); // Y-Wand (Boden/Decke) getroffen
                }
                else
                {
                    z += stepZ;
                    distanceTravelled = tMaxZ;
                    tMaxZ += tDeltaZ;
                    hitNormal = new Vector3D<int>(0, 0, -stepZ); // Z-Wand getroffen
                }
            }
        }

        // Nichts getroffen
        return new BlockResult { Hit = false };
    }

    //nur Entities Gerade linie und AABB intersection
    public EntityResult CastEntityRay(Vector3D<float> start, Vector3D<float> dir, float maxDist)
    {
        //TODO: AABB raycast für Entitys
        return new EntityResult { Hit = false };
    }
}