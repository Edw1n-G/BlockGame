using System;
using Basics.Game.Logic;

namespace Basics.Game.Logic.TerrainManaging.Generation.Noise;

/// <summary>
/// Berechnet und verwaltet Noise-Werte für die Terrain-Generierung mithilfe der FastNoise-Bibliothek.
/// Stellt eine statische Instanz für den Zugriff aus dem aktuellen Engine-Kontext bereit.
/// </summary>
/// <remarks>
/// Der NoiseCalculator wird von Worker-Threads für die Terrain-Generierung verwendet.
/// Er verwaltet einen internen <see cref="FastNoise"/>-Knoten, der aus einem codierten Knotenbaum erstellt wird.
/// </remarks>
public class NoiseCalculator : IDisposable
{
    private static readonly Lazy<NoiseCalculator> _fallbackInstance = new(() => new NoiseCalculator());
    /// <summary>
    /// Gibt die aktuelle Instanz des NoiseCalculators zurück.
    /// Nutzt den <see cref="EngineContext.Current"/> oder die statische Fallback-Instanz.
    /// </summary>
    public static NoiseCalculator Instance =>
        EngineContext.Current?.NoiseCalculator ?? _fallbackInstance.Value;
    
    private FastNoise? _Node;
    private readonly GameSettings? _settings;
    private readonly string? _customWorldtype;
    private readonly int? _customSeed;
    
    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="NoiseCalculator"/>.
    /// </summary>
    /// <param name="settings">Die optionalen <see cref="GameSettings"/> für die Noise-Parameter.</param>
    public NoiseCalculator(GameSettings? settings = null)
    {
        _settings = settings;
        SetNoiseParameters();
    }

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="NoiseCalculator"/> mit benutzerdefinierten Worldtype und Seed.
    /// </summary>
    /// <param name="worldtype">Der codierte Knotenbaum für die Noise-Generierung.</param>
    /// <param name="seed">Der Zufallswert für die Noise-Generierung.</param>
    public NoiseCalculator(string worldtype, int seed)
    {
        _customWorldtype = worldtype;
        _customSeed = seed;
        SetNoiseParameters();
    }
    
    /// <summary>
    /// Gibt den internen <see cref="FastNoise"/>-Knoten frei.
    /// </summary>
    public void Dispose()
    {
        _Node?.Dispose();
        _Node = null;
    }

    /// <summary>
    /// Setzt die Noise-Parameter basierend auf den konfigurierten Einstellungen.
    /// Lädt den FastNoise-Knoten aus dem kodierten Knotenbaum.
    /// </summary>
    private void SetNoiseParameters()
    {
        string nodeTree = _customWorldtype ?? _settings?.Worldtype ?? GameSettings.Current.Worldtype;
        _Node = FastNoise.FromEncodedNodeTree(nodeTree);
    }

    /// <summary>
    /// Gibt ein array mit Noise-Werten für den angegebenen Bereich zurück.
    /// </summary>
    /// <param name="startX">Die X-Startkoordinate im Welt-Grid.</param>
    /// <param name="startY">Die Y-Startkoordinate im Welt-Grid.</param>
    /// <param name="startZ">Die Z-Startkoordinate im Welt-Grid.</param>
    /// <param name="sizeX">Die Anzahl der Werte in X-Richtung.</param>
    /// <param name="sizeY">Die Anzahl der Werte in Y-Richtung.</param>
    /// <param name="sizeZ">Die Anzahl der Werte in Z-Richtung.</param>
    /// <param name="stepSize">Der Abstand zwischen zwei benachbarten Array-Einträgen (Standard: 1).</param>
    /// <returns>Ein Array mit Noise-Werten der Größe sizeX*sizeY*sizeZ.</returns>
    /// <remarks>
    /// stepSize=1 → deckt sizeX * sizeY * sizeZ Blöcke ab (volle Auflösung)
    /// stepSize=2 → deckt 2*sizeX * 2*sizeY * 2*sizeZ Blöcke ab (jeder 2. Block)
    /// stepSize=n → deckt n*sizeX * n*sizeY * n*sizeZ Blöcke ab
    /// </remarks>
    public float[] GetNoiseValues(int startX, int startY, int startZ, int sizeX, int sizeY, int sizeZ, int stepSize = 1)
    {
        if (_Node == null) SetNoiseParameters();
        if (stepSize < 1) stepSize = 1;

        int totalCount = sizeX * sizeY * sizeZ;
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