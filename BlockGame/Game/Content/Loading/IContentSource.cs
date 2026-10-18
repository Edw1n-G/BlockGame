using Basics.Game.Content.Registries;

namespace Basics.Game.Content.Loading;

/// <summary>
/// Die Art eines Content-Eintrags. Weitere Arten (Model, Item) kommen hinzu,
/// wenn die entsprechenden Registrierungs-Stufen im ContentLoader entstehen.
/// </summary>
public enum ContentKind
{
    Texture,
    Block,
}

/// <summary>
/// Ein einzelner Content-Eintrag: geparste Ressourcen-ID, Quelldatei (für
/// Fehlermeldungen) und ein verzögerter Lesezugriff auf den Inhalt.
/// Für Dateisystem-Quellen liest <see cref="ReadText"/> die Datei; Tests
/// liefern In-Memory-Quellen ohne Dateizugriff.
/// </summary>
public sealed class ContentEntry
{
    /// <summary>Die normalisierte Ressourcen-ID des Eintrags.</summary>
    public required ResourceId Id { get; init; }

    /// <summary>Der Pfad bzw. Quellenname, für aussagekräftige Fehlermeldungen.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Die Art des Eintrags (Textur, Block, ...).</summary>
    public required ContentKind Kind { get; init; }

    /// <summary>
    /// Liest den Textinhalt des Eintrags. Für Texturen wird dies nie aufgerufen
    /// (Texturen werden nur über ihre Pfade katalogisiert).
    /// </summary>
    public required Func<string> ReadText { get; init; }
}

/// <summary>
/// Abstraktion über die Content-Quelle: verbirgt das Dateisystem hinter einer
/// Aufzählung von Einträgen. Tests liefern In-Memory-Quellen und brauchen
/// dadurch weder Fenster, GPU noch Festplatte.
/// </summary>
public interface IContentSource
{
    /// <summary>Enumeriert alle Einträge einer Art in beliebiger Reihenfolge.</summary>
    IEnumerable<ContentEntry> Enumerate(ContentKind kind);
}
