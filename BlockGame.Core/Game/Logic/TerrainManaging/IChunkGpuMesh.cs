namespace Basics.Game.Logic.TerrainManaging;

/// <summary>Handle to an uploaded chunk mesh.</summary>
public interface IChunkGpuMesh : IDisposable
{
    bool CanReuse { get; }

    /// <summary>Releases the GPU buffers. Must run on the main thread.</summary>
    new void Dispose();
}
