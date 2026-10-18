namespace Basics.Game.Content.Registries;

/// <summary>
/// Unveränderliche Daten eines Blocks: die aufgelösten Textur-Layer als Form,
/// plus die Tags. Reichhaltige Daten für Erweiterungen; der Heißpfad (Mesher)
/// nutzt die flachen Tabellen der Registry (<see cref="BlockRegistry.FaceLayers"/>,
/// <see cref="BlockRegistry.Flags"/>), die aus diesen Definitionen gebaut werden.
/// </summary>
public sealed class BlockDefinition
{
    /// <summary>Die Ressourcen-ID des Blocks (z. B. "core:stone").</summary>
    public ResourceId Id { get; }

    /// <summary>Die Form des Blocks mit aufgelösten Daten (derzeit immer CubeShape).</summary>
    public BlockShape Shape { get; }

    /// <summary>Die normalisierten Tags des Blocks.</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Erstellt eine neue Block-Definition.
    /// </summary>
    /// <param name="id">Die Ressourcen-ID des Blocks.</param>
    /// <param name="shape">Die Form mit aufgelösten Daten.</param>
    /// <param name="tags">Die normalisierten Tags.</param>
    public BlockDefinition(ResourceId id, BlockShape shape, IReadOnlyList<string> tags)
    {
        Id = id;
        Shape = shape;
        Tags = tags;
    }
}
