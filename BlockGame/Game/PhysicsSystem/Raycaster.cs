using Basics.Game.Logic.TerrainManaging;
using Basics.PhysicsSystem.Structs;
using Silk.NET.Maths;

namespace Basics.Game.PhysicsSystem;

/// <summary>
/// Implementiert den Raycasting-Algorithmus für Voxel-Welten.
/// Wird verwendet, um zu ermitteln, welcher Block vom Kamerastandpunkt aus in einer bestimmten Richtung getroffen wird.
/// </summary>
public class Raycaster
{
    private readonly ChunkProvider _chunkProvider;

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="Raycaster"/>.
    /// </summary>
    /// <param name="chunkProvider">Der für den Raycast zu verwendende <see cref="ChunkProvider"/>.</param>
    public Raycaster(ChunkProvider chunkProvider)
    {
        _chunkProvider = chunkProvider;
    }

    /// <summary>
    /// Führt einen Raycast durch Voxel-Bloecke mithilfe des Amanatides-Woo-Algorithmus aus.
    /// </summary>
    /// <param name="start">Startposition des Strahls in Weltkoordinaten.</param>
    /// <param name="direction">Richtungsvektor des Strahls.</param>
    /// <param name="maxDistance">Maximale Reichweite des Strahls.</param>
    /// <returns>Ein <see cref="BlockResult"/> mit Trefferinformationen oder <c>Hit = false</c>.</returns>
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
        float tMaxX = stepX != 0
            ? (stepX > 0
                ? (MathF.Floor(start.X) + 1.0f - start.X) * tDeltaX
                : (start.X == MathF.Floor(start.X) ? 1.0f : (start.X - MathF.Floor(start.X))) * tDeltaX)
            : float.PositiveInfinity;

        float tMaxY = stepY != 0
            ? (stepY > 0
                ? (MathF.Floor(start.Y) + 1.0f - start.Y) * tDeltaY
                : (start.Y == MathF.Floor(start.Y) ? 1.0f : (start.Y - MathF.Floor(start.Y))) * tDeltaY)
            : float.PositiveInfinity;

        float tMaxZ = stepZ != 0
            ? (stepZ > 0
                ? (MathF.Floor(start.Z) + 1.0f - start.Z) * tDeltaZ
                : (start.Z == MathF.Floor(start.Z) ? 1.0f : (start.Z - MathF.Floor(start.Z))) * tDeltaZ)
            : float.PositiveInfinity;

        Vector3D<int> hitNormal = new Vector3D<int>(0, 0, 0);
        float distanceTravelled = 0.0f;

        // Der Strahl wandert von Block zu Block!
        while (distanceTravelled <= maxDistance)
        {
            // Wir holen die Block-ID an der aktuellen Raycast-Position
            ushort blockId = _chunkProvider.GetBlockAt(x, y, z);

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

    /// <summary>
    /// Führt einen Raycast für Entity-Kollisionen aus (Vorbereitung für AABB-Schnitttest).
    /// </summary>
    /// <param name="start">Startposition des Strahls in Weltkoordinaten.</param>
    /// <param name="dir">Richtungsvektor des Strahls.</param>
    /// <param name="maxDist">Maximale Reichweite des Strahls.</param>
    /// <returns>Ein <see cref="EntityResult"/> mit Trefferinformationen.</returns>
    /// <remarks>
    /// Diese Methode ist derzeit nicht implementiert. Sie dient als Platzhalter für die spätere Implementierung
    /// von AABB-Raycasting für Entitys.
    /// </remarks>
    public EntityResult CastEntityRay(Vector3D<float> start, Vector3D<float> dir, float maxDist)
    {
        return new EntityResult { Hit = false };
    }
}