using Basics.Blocks;
using Basics.Common;
using Basics.Engine;
using Basics.Terrain.Generation.Noise;
using Basics.Settings;
using System.Buffers;

namespace Basics.Terrain.Generation;

/// <summary>
/// Generiert Terraindaten für Chunks mithilfe von 3D-Noise-Algorithmus.
/// Wird im Hintergrund von Worker-Threads ausgeführt und erstellt <see cref="ChunkData"/> für den Meshing-Prozess.
/// </summary>
public class TerrainGenerator
{
    private readonly NoiseCalculator _noiseCalculator;
    private readonly GameSettings _settings;
    private readonly ushort _dirtId;
    private readonly ushort _grassId;
    private readonly ushort _stoneId;
    private readonly ushort _snowId;
    
    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="TerrainGenerator"/>.
    /// </summary>
    /// <param name="noiseCalculator">Der für den Noise-Generierung zu verwendende <see cref="NoiseCalculator"/>. Falls nicht angegeben, wird der aktuelle Context verwendet.</param>
    /// <param name="settings">Die für die Generierung zu verwendenden <see cref="GameSettings"/>. Falls nicht angegeben, werden die aktuellen Settings verwendet.</param>
    /// <param name="blockRegistry">Das für die Block-ID-Auflösung zu verwendende <see cref="BlockRegistry"/>. Falls nicht angegeben, wird der aktuelle Context verwendet.</param>
    public TerrainGenerator(
        NoiseCalculator? noiseCalculator = null,
        GameSettings? settings = null,
        BlockRegistry? blockRegistry = null)
    {
        _noiseCalculator = noiseCalculator ?? EngineContext.Current?.NoiseCalculator ?? NoiseCalculator.Instance;
        _settings = settings ?? EngineContext.Current?.Settings ?? GameSettings.Current;

        BlockRegistry registry = blockRegistry ?? EngineContext.Current?.BlockRegistry ?? BlockDefinitionLoader.Current;
        _dirtId = registry.GetBlockId("core:dirt");
        _grassId = registry.GetBlockId("core:grass");
        _stoneId = registry.GetBlockId("core:stone");
        _snowId = registry.GetBlockId("core:snow");
    }
    
    /// <summary>
    /// Generiert alle Blöcke für einen Chunk basierend auf 3D-Noise-Werten.
    /// </summary>
    /// <param name="coord">Die Koordinaten des zu generierenden Chunks.</param>
    /// <returns>Ein <see cref="ChunkData"/>-Objekt mit den generierten Blockdaten.</returns>
    /// <remarks>
    /// Wenn der Chunk außerhalb der Map-Größe liegt, wird ein leerer Chunk (nur Luft) zurückgegeben.
    /// Die Methode wird im Hintergrund von Worker-Threads ausgeführt und ist nicht Thread-sicher für GPU-Interaktionen.
    /// </remarks>
    public ChunkData GenerateChunk(ChunkCoord coord)
    {
        //Außerhalb der map
        int mapSize = _settings.MapSize;
        if (Math.Abs(coord.X) > mapSize || Math.Abs(coord.Z) > mapSize)
        {
            return new ChunkData(coord); // Alles Luft damit nachbarn gemesht werden können;
        }

        int stepSize = (1 << coord.LodLevel); // lod0 -> 1, lod1 -> 2, lod2 -> 4, lod3 -> 8,lod4 -> 16
        int chunkStartX = coord.X * stepSize * 16;
        int chunkStartY = coord.Y * stepSize * 16;
        int chunkStartZ = coord.Z * stepSize * 16;
        int chunkTopY = chunkStartY + stepSize - 1; // Oberster Block im Chunk (skaliert nach LOD)

        ushort[] chunkBlocks;
        
        // +1 in Y für robuste Surface-Erkennung (Block über current)
        const int noiseSizeX = 16;
        const int noiseSizeY = 17;
        const int noiseSizeZ = 16;
        const int densityCount = noiseSizeX * noiseSizeY * noiseSizeZ;
        float[] densityField = ArrayPool<float>.Shared.Rent(densityCount);
        try
        {
            _noiseCalculator.FillNoiseValues(
                densityField.AsSpan(0, densityCount),
                chunkStartX,
                chunkStartY,
                chunkStartZ,
                noiseSizeX,
                noiseSizeY,
                noiseSizeZ,
                stepSize);

            // Ignore the extra Y plane: it is sampled only for surface detection.
            // Exit as soon as a solid voxel is found, keeping the usual nonempty
            // generation loop free of a per-voxel allocation check.
            if (!HasSolidVoxel(densityField.AsSpan(0, densityCount))) return new ChunkData(coord);
            chunkBlocks = new ushort[ChunkData.BlockCount];

            for (byte x = 0; x < 16; x++)
            {
                for (byte z = 0; z < 16; z++)
                {
                    for (byte y = 0; y < 16; y++)
                    {
                        int globalY = chunkStartY + y * stepSize;

                        int blockIndex = x * 256 + y * 16 + z;

                        int densityIndex = x + y * noiseSizeX + z * noiseSizeX * noiseSizeY;
                        int aboveIndex = densityIndex + noiseSizeX;

                        float density = densityField[densityIndex];
                        float densityAbove = densityField[aboveIndex];

                        if (density > 0)
                        {
                            bool isTopSolidBlock = densityAbove <= 0;
                            bool isNearSurface = isTopSolidBlock || density <= 4 * stepSize;

                            if (globalY > 80 && isTopSolidBlock)
                            {
                                chunkBlocks[blockIndex] = _snowId;
                            }
                            else if (isTopSolidBlock)
                            {
                                chunkBlocks[blockIndex] = _grassId;
                            }
                            else if (isNearSurface)
                            {
                                chunkBlocks[blockIndex] = _dirtId;
                            }
                            else
                            {
                                chunkBlocks[blockIndex] = _stoneId;
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            // No clearing needed: every value is overwritten before the next read.
            ArrayPool<float>.Shared.Return(densityField);
        }
        
        
        return new ChunkData(coord, chunkBlocks);
    }

    // Density layout is x + y * 16 + z * 16 * 17, including one halo Y plane.
    internal static bool HasSolidVoxel(ReadOnlySpan<float> densityField)
    {
        for (int z = 0; z < 16; z++)
        {
            int start = z * 16 * 17;
            for (int i = start; i < start + ChunkData.ChunkArea; i++)
                if (densityField[i] > 0) return true;
        }
        return false;
    }
    
}
