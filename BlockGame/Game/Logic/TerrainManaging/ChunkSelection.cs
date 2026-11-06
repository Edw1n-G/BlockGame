using Basics.Game.Utilities;
namespace Basics.Game.Logic.TerrainManaging;

/// <summary>Computes the ellipsoid around a player without scheduling work.</summary>
internal static class ChunkSelection
{
    public static HashSet<ChunkCoord> Calculate(ChunkCoord center, int horizontal, int vertical, Func<bool>? shouldStop = null)
    {
        horizontal = Math.Max(0,horizontal); vertical = Math.Max(0,vertical);
        // For tiny selections, constructing a column table costs more than the entire bounded scan.
        if (horizontal <= 2 && vertical <= 3)
        {
            var small = new HashSet<ChunkCoord>();
            for (int x = -horizontal; x <= horizontal; x++)
            {
                if (shouldStop?.Invoke() == true) break;
                for (int z = -horizontal; z <= horizontal; z++)
                for (int y = -vertical; y <= vertical; y++)
                {
                    double h = horizontal == 0 ? 0 : ((double)x*x+(double)z*z)/((double)horizontal*horizontal);
                    double v = vertical == 0 ? 0 : (double)y*y/((double)vertical*vertical);
                    if (h+v <= 1) small.Add(new ChunkCoord(center.X+x,center.Y+y,center.Z+z,0));
                }
            }
            return small;
        }
        var shape = ChunkSelectionShape.Create(horizontal, vertical, shouldStop);
        if (shape is null) return new();
        var result = new HashSet<ChunkCoord>(shape.Count);
        foreach (var column in shape.Columns)
        {
            if (shouldStop?.Invoke() == true) break;
            for (int y = -Math.Min(column.Height, 32768); y <= Math.Min(column.Height, 32767); y++)
                result.Add(new ChunkCoord(center.X + column.X, center.Y + y, center.Z + column.Z, 0));
        }
        return result;
    }
}
