namespace Basics.Game.Logic.TerrainManaging;

/// <summary>
/// Benutzt um garbage collector zu entlasten
/// durch wiederverwendung von buffers
/// </summary>
public struct PooledMeshBuffer
{
    public uint Vao;
    public uint Vbo;
    public uint Ebo;
    public nuint VboCapacity; // Wie groß ist dieser VBO auf der Grafikkarte gerade?
    public nuint EboCapacity;
}