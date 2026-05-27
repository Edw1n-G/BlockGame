namespace Basics.Game.Logic.TerrainManaging;

/// <summary>
/// Benutzt, um Garbage Collector zu entlasten,
/// durch wiederverwendung von buffers
/// TODO noch solche für chunks machen. anzahl kann man ja von render distance berechnen
/// </summary>
public struct PooledMeshBuffer
{
    public uint Vao;
    public uint Vbo;
    public uint Ebo;
    public nuint VboCapacity; // Wie groß auf der Grafikkarte gerade?
    public nuint EboCapacity;
}