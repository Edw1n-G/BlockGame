using System;

namespace Basics.Game.TerrainManaging.Generation;

public class NoiseCalculator
{
    private const int Seed = 1223456789;
    
    // Globale Parameter für die Form
    private const float BaseHeight = 16f;      // Wasserlevel / Basis-Höhe
    private const float TerrainScale = 12f;    // Wie stark die Hügel hoch und runter gehen
    private const float RiverWidth = 0.04f;    // Je kleiner, desto schmaler die Flüsse
    
    private int _maxMapSize = 1;
    private int _mapLimit;

    // FastNoise Nodes
    private FastNoise _finalNoiseNode;

    public void SetMapSize(int size)
    {
        _maxMapSize = size;
        _mapLimit = (_maxMapSize / 2) * 32;
    }

    private void SetNoiseParameters()
    {
        // 1. Die Basis-Quelle (Simplex)
        FastNoise simplex = new FastNoise("Simplex");

        // 2. Sanfte Hügel und Täler (FractalFBm)
        FastNoise fbm = new FastNoise("FractalFBm");
        fbm.Set("Source", simplex);
        fbm.Set("Octaves", 4);       // Details
        fbm.Set("Gain", 0.5f);
        fbm.Set("Lacunarity", 2.0f);

        FastNoise scaleBase = new FastNoise("DomainScale");
        scaleBase.Set("Source", fbm);
        scaleBase.Set("Scaling", 1.5f); // Großflächige Hügel

        // 3. Raue Gebirge für Details (FractalRidged)
        FastNoise ridged = new FastNoise("FractalRidged");
        ridged.Set("Source", simplex);
        ridged.Set("Octaves", 5);

        FastNoise scaleRidged = new FastNoise("DomainScale");
        scaleRidged.Set("Source", ridged);
        scaleRidged.Set("Scaling", 3.0f); // Feine Bergstrukturen

        // 4. Mischen! (Add Node)
        // Wir nehmen die sanften Hügel und addieren ein bisschen raues Gebirge obendrauf
        FastNoise addNode = new FastNoise("Add");
        addNode.Set("LHS", scaleBase);
        addNode.Set("RHS", scaleRidged);

        _finalNoiseNode = addNode; // Unser fertiger Bauplan
    }

    public float[] GetNoiseValues(int startX, int startZ, int sizeX, int sizeZ)
    {
        if (_finalNoiseNode == null)
            SetNoiseParameters();

        int totalCount = sizeX * sizeZ;
        float[] xPositions = new float[totalCount];
        float[] yPositions = new float[totalCount];
        float[] zPositions = new float[totalCount];
        float[] wPositions = new float[totalCount];
        
        // Wir speichern uns den Gradienten für den Rand separat ab
        float[] edgeGradients = new float[totalCount];
        float[] output = new float[totalCount];

        int index = 0;
        for (int z = startZ; z < startZ + sizeZ; z++)
        {
            for (int x = startX; x < startX + sizeX; x++)
            {
                // WICHTIG: Sicheres Modulo für negative Koordinaten!
                float modX = ((x % _mapLimit) + _mapLimit) % _mapLimit;
                float modZ = ((z % _mapLimit) + _mapLimit) % _mapLimit;

                // 4D Torus-Mapping (für die Nahtlosigkeit)
                float angleX = (modX / _mapLimit) * 2 * MathF.PI;
                float angleZ = (modZ / _mapLimit) * 2 * MathF.PI;

                xPositions[index] = MathF.Cos(angleX);
                yPositions[index] = MathF.Sin(angleX);
                zPositions[index] = MathF.Cos(angleZ);
                wPositions[index] = MathF.Sin(angleZ);

                // ==========================================
                // DIE INSEL-LOGIK (Rand-Klippen)
                // ==========================================
                // Distanz von der Mitte berechnen (0 = Mitte, 1 = Äußerster Rand)
                float normX = MathF.Abs((modX / _mapLimit) - 0.5f) * 2.0f;
                float normZ = MathF.Abs((modZ / _mapLimit) - 0.5f) * 2.0f;
                
                // Wir nehmen den größeren Wert, damit die Karte quadratisch von Bergen umrandet ist
                float edgeDist = MathF.Max(normX, normZ); 

                // Kurven-Magie: Power 8 sorgt dafür, dass die ersten 90% extrem flach sind 
                // und die letzten 5-10% wie eine Rakete in den Himmel schießen!
                float mountainSpike = MathF.Pow(edgeDist, 8.0f) * 80.0f; // +80 Blöcke hohe Klippen
                
                edgeGradients[index] = mountainSpike;
                index++;
            }
        }

        // SIMD Batch-Berechnung über C++ (Unfassbar schnell)
        _finalNoiseNode.GenPositionArray4D(
            output, xPositions, yPositions, zPositions, wPositions, 
            0, 0, 0, 0, Seed);

        // ==========================================
        // POST-PROCESSING: Flüsse, Seen & Klippen
        // ==========================================
        for (int i = 0; i < totalCount; i++)
        {
            // Der FastNoise Output ist meist zwischen -1.5 und 1.5
            float rawNoise = output[i];
            
            // Flüsse generieren (Magischer Trick: Betrag des Noises)
            // Wenn der Noise genau auf der 0-Linie liegt, graben wir ein tiefes Tal
            float riverFactor = MathF.Abs(rawNoise);
            float riverDepth = 0f;
            if (riverFactor < RiverWidth)
            {
                // Sanftes V-Tal nach unten
                riverDepth = (RiverWidth - riverFactor) * 100f; // Multiplikator für Flusstiefe
            }

            // Finale Höhe zusammensetzen
            float height = BaseHeight 
                         + (rawNoise * TerrainScale) // Natürliche Hügel
                         - riverDepth                // Flüsse abziehen
                         + edgeGradients[i];         // Rand-Berge addieren

            output[i] = height - 16; // Deine Generation in TerrainGenerator.cs addiert +16, deshalb ziehen wir sie hier ab.
        }

        return output;
    }
}