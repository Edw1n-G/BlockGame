
using System;

namespace Basics.Game.TerrainManaging.Generation;

public class NoiseCalculator
{
    private const int Seed = 1223456789;
    private const float Scale = 25f; // "Zoom" der Noise-Textur kleiner = näher, größer = weiter weg
    private const float multiplicator = 13f; // Höhen multiplikator
    private int _maxMapSize = 1;
    private int _mapLimit;
    private int _radius;

    private FastNoise simplex;
    private FastNoise _scalingNode;
    private FastNoise _multiplicatorNode;

    public void SetMapSize(int size)
    {
        _maxMapSize = size;
        _radius = _maxMapSize / 2;
        _mapLimit = _radius * 32;
    }

    private void SetNoiseParameters()
    {
        //FastNoise benutzt Nodes um irgendwas zu berabeiten muss der Noisetype als Quelle für den DomainScale Node gegeben werden
        simplex = new FastNoise("Simplex");

        _scalingNode = new FastNoise("DomainScale");
        _scalingNode.Set("Source", simplex);
        _scalingNode.Set("Scaling", Scale);
        
        _multiplicatorNode = new FastNoise("Multiply");
        _multiplicatorNode.Set("LHS", _scalingNode);
        _multiplicatorNode.Set("RHS", multiplicator);    
        
    }

    public float[] GetNoiseValues(int startX, int startZ, int sizeX, int sizeZ)
    {
        if (_multiplicatorNode == null)
            SetNoiseParameters();

        int totalCount = sizeX * sizeZ;
        float[] xPositions = new float[totalCount];
        float[] yPositions = new float[totalCount];
        float[] zPositions = new float[totalCount];
        float[] wPositions = new float[totalCount];
        float[] output = new float[totalCount];

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

        // Batch-Berechnung nutzt SIMD voll aus
        _multiplicatorNode.GenPositionArray4D(
            output, xPositions, yPositions, zPositions, wPositions, 
            0, 0, 0, 0, Seed);

        return output; // Array mit Noise Werten
    }
}