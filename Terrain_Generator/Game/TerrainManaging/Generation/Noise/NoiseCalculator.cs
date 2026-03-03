using System;

namespace Basics.Game.TerrainManaging.Generation;

public class NoiseCalculator : IDisposable
{
    private const int Seed = 1337;
    
    // Terrain Config
    private const float BaseHeight = 1f;     // Average height
    private const float Amplitude = 10f;      // Height variation
    
    private int _maxMapSize = 1;
    private int _mapLimit;
    private FastNoise? _fractalNode;
    private FastNoise? _scaleNode;
    
    public void Dispose()
    {
        _fractalNode?.Dispose();
        _scaleNode?.Dispose();
    }

    public void SetMapSize(int size)
    {
        _maxMapSize = size;
        _mapLimit = size / 2;
    }

    private void SetNoiseParameters()
    {
        FastNoise simplex = new FastNoise("Super Simplex");

        // One fractal noise node that has simplex as input
        // FractalRidged creates nice mountain peaks
        _fractalNode = new FastNoise("FractalRidged");
        _fractalNode.Set("Source", simplex);
        _fractalNode.Set("Octaves", 6); // High detail
        _fractalNode.Set("Gain", 0.5f);
        _fractalNode.Set("Lacunarity", 2.0f);
        
        _scaleNode = new FastNoise("Domain Scale");
        _scaleNode.Set("Source", _fractalNode);
        _scaleNode.Set("Scaling", 15f ); // Scale
    }

    public float[] GetNoiseValues(int startX, int startZ, int sizeX, int sizeZ)
    {
        if (_fractalNode == null) SetNoiseParameters();
        
        int totalCount = sizeX * sizeZ;
        float[] xPositions = new float[totalCount];
        float[] yPositions = new float[totalCount];
        float[] zPositions = new float[totalCount];
        float[] wPositions = new float[totalCount];
        float[] output = new float[totalCount];
        
        // Torus Mapping Geometry
        float totalWidth = _maxMapSize; 

        int index = 0;
        for (int z = startZ; z < startZ + sizeZ; z++)
        {
            for (int x = startX; x < startX + sizeX; x++)
            {
                float angleX = (x % _mapLimit) / (float)_mapLimit * 2 * MathF.PI;
                float angleZ = (z % _mapLimit) / (float)_mapLimit * 2 * MathF.PI;

                xPositions[index] = MathF.Cos(angleX);
                yPositions[index] = MathF.Sin(angleX);
                zPositions[index] = MathF.Cos(angleZ);
                wPositions[index] = MathF.Sin(angleZ);
                
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