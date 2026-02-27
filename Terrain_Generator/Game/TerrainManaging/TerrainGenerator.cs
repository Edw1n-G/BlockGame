using System.Drawing;
using System.Drawing.Imaging;
using Basics.Graphics;
using Basics.Utilities;

namespace Basics.Game.TerrainManaging;

public class TerrainGenerator
{
    private const int _seed = 1223456789;
    private const float _step = 5f; // Schrittweite für die Noise-Abtastung, je kleiner desto detaillierter aber auch rechenintensiver
    private const float _scale = 25.0f;
    private int _maxMapSize = 1; // Maximale Anzahl von Chunks in x und z Richtung. Dummy wert
    private int _mapLimit;
    private int _radius; //Um 0/0 als Mittelpunkt zu haben
    
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

    }
    
    /**
     * Bekommt den Index des Chunkes mit 0/0 als Mittelpunkt
     * rechnet den ChunkIndex in die Weltposition
     * generiert alles Blöcke des Chunkes mit 4D OpenSimplex Noise
     * wird in @param ChunkBlocks gespeichert und an den ChunkMesher übergeben, der die Geometrie erstellt
     * Berechnung der 4D Koordinate abhängig von @param mapLimit, damit die Karte an den Grenzen nahtlos verbunden ist (Torus Mapping)
     */
    public ChunkMesher GenerateChunk(ChunkCoord coord)
    {
        if (Math.Abs(coord.X) > _maxMapSize || Math.Abs(coord.Z)+1 > _maxMapSize)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNUNG: Chunk außerhalb der Weltlimits angefragt.");
            Console.ResetColor(); 
        }
        int chunkStartX = coord.X * 32;
        int chunkStartY = coord.Y * 62;
        int chunkStartZ = coord.Z * 32;
        
        int[,,] chunkBlocks = new int[32, 32, 32];
        
        //2 Schleifen für alle Blöcke im Chunk
        for (int blockX = 0; blockX < 32; blockX++)
        {
            for (int blockZ = 0; blockZ < 32; blockZ++)
            {
                // Weltkoordinaten des Blocks berechnen
                int x = chunkStartX + blockX;
                int z = chunkStartZ + blockZ;
                
                // Winkel für die Position auf dem Torus berechnen
                // Weltkoordinate des Blocks + äußerste Grenze der Karte geteilt durch die gesamte Breite der Karte (2*mapLimit) mal 2*PI für den Winkel
                double angleX = (x + _mapLimit) / (double)(2 * _mapLimit) * 2.0 * Math.PI;
                double angleZ = (z + _mapLimit) / (double)(2 * _mapLimit) * 2.0 * Math.PI;
                // Die 4D Koordinaten berechnen (Torus mapping)
                double x4 = _step * Math.Sin(angleX);
                double y4 = _step * Math.Cos(angleX);
                double z4 = _step * Math.Sin(angleZ);
                double w4 = _step * Math.Cos(angleZ);
                double noiseValue = OpenSimplex2S.Noise4_Fallback(_seed, x4, y4, z4, w4);
                int height = (int)(noiseValue * _scale + 16); // +16 mitte des Chunks
                // Clamp height um Kein OutOfBounds zu bekommen
                if (height < 0) height = 0;
                if (height > 31) height = 31;
                for (int y = 0; y < 32; y++)
                {
                    if (y <= height)
                    {
                        if (y > 28) 
                        {
                            chunkBlocks[blockX, y, blockZ] = 3; // Schnee auf den höchsten Blöcken
                        }
                        else if (y <= (height - 2) || y > (20)) 
                        {
                            chunkBlocks[blockX, y, blockZ] = 2; // unter Erde ist und mittlere Blöcke als Stein
                        }
                        else
                        {
                            chunkBlocks[blockX, y, blockZ] = 1; // Mitlere Blöcke als Erde
                        }
                    }
                    else
                    {
                        chunkBlocks[blockX, y, blockZ] = 0;
                    }
                }
            }
        }
        return new ChunkMesher(Renderer.gl, new ChunkCoord(chunkStartX, chunkStartY, chunkStartZ), chunkBlocks);
    }
    
    public void DebugExportNoiseMap(string filename = "debug_noisemap.png")
    {
        int totalwidth = _mapLimit * 2;
        // Bitmap erstellen
        using (Bitmap bmp = new Bitmap(totalwidth, totalwidth))
        {
            for (int x = 0; x < totalwidth; x++)
            {
                for (int z = 0; z < totalwidth; z++)
                {
                    // Gleiche Mathematik wie im Generator kopieren, um exakt das gleiche Ergebnis zu prüfen
                    double angleX = (double)x / _mapLimit * 2.0 * Math.PI;
                    double angleZ = (double)z / _mapLimit * 2.0 * Math.PI;

                    double x4 = _radius * Math.Sin(angleX);
                    double y4 = _radius * Math.Cos(angleX);
                    double z4 = _radius * Math.Sin(angleZ);
                    double w4 = _radius * Math.Cos(angleZ);

                    double noiseValue = OpenSimplex2S.Noise4_Fallback(_seed, x4, y4, z4, w4);
                
                    // Height Berechnung exakt wie im Code
                    int height = (int)(noiseValue * _scale + 16);

                    // Clamp Visualisierung (Rot = Fehler unter 0, Blau = Fehler über 31)
                    Color pixelColor;
                    if (height < 0) 
                        pixelColor = Color.Red; 
                    else if (height > 31) 
                        pixelColor = Color.Blue;
                    else
                    {
                        // Graustufen basierend auf Höhe (0..31 auf 0..255 mappen)
                        int grayValue = (int)((height / 31.0f) * 255);
                        pixelColor = Color.FromArgb(grayValue, grayValue, grayValue);
                    }

                    bmp.SetPixel(x, z, pixelColor);
                }
            }
        
            bmp.Save(filename, ImageFormat.Png);
            Console.WriteLine($"Noise Map gespeichert unter: {Path.GetFullPath(filename)}");
        }
    }
}