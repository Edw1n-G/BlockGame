namespace Basics.Game.Content.Registries;

/// <summary>
/// Die Render-Art eines Blocks. Würfelblöcke laufen über den Greedy-Face-Pfad
/// des Meshers; Model-Blöcke werden dort übersprungen und später über einen
/// separaten Instanced-Pass gezeichnet.
/// </summary>
public enum BlockRenderKind
{
    /// <summary>Nicht renderbar (Luft).</summary>
    None,

    /// <summary>Ein voller Würfel mit sechs Texturseiten.</summary>
    Cube,

    /// <summary>Ein 3D-Modell aus dem ModelCatalog (zukünftige Erweiterung).</summary>
    Model,
}

/// <summary>
/// Die Form eines Blocks — die Nahtstelle für zukünftige 3D-Modelle.
/// Derzeit gibt es nur <see cref="CubeShape"/>; Model-Blöcke kommen hinzu,
/// sobald ein ModelCatalog existiert. Der Mesher entscheidet über
/// <see cref="BlockRenderKind"/>, welchen Render-Pfad ein Block nimmt.
/// </summary>
public abstract record BlockShape
{
    /// <summary>Die Render-Art, die sich aus der Form ergibt.</summary>
    public abstract BlockRenderKind Kind { get; }
}

/// <summary>
/// Ein voller Würfel mit sechs aufgelösten Textur-Layern
/// (Reihenfolge: Top, Bottom, Front, Back, Left, Right — wie <see cref="BlockRegistry"/>-Face-Konstanten).
/// </summary>
public sealed record CubeShape(ushort[] FaceLayers) : BlockShape
{
    /// <inheritdoc />
    public override BlockRenderKind Kind => BlockRenderKind.Cube;
}

/// <summary>
/// Ein Block, der aus einem 3D-Modell gerendert wird (Nahtstelle — der
/// ModelCatalog und der Model-Pass existieren noch nicht).
/// </summary>
/// <param name="Model">Die Ressourcen-ID des Modells.</param>
/// <param name="TextureOverrides">Optionale Textur-Überschreibungen des Modells.</param>
public sealed record ModelShape(ResourceId Model, ResourceId[]? TextureOverrides) : BlockShape
{
    /// <inheritdoc />
    public override BlockRenderKind Kind => BlockRenderKind.Model;
}

/// <summary>
/// Flache, pro Block vorberechnete Flags für den Heißpfad (Mesher):
/// verhindert wiederholte Verzweigungen über die Definitionen.
/// </summary>
/// <param name="IsOpaque">Gibt an, ob der Block undurchsichtig ist (Luft ist nie undurchsichtig).</param>
/// <param name="RenderKind">Die Render-Art des Blocks.</param>
public readonly record struct BlockFlags(bool IsOpaque, BlockRenderKind RenderKind);
