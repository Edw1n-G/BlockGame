using System;
using System.IO;
using Basics.Configurations;
using Basics.Game.Content.Registries;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging.Generation.Noise;
using Basics.Game.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Basics.Game.Logic.TerrainManaging.Generation;

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

        BlockRegistry registry = blockRegistry ?? EngineContext.Current?.BlockRegistry ?? BlockLoader.Current;
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

        ushort[] chunkBlocks = new ushort[4096];
        
        // +1 in Y für robuste Surface-Erkennung (Block über current)
        const int noiseSizeX = 16;
        const int noiseSizeY = 17;
        const int noiseSizeZ = 16;
        float[] densityField = _noiseCalculator.GetNoiseValues(
            chunkStartX,
            chunkStartY,
            chunkStartZ,
            noiseSizeX,
            noiseSizeY,
            noiseSizeZ,
            stepSize);

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
                    else
                    {
                        chunkBlocks[blockIndex] = 0;
                    }
                }
            }
        }
        
        
        // Wenn der Chunkgenerator null zurückgibt ist der Chunk nur Luft oder
        // nicht in der Welt. Trotzdem speichern, damit Nachbar-Chunks gemesht werden können
        ChunkData chunkData;
        if (chunkBlocks == null)
        {
            chunkData = new ChunkData(coord); // Alles Luft
        }
        else
        {
            chunkData = new ChunkData(coord, chunkBlocks);
        }
        
        return chunkData;
    }
    
    //not usable because of 3d noise change
    // TODO: figure out how to make a 2D map out of 3D data
    public void DebugExportNoiseMap(string filename = "debug_noisemap.png", int steps = 16)
    {
        int totalwidth = _settings.MapSize * 16;
        float[] noiseValues = _noiseCalculator.GetNoiseValues(1, 1, 1, 1, 1, 1);
        
        float minNoise = noiseValues[0];
        float maxNoise = noiseValues[0];
        foreach (float value in noiseValues)
        {
            if (value < minNoise) minNoise = value;
            if (value > maxNoise) maxNoise = value;
        }
        
        float range = maxNoise - minNoise;
        if (range == 0) range = 1; // null teilen verhindern
        
        Console.WriteLine($"Noise Map Stats: Min={minNoise:F2}, Max={maxNoise:F2}, Range={range:F2}, Steps={steps}");
        
        // Bitmap erstellen
        using (Image<Rgba32> image = new Image<Rgba32>(totalwidth, totalwidth))
        {
            for (int x = 0; x < totalwidth; x++)
            {
                for (int z = 0; z < totalwidth; z++)
                {
                    int index = z * totalwidth + x;
                    
                    // Normalisieren auf 0..1 basierend auf min/max
                    float normalized = (noiseValues[index] - minNoise) / range;
                    
                    // In diskrete Stufen (Steps) quantisieren
                    // z.B. bei 16 steps: 0.0, 0.0625, 0.125, ... 1.0
                    float stepped = MathF.Floor(normalized * steps) / steps;
                    
                    // Werte nahe -1 (Min) → weiß (255), Werte nahe 1 (Max) → schwarz (0)
                    byte val = (byte)((1f - stepped) * 255);
                    Rgba32 pixelColor = new Rgba32(val, val, val);
                    
                    image[x, z] = pixelColor;
                }
            }
        
            image.SaveAsPng(filename);
            Console.WriteLine($"Noise Map gespeichert unter: {Path.GetFullPath(filename)}");
        }
    }
}