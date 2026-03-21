using System;
using System.IO;
using Basics.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Basics.Game.TerrainManaging.Generation;

public class TerrainGenerator
{
    // Theoretische Grenzen aus NoiseCalculator: BaseHeight ± Amplitude
    // Noise liefert Werte im Bereich -1..1, also: Höhe = BaseHeight + noise * Amplitude
    private const float MaxPossibleHeight = 1f + 70f;  // = 41  (BaseHeight + Amplitude)
    private const float MinPossibleHeight = 1f - 70f;  // = -39 (BaseHeight - Amplitude)
    private const float CaveSafetyMargin = 12f;         // 4 Blöcke Schutzzone + 8 Blöcke Übergang
    
    //Anstatt dass jeder Chunk seine eigenen NoiseCalculator erstellt greift jeder Chunk auf eine Instanz zu
    private NoiseCalculator _noiseCalculator => NoiseCalculator.Instance;
    
    /// <summary>
    /// Setzt die Max Größe der Karte
    /// @param mapSize absolute Chunkmenge in x und z
    /// @param Menge der Chunks jeweils in die positive und negative Richtung
    /// @param mapLimit die Grenze der Karte in Blöcken
    /// </summary>
    
    /// <summary>
    /// Bekommt den Index des Chunkes
    /// rechnet den ChunkIndex in die Weltposition
    /// generiert alles Blöcke des Chunkes mit 4D Noise
    /// wird in @param ChunkBlocks gespeichert und an den ChunkMesher übergeben, der die Geometrie erstellt
    /// </summary>
    public byte[] GenerateChunk(ChunkCoord coord)
    {
        if (Math.Abs(coord.X) > GameSettings.MapSize || Math.Abs(coord.Z) > GameSettings.MapSize)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNUNG: Chunk außerhalb der Weltlimits angefragt.");
            Console.ResetColor();
            return null;
        }

        int stepSize = (1 << coord.LodLevel); // lod0 -> 1, lod1 -> 2, lod2 -> 4, lod3 -> 8,lod4 -> 16
        int chunkStartX = coord.X * stepSize * 32;
        int chunkStartY = coord.Y * stepSize * 32;
        int chunkStartZ = coord.Z * stepSize * 32;
        int chunkTopY = chunkStartY + stepSize - 1; // Oberster Block im Chunk (skaliert nach LOD)
    
        byte[] chunkBlocks = new byte[32768]; // 32*32*32 Blöcke pro Chunk
        
        // Chunk-Boden liegt über dem maximal möglichen Terrain
        if (chunkStartY > MaxPossibleHeight)
        {
            return null; // Nichts zurückgeben und den Chunk skippen
        }
        
        // Chunk-Decke liegt unter der oberfläche
        if (chunkTopY < MinPossibleHeight - CaveSafetyMargin)
        {
            Array.Fill(chunkBlocks, (byte)2); // Komplett Stein
            return chunkBlocks;
        }
        
        float[] heightMap = _noiseCalculator.GetNoiseValues(chunkStartX, chunkStartZ, 32, 32, stepSize);
        
        // Herausfinden was der höchste und niedrigste Punkt in dieser Spalte ist
        float maxHeight = float.MinValue;
        float minHeight = float.MaxValue;
        
        
        //float[] caves3D = _noiseCalculator.GetCaves3D(chunkStartX, chunkStartY, chunkStartZ, 32, 32, 32);
        //_noiseCalculator.Dispose();
        
        for (byte x = 0; x < 32; x++)
        {
            for (byte z = 0; z < 32; z++)
            {
                // Die 2D-Basishöhe an dieser X/Z Koordinate
                float baseHeight = heightMap[z * 32 + x];
    
                for (byte y = 0; y < 32; y++)
                {
                    // Wetkoordinate
                    int globalY = chunkStartY + y * stepSize;
                    
                    // Indizes für die Arrays
                    ushort blockIndex = (ushort)(x * 1024 + y * 32 + z);
                    
                    // FastNoise UniformGrid3D index
                    //int noise3DIndex = x + y * 32 + z * 1024; 
                    
                    float density = baseHeight - globalY;
    
                    // Höhlen-Logik anwenden:
                    // Nur unterhalb der Oberfläche Höhlen erlauben (Schutzzone an der Oberfläche)
                    // Je tiefer unter der Oberfläche, desto stärker die Höhlen
                    //float depthBelowSurface = baseHeight - globalY;
                    //float caveAttenuation = MathF.Max(0, MathF.Min(1, (depthBelowSurface - 4f) / 8f));
                    //float cavePower = MathF.Abs(caves3D[noise3DIndex]) * 20f * caveAttenuation;
                    
                    // Wir subtrahieren die Höhlen von unserer Dichte!
                    density -= 0;
                    
                    // BLOCK PLATZIEREN BASIEREND AUF DICHTE
                    if (density > 0)
                    {
                        // Der Block ist solid
                        
                        if (density < 2 && globalY > 30) 
                        {
                            chunkBlocks[blockIndex] = 3; // Schnee (Ganz oben auf den Bergspitzen)
                        }
                        else if (density < 4) 
                        {
                            chunkBlocks[blockIndex] = 1; // Erde (Die obersten 3-4 Blöcke der Oberfläche)
                        }
                        else 
                        {
                            chunkBlocks[blockIndex] = 2; // Stein
                        }
                    }
                    else
                    {
                        // Dichte ist negativ -> Luft!
                        chunkBlocks[blockIndex] = 0;
                    }
                }
            }
        }
    
        return chunkBlocks;
    }
    
    public void DebugExportNoiseMap(string filename = "debug_noisemap.png", int steps = 16)
    {
        int totalwidth = GameSettings.MapSize * 32;
        float[] noiseValues = _noiseCalculator.GetNoiseValues(-totalwidth/2, -totalwidth/2, totalwidth, totalwidth, 1);
        
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