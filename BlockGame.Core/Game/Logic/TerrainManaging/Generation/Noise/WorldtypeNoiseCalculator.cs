namespace Basics.Game.Logic.TerrainManaging.Generation.Noise;

/// <summary>
/// Fassade für NoiseCalculator. Ermöglicht Zugriff auf die Instanz des aktuellen Engine-Kontexts.
/// </summary>
public static class WorldtypeNoiseCalculator
{
    public static NoiseCalculator Instance => NoiseCalculator.Instance;
}
