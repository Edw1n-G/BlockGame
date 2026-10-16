namespace Basics.Game.Logic.TerrainManaging;

/// <summary>
/// Struktur für wiederverwendete GPU-Buffer (VBO/EBO), um den Garbage Collector zu entlasten.
/// </summary>
/// <remarks>
/// Wird verwendet, um Buffer-Ressourcen zwischen Meshings zu cachen und wiederzuverwenden.
/// Die Kapazität der Buffer auf der Grafikkarte wird in <see cref="VboCapacity"/> und <see cref="EboCapacity"/> gespeichert.
/// </remarks>
public struct PooledMeshBuffer
{
    /// <summary>Das Vertex Array Object Handle.</summary>
    public uint Vao;
    /// <summary>Das Vertex Buffer Object Handle.</summary>
    public uint Vbo;
    /// <summary>Das Element Buffer Object Handle.</summary>
    public uint Ebo;
    /// <summary>Die aktuelle Kapazität des VBO auf der Grafikkarte in Bytes.</summary>
    public nuint VboCapacity;
    /// <summary>Die aktuelle Kapazität des EBO auf der Grafikkarte in Bytes.</summary>
    public nuint EboCapacity;
}