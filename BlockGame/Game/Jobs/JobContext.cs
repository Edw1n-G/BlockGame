using Basics.Configurations;
using Basics.Game.Configurations;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Generation;

namespace Basics.Game.Utilities;

/// <summary>
/// Enthält den Kontext, der für die Ausführung von Jobs benötigt wird.
/// Vermeidet das Reichen von Referenzen durch den gesamten Job-Stack.
/// </summary>
/// <remarks>
/// Der JobContext enthält Referenzen auf alle notwendigen Subsysteme
/// (TerrainGenerator, ChunkProvider, BlockRegistry), damit Jobs ohne
/// komplexe Parameterübergabe auf diese zugreifen können.
/// </remarks>
public class JobContext
{
    /// <summary>Der für den Job zu verwendende <see cref="TerrainGenerator"/>.</summary>
    public TerrainGenerator TerrainGen { get; }
    /// <summary>Der für den Job zu verwendende <see cref="ChunkProvider"/>.</summary>
    public ChunkProvider Provider { get; }
    /// <summary>Das für den Job zu verwendende <see cref="BlockRegistry"/>.</summary>
    public BlockRegistry BlockRegistry { get; }
    

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="JobContext"/>.
    /// </summary>
    /// <param name="terrainGen">Der für den Job zu verwendende <see cref="TerrainGenerator"/>.</param>
    /// <param name="provider">Der für den Job zu verwendende <see cref="ChunkProvider"/>.</param>
    /// <param name="blockRegistry">Das optionale <see cref="BlockRegistry"/>. Falls nicht angegeben, wird der aktuelle EngineContext verwendet.</param>
    /// <exception cref="InvalidOperationException">Wird ausgelöst, wenn kein <see cref="BlockRegistry"/> gefunden werden kann.</exception>
    public JobContext(TerrainGenerator terrainGen, ChunkProvider provider, BlockRegistry? blockRegistry = null)
    {
        TerrainGen = terrainGen;
        Provider = provider;
        BlockRegistry = blockRegistry ?? EngineContext.Current?.BlockRegistry ?? BlockLoader.Current;
        
        if (BlockRegistry == null)
        {
            throw new InvalidOperationException("JobContext: BlockRegistry konnte nicht gefunden werden.");
        }
    }
}
