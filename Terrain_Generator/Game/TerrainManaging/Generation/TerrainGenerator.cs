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
    
    // Theoretische Grenzen aus NoiseCalculator: BaseHeight ± Amplitude
    // Noise liefert Werte im Bereich -1..1, also: Höhe = BaseHeight + noise * Amplitude
    private const float MaxPossibleHeight = 1f + 40f;  // = 41  (BaseHeight + Amplitude)
    private const float MinPossibleHeight = 1f - 40f;  // = -39 (BaseHeight - Amplitude)
    private const float CaveSafetyMargin = 12f;         // 4 Blöcke Schutzzone + 8 Blöcke Übergang
    
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
        _mapLimit = _radius;
        _noiseCalculator.SetMapSize(size);

    }
    
    /// <summary>
    /// Bekommt den Index des Chunkes mit 0/0 als Mittelpunkt
    /// rechnet den ChunkIndex in die Weltposition
    /// generiert alles Blöcke des Chunkes mit 4D Noise
    /// wird in @param ChunkBlocks gespeichert und an den ChunkMesher übergeben, der die Geometrie erstellt
    /// Berechnung der 4D Koordinate abhängig von @param mapLimit, damit die Karte an den Grenzen nahtlos verbunden ist (Torus Mapping)
    /// </summary>
    public byte[] GenerateChunk(ChunkCoord coord)
    {
        if (Math.Abs(coord.X) > _maxMapSize || Math.Abs(coord.Z) > _maxMapSize)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNUNG: Chunk außerhalb der Weltlimits angefragt.");
            Console.ResetColor();
            return new byte[32 * 32 * 32]; // Leerer Chunk (nur Luft)
        }
        
        int chunkStartX = coord.X * 32;
        int chunkStartY = coord.Y * 32;
        int chunkStartZ = coord.Z * 32;
        int chunkTopY = chunkStartY + 31; // Oberster Block im Chunk
    
        byte[] chunkBlocks = new byte[32 * 32 * 32];
        
        // Chunk-Boden liegt über dem maximal möglichen Terrain
        if (chunkStartY > MaxPossibleHeight)
        {
            return chunkBlocks; // Luft zurückgeben
        }
        
        // Chunk-Decke liegt unter der oberfläche
        if (chunkTopY < MinPossibleHeight - CaveSafetyMargin)
        {
            Array.Fill(chunkBlocks, (byte)2); // Komplett Stein
            return chunkBlocks;
        }
        
        float[] heightMap = _noiseCalculator.GetNoiseValues(chunkStartX, chunkStartZ, 32, 32);
        
        // Herausfinden was der höchste und niedrigste Punkt in dieser Spalte ist
        float maxHeight = float.MinValue;
        float minHeight = float.MaxValue;
        
        // ======================================================================
        // Ab hier ist der Chunk in dem Bereich zwischen min und max der Heightmap
        // ======================================================================
        float[] caves3D = _noiseCalculator.GetCaves3D(chunkStartX, chunkStartY, chunkStartZ, 32, 32, 32);
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
                    int globalY = chunkStartY + y;
                    
                    // Indizes für die Arrays
                    ushort blockIndex = (ushort)(x * 32 * 32 + y * 32 + z);
                    
                    // FastNoise UniformGrid3D index
                    int noise3DIndex = x + y * 32 + z * 32 * 32; 
                    
                    float density = baseHeight - globalY;
    
                    // Höhlen-Logik anwenden:
                    // Nur unterhalb der Oberfläche Höhlen erlauben (Schutzzone an der Oberfläche)
                    // Je tiefer unter der Oberfläche, desto stärker die Höhlen
                    float depthBelowSurface = baseHeight - globalY;
                    float caveAttenuation = MathF.Max(0, MathF.Min(1, (depthBelowSurface - 4f) / 8f));
                    float cavePower = MathF.Abs(caves3D[noise3DIndex]) * 20f * caveAttenuation;
                    
                    // Wir subtrahieren die Höhlen von unserer Dichte!
                    density -= cavePower;
    
                    // ==========================================
                    // BLOCK PLATZIEREN BASIEREND AUF DICHTE
                    // ==========================================
                    if (density > 0)
                    {
                        // Der Block ist solide Materie! 
                        // Je nachdem wie tief wir unter der Oberfläche sind (Dichte), wählen wir den Block:
                        
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
                            chunkBlocks[blockIndex] = 2; // Stein (Tief im Inneren der Berge)
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
        int totalwidth = _mapLimit * 2 * 32;
        float[] noiseValues = _noiseCalculator.GetNoiseValues(-_mapLimit*32, -_mapLimit*32, totalwidth, totalwidth);
        
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