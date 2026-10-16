using System;
using Basics.Game.Logic;

namespace Basics.Game.Logic.TerrainManaging.Generation.Noise;

public class NoiseCalculator : IDisposable
{
    private static readonly Lazy<NoiseCalculator> _fallbackInstance = new(() => new NoiseCalculator());
    public static NoiseCalculator Instance =>
        EngineContext.Current?.NoiseCalculator ?? _fallbackInstance.Value;
    
    private FastNoise? _Node;
    private readonly GameSettings? _settings;
    private readonly string? _customWorldtype;
    private readonly int? _customSeed;
    
    public NoiseCalculator(GameSettings? settings = null)
    {
        _settings = settings;
        SetNoiseParameters();
    }

    public NoiseCalculator(string worldtype, int seed)
    {
        _customWorldtype = worldtype;
        _customSeed = seed;
        SetNoiseParameters();
    }
    
    public void Dispose()
    {
        _Node?.Dispose();
        _Node = null;
    }

    private void SetNoiseParameters()
    {
        string nodeTree = _customWorldtype ?? _settings?.Worldtype ?? GameSettings.Current.Worldtype;
        _Node = FastNoise.FromEncodedNodeTree(nodeTree);
    }

    /// <summary>
    /// Gibt ein sizeX*sizeZ großes Noise-Array zurück.
    /// stepSize bestimmt den Welt-Abstand zwischen zwei benachbarten Array-Einträgen:
    ///   stepSize=1 → deckt sizeX * sizeZ Blöcke ab  (volle Auflösung)
    ///   stepSize=2 → deckt 2*sizeX * 2*sizeZ Blöcke ab (jeder 2. Block)
    ///   stepSize=n → deckt n*sizeX * n*sizeZ Blöcke ab
    /// </summary>
    public float[] GetNoiseValues(int startX, int startY, int startZ, int sizeX, int sizeY, int sizeZ, int stepSize = 1)
    {
        if (_Node == null) SetNoiseParameters();
        if (stepSize < 1) stepSize = 1;

        int totalCount = sizeX * sizeZ * sizeY;
        float[] noiseOutput = new float[totalCount];

        int seed = _customSeed ?? _settings?.Seed ?? GameSettings.Current.Seed;

        _Node!.GenUniformGrid3D(
            noiseOutput,
            startX,
            startY,
            startZ,
            sizeX,
            sizeY,
            sizeZ,
            stepSize,
            stepSize,
            stepSize,
            seed);

        return noiseOutput;
    }
}