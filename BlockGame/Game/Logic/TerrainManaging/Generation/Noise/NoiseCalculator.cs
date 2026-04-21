namespace Basics.Game.Logic.TerrainManaging.Generation.Noise;

public class NoiseCalculator : IDisposable
{
    //Das soll vieleicht maybe vehindern das jeder Chunk seine eigene Instanz von NoiseCalculator erstellt
    private static readonly Lazy<NoiseCalculator> _instance = new(() => new NoiseCalculator());
    public static NoiseCalculator Instance => _instance.Value;
    
    private FastNoise? _Node;
    
    //TODO: mehrere noise maps generieren
    //TODO: density anstatt absolut y benutzen für höhlen
    //TODO: 3d terrain und am weltrand ein chunk als übergang interpoliert plazieren
    //TODO: setting menü für nodes values mit maybe 2d map preview
    
    private NoiseCalculator()
    {
        SetNoiseParameters();
    }
    
    public void Dispose()
    {
        _Node?.Dispose(); ;
    }

    private void SetNoiseParameters()
    {
        string nodeTree = GameSettings.Worldtype;
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
            GameSettings.Seed);

        return noiseOutput;
    }
}