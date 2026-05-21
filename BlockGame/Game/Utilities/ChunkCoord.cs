

namespace Basics.Game.Utilities;

/// <summary>
/// Nur für die Identifikation der Chunks gedacht
/// Für verwendung in Listen etc.
/// 3 shorts + byte = 7bytes
/// soll max 8 byte bleiben.
/// wenn mehr gebraucht wird bitpacking anwenden da shorts -32.768 und +32.767 max werte
/// aber ca +- 13.500 angepeilt ist
/// </summary>
public struct ChunkCoord : IEquatable<ChunkCoord>
{
    public readonly short X;
    public readonly short Y;
    public readonly short Z;
    public readonly byte LodLevel;

    public ChunkCoord(int x, int y, int z, byte Lod)
    {
        X = (short)x;
        Y = (short)y;
        Z = (short)z;
        LodLevel = Lod;
    }

    public bool Equals(ChunkCoord other) => X == other.X && Y == other.Y && Z == other.Z && LodLevel == other.LodLevel;
    public override bool Equals(object? obj) => obj is ChunkCoord other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, LodLevel);
    public static bool operator ==(ChunkCoord left, ChunkCoord right) => left.Equals(right);
    public static bool operator !=(ChunkCoord left, ChunkCoord right) => !left.Equals(right);
    public override string ToString() => $"({X}, {Y}, {Z})";
}