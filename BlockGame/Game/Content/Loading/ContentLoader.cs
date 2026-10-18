using Basics.Game.Content.Registries;

namespace Basics.Game.Content.Loading;

/// <summary>
/// Orchestriert den Ladevorgang des Contents und baut alle Registries.
/// Die Lade-Reihenfolge folgt den Abhängigkeiten: Texturen → Blöcke (→ später
/// Models → Items). Jede Stufe erhält die fertigen Kataloge der Stufen davor,
/// dadurch schlägt eine fehlende Referenz (z. B. eine unbekannte Textur) mit
/// derselben klaren Fehlermeldung wie bisher fehl.
/// <para>
/// Die Reihenfolge innerhalb einer Stufe ist ordinal deterministisch — sie
/// definiert die ID-Vergabe (first come first serve, ohne Bevorzugung einzelner
/// Erweiterungen). Ein späteres Load-Order-System würde ausschließlich die
/// Sortierung hier ersetzen; ein Block-Overwriting-System die Duplicate-Prüfung
/// in <see cref="Palette{TDef}.Add"/>.
/// </para>
/// </summary>
public static class ContentLoader
{
    /// <summary>
    /// Lädt Blöcke (und ihren Texturkatalog) aus der gegebenen Quelle in eine
    /// unveränderliche <see cref="BlockRegistry"/>.
    /// </summary>
    /// <param name="source">Die Content-Quelle.</param>
    /// <returns>Die fertige, unveränderliche Registry.</returns>
    /// <exception cref="InvalidDataException">Wird bei doppelten Namen, unbekannten Texturen oder ungültigem JSON ausgelöst.</exception>
    /// <exception cref="InvalidDataException">Wird bei doppelten Namen, unbekannten Texturen oder ungültigem JSON ausgelöst.</exception>
    /// <exception cref="InvalidOperationException">Wird ausgelöst, wenn die erforderlichen Core-Blöcke fehlen.</exception>
    public static BlockRegistry LoadBlocks(IContentSource source)
    {
        TextureCatalog textures = LoadTextureCatalog(source);

        var builder = new BlockRegistryBuilder()
            .UseTextures(textures)
            .ReserveAir();

        foreach (ContentEntry entry in Sort(source.Enumerate(ContentKind.Block)))
        {
            BlockSpec spec = BlockJsonParser.Parse(entry.ReadText(), entry.SourcePath);
            builder.AddBlock(entry.Id, spec, entry.SourcePath);
        }

        BlockRegistry registry = builder.Build();

        // Validiere, dass die erforderlichen Core-Blöcke vorhanden sind
        ValidateCoreBlocks(registry);

        return registry;
    }

    /// <summary>
    /// Stellt sicher, dass die für die Terrain-Generierung erforderlichen Core-Blöcke
    /// in der Registry vorhanden sind.
    /// </summary>
    private static void ValidateCoreBlocks(BlockRegistry registry)
    {
        string[] required = { "core:dirt", "core:grass", "core:snow", "core:stone" };
        var missing = new System.Collections.Generic.List<string>();

        foreach (string name in required)
        {
            try
            {
                registry.GetBlockId(name);
            }
            catch (KeyNotFoundException)
            {
                missing.Add(name);
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidDataException(
                $"Fehlende erforderliche Core-Blöcke: {string.Join(", ", missing)}. " +
                $"Ohne diese Blöcke kann die Terrain-Generierung nicht funktionieren. " +
                $"Stellen Sie sicher, dass Content/Core/Blocks/*.json vorhanden ist.");
        }
    }

    /// <summary>
    /// Baut den Texturkatalog aus der gegebenen Quelle: Ressourcenname → Layer-Index.
    /// Doppelte Textur-IDs führen zu einer <see cref="InvalidDataException"/>,
    /// die beide kollidierenden Dateien nennt.
    /// </summary>
    /// <param name="source">Die Content-Quelle.</param>
    /// <returns>Der CPU-seitige Texturkatalog (ohne GL).</returns>
    public static TextureCatalog LoadTextureCatalog(IContentSource source)
    {
        var entries = Sort(source.Enumerate(ContentKind.Texture))
            .Select(entry => (entry.Id, entry.SourcePath));

        return new TextureCatalog(entries);
    }

    /// <summary>
    /// Sortiert Einträge ordinal nach Ressourcenname — deterministisch, unabhängig
    /// von der Reihenfolge, in der das Dateisystem sie liefert.
    /// </summary>
    private static IOrderedEnumerable<ContentEntry> Sort(IEnumerable<ContentEntry> entries)
    {
        return entries.OrderBy(entry => entry.Id.FullName, StringComparer.Ordinal);
    }
}
