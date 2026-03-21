using System;
using System.Buffers;

namespace Basics.Game.TerrainManaging.Generation;

public class NoiseCalculator : IDisposable
{
    // Terrain Config
    private const float BaseHeight = 1f;     // Average height
    private const float Amplitude = 70f;    // Height Multiplier
    private const float FeatureScale = 9f; // Detail. je kleiner, desto mehr Details, aber auch mehr Rechenzeit
    private const float Scale = 20f;         // End result scale
    
    //Das soll vieleicht maybe vehindern das jeder Chunk seine eigene Instanz von NoiseCalculator erstellt
    private static readonly Lazy<NoiseCalculator> _instance = new(() => new NoiseCalculator());
    public static NoiseCalculator Instance => _instance.Value;
    
    private FastNoise? _superSimplexNode;
    private FastNoise? _domainWarpSuperSimplexNode;
    private FastNoise? _domainWarpFractalProgressiveNode;
    private FastNoise? _scaleNode;
    
    private NoiseCalculator()
    {
        SetNoiseParameters();
    }
    
    public void Dispose()
    {
        _superSimplexNode.Dispose();
        _domainWarpSuperSimplexNode.Dispose();
        _domainWarpFractalProgressiveNode.Dispose();
        _scaleNode?.Dispose();
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

    /// <summary>
    /// Gibt ein sizeX*sizeZ großes Noise-Array zurück.
    /// stepSize bestimmt den Welt-Abstand zwischen zwei benachbarten Array-Einträgen:
    ///   stepSize=1 → deckt sizeX * sizeZ  Blöcke ab  (volle Auflösung)
    ///   stepSize=2 → deckt 2*sizeX * 2*sizeZ Blöcke ab (jeder 2. Block)
    ///   stepSize=n → deckt n*sizeX * n*sizeZ Blöcke ab
    /// </summary>
    public float[] GetNoiseValues(int startX, int startZ, int sizeX, int sizeZ, int stepSize = 1)
    {
        if (_scaleNode == null) SetNoiseParameters();
        if (stepSize < 1) stepSize = 1;

        int totalCount = sizeX * sizeZ;
        // ArrayPool damit nicht jedes Array neu erstellt und vom GC gelöscht wird, sondern wiederbenutzen 
        float[] xPositions = ArrayPool<float>.Shared.Rent(totalCount);
        float[] yPositions = ArrayPool<float>.Shared.Rent(totalCount);
        float[] zPositions = ArrayPool<float>.Shared.Rent(totalCount);
        float[] wPositions = ArrayPool<float>.Shared.Rent(totalCount);
        
        // Torus-Breite in Blöcken
        float totalWidth = GameSettings.MapSize * 32;

        int index = 0;
        for (int zi = 0; zi < sizeZ; zi++)
        {
            for (int xi = 0; xi < sizeX; xi++)
            {
                // Jeden stepSize-ten Block in der Welt samplen
                int x = startX + xi * stepSize;
                int z = startZ + zi * stepSize;

                float shiftedX = ((x % totalWidth) + totalWidth) % totalWidth;
                float shiftedZ = ((z % totalWidth) + totalWidth) % totalWidth;

                float angleX = shiftedX / totalWidth * 2.0f * MathF.PI;
                float angleZ = shiftedZ / totalWidth * 2.0f * MathF.PI;

                float xBase = MathF.Cos(angleX);
                float yBase = MathF.Sin(angleX);
                float zBase = MathF.Cos(angleZ);
                float wBase = MathF.Sin(angleZ);

                xPositions[index] = xBase * 0.866f + zBase * 0.5f  + 10.5f;
                yPositions[index] = yBase * 0.965f + wBase * 0.258f + 12.3f;
                zPositions[index] = zBase * 0.866f - xBase * 0.5f  -  8.7f;
                wPositions[index] = wBase * 0.965f - yBase * 0.258f - 15.2f;

                index++;
            }
        }

        float[] noiseOutput = new float[totalCount];
        _scaleNode!.GenPositionArray4D(noiseOutput, xPositions, yPositions, zPositions, wPositions,
                                                    0, 0, 0, 0, GameSettings.Seed);

        for (int i = 0; i < totalCount; i++)
            noiseOutput[i] = BaseHeight + noiseOutput[i] * Amplitude;
        
        ArrayPool<float>.Shared.Return(xPositions);
        ArrayPool<float>.Shared.Return(yPositions);
        ArrayPool<float>.Shared.Return(zPositions);
        ArrayPool<float>.Shared.Return(wPositions);
        
        return noiseOutput;
    }
    
    public float[] GetCaves3D(int startX, int startY, int startZ, int sizeX, int sizeY, int sizeZ)
    {
        // No caves requested right now
        return new float[sizeX * sizeY * sizeZ]; 
    }
    
}