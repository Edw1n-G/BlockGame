using Basics.Common;

namespace Basics.Terrain.Selection;

internal readonly record struct SelectionColumn(int X, int Z, int Height);

/// <summary>One Y interval per X/Z column; reproduces the original floating-point boundary.</summary>
internal sealed class ChunkSelectionShape
{
    public int Horizontal { get; }
    public int Vertical { get; }
    public int Count { get; }
    public SelectionColumn[] Columns { get; }
    private readonly int[] _heights;
    private readonly int _diameter;

    private ChunkSelectionShape(int horizontal, int vertical, int[] heights, SelectionColumn[] columns, int count)
    {
        Horizontal = horizontal; Vertical = vertical; _diameter = checked(horizontal*2+1);
        _heights = heights; Columns = columns; Count = count;
    }

    public int HeightAt(int x, int z) => Math.Abs(x) > Horizontal || Math.Abs(z) > Horizontal
        ? -1 : _heights[(x+Horizontal)*_diameter + z+Horizontal];

    public static ChunkSelectionShape? Create(int horizontal, int vertical, Func<bool>? shouldStop = null)
    {
        horizontal = Math.Max(0,horizontal); vertical = Math.Max(0,vertical);
        if (shouldStop?.Invoke() == true) return null;
        int diameter = checked(horizontal*2+1);
        var heights = new int[checked(diameter*diameter)]; Array.Fill(heights,-1);
        var columns = new List<SelectionColumn>();
        int count = 0;
        for (int x = -horizontal; x <= horizontal; x++)
        {
            if (shouldStop?.Invoke() == true) return null;
            for (int z = -horizontal; z <= horizontal; z++)
            {
                double h = horizontal == 0 ? 0 : ((double)x*x + (double)z*z) / ((double)horizontal*horizontal);
                if (h > 1) continue;
                int height = vertical == 0 ? 0 : (int)Math.Min(vertical, Math.Sqrt((1-h)*((double)vertical*vertical)));
                // Sqrt rounding and h+v rounding can disagree at the boundary. Preserve the old predicate.
                bool Inside(int y) => h + (vertical == 0 ? 0 : (double)y*y / ((double)vertical*vertical)) <= 1;
                while (height > 0 && !Inside(height)) height--;
                while (height < vertical && Inside(height+1)) height++;
                height = Math.Min(height,32768); // larger Y intervals cover the entire short-coordinate circle
                heights[(x+horizontal)*diameter+z+horizontal] = height;
                columns.Add(new(x,z,height));
                count = checked(count + Math.Min(65536,height*2+1));
            }
        }
        return new(horizontal,vertical,heights,columns.ToArray(),count);
    }
}

internal readonly record struct ChunkSelectionRegion(ChunkCoord Center, ChunkSelectionShape Shape)
{
    public bool Contains(ChunkCoord coord)
    {
        if (coord.LodLevel != 0) return false;
        int height = Shape.HeightAt((short)(coord.X-Center.X),(short)(coord.Z-Center.Z));
        return height >= 0 && Math.Abs((int)(short)(coord.Y-Center.Y)) <= height;
    }

    public bool AppendExcept(ChunkSelectionRegion? other, List<ChunkCoord> output, Func<bool>? shouldStop)
    {
        int centerY = Center.Y;
        foreach (var column in Shape.Columns)
        {
            if (shouldStop?.Invoke() == true) return false;
            int x = Center.X+column.X, z = Center.Z+column.Z;
            int lower = -Math.Min(column.Height,32768), upper = Math.Min(column.Height,32767);
            int otherHeight = other is { } region ? region.Shape.HeightAt((short)(x-region.Center.X),(short)(z-region.Center.Z)) : -1;
            if (otherHeight < 0) { Emit(lower,upper); continue; }
            int deltaY = (short)(other!.Value.Center.Y-Center.Y);
            int otherLower = deltaY-Math.Min(otherHeight,32768), otherUpper = deltaY+Math.Min(otherHeight,32767);
            int cursor = lower;
            // Coordinates wrap to short. Three sorted copies handle intervals crossing that seam.
            for (int shift = -65536; shift <= 65536 && cursor <= upper; shift += 65536)
            {
                int start = Math.Max(lower,otherLower+shift), end = Math.Min(upper,otherUpper+shift);
                if (start > end) continue;
                Emit(cursor,start-1); cursor = Math.Max(cursor,end+1);
            }
            Emit(cursor,upper);

            void Emit(int from, int to)
            {
                for (int y = from; y <= to; y++) output.Add(new ChunkCoord(x,centerY+y,z,0));
            }
        }
        return true;
    }
}

/// <summary>Single-owner reusable transition scratch space; canceled plans must never be applied.</summary>
internal sealed class ChunkSelectionPlan
{
    public List<ChunkCoord> Added { get; } = new();
    public List<ChunkCoord> Removed { get; } = new();
    public ChunkSelectionRegion Target { get; private set; }
    private ChunkSelectionShape? _shape, _previousShape;

    public bool TryBuild(ChunkSelectionRegion? previous, ChunkCoord center, int horizontal, int vertical, Func<bool>? shouldStop = null)
    {
        Added.Clear(); Removed.Clear();
        if (shouldStop?.Invoke() == true) return false;
        horizontal = Math.Max(0,horizontal); vertical = Math.Max(0,vertical);
        bool Matches(ChunkSelectionShape? s) => s is not null && s.Horizontal == horizontal && s.Vertical == vertical;
        if (!Matches(_shape))
        {
            var next = Matches(_previousShape) ? _previousShape : ChunkSelectionShape.Create(horizontal,vertical,shouldStop);
            if (next is null) return false;
            _previousShape = _shape; _shape = next;
        }
        Target = new(center,_shape!);
        if (previous is { } old && old.Center == center && old.Shape.Horizontal == horizontal && old.Shape.Vertical == vertical) return true;
        return Target.AppendExcept(previous,Added,shouldStop) &&
            (previous is null || previous.Value.AppendExcept(Target,Removed,shouldStop));
    }
}
