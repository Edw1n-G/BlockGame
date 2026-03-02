using System;
using System.IO;
using Basics.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Basics.Game.TerrainManaging.Generation;

public class TerrainGenerator
{
    private int _maxMapSize = 1; // Maximale Anzahl von Chunks in x und z Richtung. Dummy wert
    private int _mapLimit;
    private int _radius; //Um 0/0 als Mittelpunkt zu haben
    
    private readonly NoiseCalculator _noiseCalculator = new NoiseCalculator();
    
    /// <summary>
    /// Setzt die Max Größe der Karte
    /// @param mapSize absolute Chunkmenge in x und z
    /// @param Menge der Chunks jeweils in die positive und negative Richtung
    /// @param mapLimit die Grenze der Karte in Blöcken
    /// </summary>
    public void SetMapSize(int size)
    {
        _maxMapSize = size;
        _radius = _maxMapSize/2;
        _mapLimit = _radius * 32;
        _noiseCalculator.SetMapSize(size);

    }
    
    /// <summary>
    /// Bekommt den Index des Chunkes mit 0/0 als Mittelpunkt
    /// rechnet den ChunkIndex in die Weltposition
    /// generiert alles Blöcke des Chunkes mit 4D OpenSimplex Noise
    /// wird in @param ChunkBlocks gespeichert und an den ChunkMesher übergeben, der die Geometrie erstellt
    /// Berechnung der 4D Koordinate abhängig von @param mapLimit, damit die Karte an den Grenzen nahtlos verbunden ist (Torus Mapping)
    /// </summary>
    public byte[] GenerateChunk(ChunkCoord coord)
    {
        if (Math.Abs(coord.X) > _maxMapSize || Math.Abs(coord.Z)+1 > _maxMapSize)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNUNG: Chunk außerhalb der Weltlimits angefragt.");
            Console.ResetColor(); 
        }
        int chunkStartX = coord.X * 32;
        int chunkStartY = coord.Y * 32;
        int chunkStartZ = coord.Z * 32;

        byte[] chunkBlocks = new byte[32*32*32]; // 32x32x32 Blöcke pro Chunk
        
        float[] noiseValues = _noiseCalculator.GetNoiseValues(chunkStartX, chunkStartZ, 32, 32);
        
        //2 Schleifen für alle Blöcke im Chunk
        for (byte blockX = 0; blockX < 32; blockX++)
        {
            for (byte blockZ = 0; blockZ < 32; blockZ++)
            {
                float noiseValue = noiseValues[blockZ * 32 + blockX];
                int height = (int)(noiseValue + 16);
                
                // Clamp height um Kein OutOfBounds zu bekommen
                if (height < 0) height = 0;
                if (height > 31) height = 31;
                for (byte y = 0; y < 32; y++)
                {
                    ushort index = (ushort)(blockX * 32 * 32 + y * 32 + blockZ);
                    if (y <= height)
                    {
                        if (y > 28) 
                        {
                            chunkBlocks[index] = 3; // Schnee auf den höchsten Blöcken
                        }
                        else if (y <= (height - 2) || y > (20)) 
                        {
                            chunkBlocks[index] = 2; // unter Erde ist und mittlere Blöcke als Stein
                        }
                        else
                        {
                            chunkBlocks[index] = 1; // Mitlere Blöcke als Erde
                        }
                    }
                    else
                    {
                        chunkBlocks[index] = 0;
                    }
                }
            }
        }

        return chunkBlocks;
    }
    
    public void DebugExportNoiseMap(string filename = "debug_noisemap.png")
    {
        int totalwidth = _mapLimit * 2;
        float[] noiseValues = _noiseCalculator.GetNoiseValues(-_mapLimit, -_mapLimit, totalwidth, totalwidth);
        
        // Bitmap erstellen
        using (Image<Rgba32> image = new Image<Rgba32>(totalwidth, totalwidth))
        {
            for (int x = 0; x < totalwidth; x++)
            {
                for (int z = 0; z < totalwidth; z++)
                {
                    int index = z * totalwidth + x;
                    
                    float noiseValue = noiseValues[index];
                    int height = (int)(noiseValue + 16);
                    
                    // Clamp Visualisierung (Rot = Fehler unter 0, Blau = Fehler über 31)
                    Rgba32 pixelColor;
                    if (height < 0) 
                        pixelColor = new Rgba32(255, 0, 0);
                    else if (height > 31) 
                        pixelColor = new Rgba32(0, 0, 255);
                    else
                    {
                        // Graustufen basierend auf Höhe (0..31 auf 0..255 mappen)
                        int grayValue = (int)((height / 31.0f) * 255);
                         pixelColor = new Rgba32((byte)grayValue, (byte)grayValue, (byte)grayValue);
                    }

                    image[x, z] = pixelColor;
                }
            }
        
            image.SaveAsPng(filename);
            Console.WriteLine($"Noise Map gespeichert unter: {Path.GetFullPath(filename)}");
        }
    }
}