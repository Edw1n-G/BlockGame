using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Generation;

namespace Basics.Game.Utilities;

/// <summary>
/// Die Jobs müssen wissen welchen code die benutzen sollen um code auszuführen
/// Alle referenzen von den klassen in den Job runterzureichen würde den so groß machen das speichern in der queue inefficient wäre
/// z.B 
/// </summary>
public class JobContext
{
    public TerrainGenerator TerrainGen { get; }
    public ChunkProvider Provider { get; }
    // Hier kannst du später auch BlockRegistry, BiomeManager etc. reinlegen

    public JobContext(TerrainGenerator terrainGen, ChunkProvider provider)
    {
        TerrainGen = terrainGen;
        Provider = provider;
    }
}
