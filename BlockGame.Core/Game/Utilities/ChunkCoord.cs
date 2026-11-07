

namespace Basics.Game.Utilities;

/// <summary>
/// Struktur zur eindeutigen Identifizierung von Chunks im World-Grid.
/// Wird für die Verwendung in Collections und als Schlüssel in Dictionarys verwendet.
/// </summary>
/// <remarks>
/// Optimiert für Speichereffizienz mit 3 Shorts (je 2 Bytes) und 1 Byte für LOD-Level = 7 Bytes insgesamt.
/// Maximale Kapazität liegt bei etwa ±13.500 Chunks pro Achse, ausreichend für die meisten Anwendungsfälle.
/// Die Shorts unterstützen Werte von -32.768 bis +32.767; bei Bedarf können Bitpacking-Techniken eingesetzt werden.
/// </remarks>
public struct ChunkCoord : IEquatable<ChunkCoord>
{
    /// <summary>Die X-Koordinate des Chunks im Grid.</summary>
    public readonly short X;
    /// <summary>Die Y-Koordinate des Chunks im Grid.</summary>
    public readonly short Y;
    /// <summary>Die Z-Koordinate des Chunks im Grid.</summary>
    public readonly short Z;
    /// <summary>Der LOD-Level (Level of Detail) für dieses Chunk.</summary>
    public readonly byte LodLevel;

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="ChunkCoord"/>.
    /// </summary>
    /// <param name="x">Die X-Koordinate des Chunks.</param>
    /// <param name="y">Die Y-Koordinate des Chunks.</param>
    /// <param name="z">Die Z-Koordinate des Chunks.</param>
    /// <param name="Lod">Der LOD-Level für dieses Chunk.</param>
    public ChunkCoord(int x, int y, int z, byte Lod)
    {
        X = (short)x;
        Y = (short)y;
        Z = (short)z;
        LodLevel = Lod;
    }

    /// <summary>
    /// Bestimmt, ob das aktuelle Objekt mit einem anderen <see cref="ChunkCoord"/> gleich ist.
    /// </summary>
    /// <param name="other">Das Objekt, das mit dem aktuellen Objekt verglichen werden soll.</param>
    /// <returns><c>true</c>, wenn die Objekte gleich sind, andernfalls <c>false</c>.</returns>
    public bool Equals(ChunkCoord other) => X == other.X && Y == other.Y && Z == other.Z && LodLevel == other.LodLevel;
    
    /// <summary>
    /// Bestimmt, ob das aktuelle Objekt mit einem anderen Objekt gleich ist.
    /// </summary>
    /// <param name="obj">Das Objekt, das mit dem aktuellen Objekt verglichen werden soll.</param>
    /// <returns><c>true</c>, wenn die Objekte gleich sind, andernfalls <c>false</c>.</returns>
    public override bool Equals(object? obj) => obj is ChunkCoord other && Equals(other);
    
    /// <summary>
    /// Gibt den Hashcode für dieses <see cref="ChunkCoord"/> zurück.
    /// </summary>
    /// <returns>Ein Hashcode für dieses Objekt.</returns>
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, LodLevel);
    
    /// <summary>
    /// Gibt an, ob zwei <see cref="ChunkCoord"/>-Instanzen gleich sind.
    /// </summary>
    /// <param name="left">Die erste zu vergleichende Instanz.</param>
    /// <param name="right">Die zweite zu vergleichende Instanz.</param>
    /// <returns><c>true</c>, wenn gleich, andernfalls <c>false</c>.</returns>
    public static bool operator ==(ChunkCoord left, ChunkCoord right) => left.Equals(right);
    
    /// <summary>
    /// Gibt an, ob zwei <see cref="ChunkCoord"/>-Instanzen ungleich sind.
    /// </summary>
    /// <param name="left">Die erste zu vergleichende Instanz.</param>
    /// <param name="right">Die zweite zu vergleichende Instanz.</param>
    /// <returns><c>true</c>, wenn ungleich, andernfalls <c>false</c>.</returns>
    public static bool operator !=(ChunkCoord left, ChunkCoord right) => !left.Equals(right);
    
    /// <summary>
    /// Gibt eine Zeichenfolge zurück, die dieses <see cref="ChunkCoord"/> repräsentiert.
    /// </summary>
    /// <returns>Zeichenfolge im Format "(X, Y, Z)".</returns>
    public override string ToString() => $"({X}, {Y}, {Z})";
}