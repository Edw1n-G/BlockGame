using System;

namespace Basics.Game.TerrainManaging.Generation;

public class NoiseCalculator : IDisposable
{
    private const int Seed = 1337;
    
    // Terrain Config
    private const float BaseHeight = 1f;     // Average height
    private const float Amplitude = 70f;    // Height Multiplier
    private const float FeatureScale = 10f; // Detail. je kleiner, desto mehr Details, aber auch mehr Rechenzeit
    private const float Scale = 23f;         // End result scale
    
    private int _maxMapSize = 1;
    private int _mapLimit;
    private FastNoise? _superSimplexNode;
    private FastNoise? _domainWarpSuperSimplexNode;
    private FastNoise? _domainWarpFractalProgressiveNode;
    private FastNoise? _scaleNode;
    
    public void Dispose()
    {
        _superSimplexNode.Dispose();
        _domainWarpSuperSimplexNode.Dispose();
        _domainWarpFractalProgressiveNode.Dispose();
        _scaleNode?.Dispose();
    }

    public void SetMapSize(int size)
    {
        _maxMapSize = size;
        _mapLimit = size / 2;
    }

    private void SetNoiseParameters()
    {
        _superSimplexNode = new FastNoise("Super Simplex");
        //_superSimplexNode.Set("Seed", Seed); no member for seed ig 
        _superSimplexNode.Set("Feature Scale", FeatureScale);
        
        _domainWarpSuperSimplexNode = new FastNoise("Domain Warp Super Simplex");
        _domainWarpSuperSimplexNode.Set("Source", _superSimplexNode);
        _domainWarpSuperSimplexNode.Set("Warp Amplitude", 50f);
        //_DomainWarpSuperSimplexNode.Set("X Amplitude Scaling", 1f); Sind im Wiki aber
        //_DomainWarpSuperSimplexNode.Set("Y Amplitude Scaling", 1f); werden trotzdem nicht
        //_DomainWarpSuperSimplexNode.Set("Z Amplitude Scaling", 1f); gefunden
        //_DomainWarpSuperSimplexNode.Set("W Amplitude Scaling", 1f);
        _domainWarpSuperSimplexNode.Set("Vectorization Scheme", "Gradient Outer Product"); //"Orthogonal Gradient Matrix" alternativ
        
        _domainWarpFractalProgressiveNode = new FastNoise("Domain Warp Fractal Progressive");
        _domainWarpFractalProgressiveNode.Set("Domain Warp Source", _domainWarpSuperSimplexNode);
        _domainWarpFractalProgressiveNode.Set("Gain", 0.5f);
        _domainWarpFractalProgressiveNode.Set("Lacunarity", 2f);
        _domainWarpFractalProgressiveNode.Set("Octaves", 4);
        _domainWarpFractalProgressiveNode.Set("Weighted Strength", 0f);
        
        _scaleNode = new FastNoise("Domain Scale");
        _scaleNode.Set("Source", _domainWarpFractalProgressiveNode);
        _scaleNode.Set("Scaling", Scale ); // Scale
    }

    public float[] GetNoiseValues(int startX, int startZ, int sizeX, int sizeZ)
    {
        if (_scaleNode == null) SetNoiseParameters();
        
        int totalCount = sizeX * sizeZ;
        float[] xPositions = new float[totalCount];
        float[] yPositions = new float[totalCount];
        float[] zPositions = new float[totalCount];
        float[] wPositions = new float[totalCount];
        
        // Torus Mapping größe chunks * chunkgröße (32) = tatsächliche Breite der Karte in Blöcken
        float totalWidth = _maxMapSize * 32; 

        int index = 0;
        for (int z = startZ; z < startZ + sizeZ; z++)
        {
            for (int x = startX; x < startX + sizeX; x++)
            {
                // 1. Das SICHERE Modulo (Verhindert das Spiegel-Muster bei negativen Koordinaten!)
                // Wir nutzen totalWidth statt _mapLimit für eine saubere Berechnung
                float shiftedX = ((x % totalWidth) + totalWidth) % totalWidth;
                float shiftedZ = ((z % totalWidth) + totalWidth) % totalWidth;
                
                float angleX = (shiftedX / totalWidth) * 2.0f * MathF.PI;
                float angleZ = (shiftedZ / totalWidth) * 2.0f * MathF.PI;

                float xBase = MathF.Cos(angleX);
                float yBase = MathF.Sin(angleX);
                float zBase = MathF.Cos(angleZ);
                float wBase = MathF.Sin(angleZ);
                
                xPositions[index] = xBase * 0.866f + zBase * 0.5f;
                zPositions[index] = zBase * 0.866f - xBase * 0.5f;

                yPositions[index] = yBase * 0.965f + wBase * 0.258f;
                wPositions[index] = wBase * 0.965f - yBase * 0.258f;
                
                xPositions[index] += 10.5f;
                yPositions[index] += 12.3f;
                zPositions[index] -= 8.7f;
                wPositions[index] -= 15.2f;
                
                index++;
            }
        }

        float[] noiseOutput = new float[totalCount];
        // Generate noise
        _scaleNode!.GenPositionArray4D(noiseOutput, xPositions, yPositions, zPositions, wPositions, 0, 0, 0, 0, Seed);

        // Map noise to height
        for (int i = 0; i < totalCount; i++)
        {
            // FractalRidged typically returns -1..1 or 0..1 range. 
            // We scale it to our desired height.
            noiseOutput[i] = BaseHeight + (noiseOutput[i] * Amplitude);
        }

        return noiseOutput;
    }
    
    public float[] GetCaves3D(int startX, int startY, int startZ, int sizeX, int sizeY, int sizeZ)
    {
        // No caves requested right now
        return new float[sizeX * sizeY * sizeZ]; 
    }
    
}