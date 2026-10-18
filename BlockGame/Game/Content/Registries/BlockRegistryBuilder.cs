namespace Basics.Game.Content.Registries;

/// <summary>
/// Sammelt Block-Spezifikationen, validiert sie und baut daraus die unveränderliche
/// <see cref="BlockRegistry"/>. Es gibt keinen "halb-gefüllten" Zustand: Fehler
/// (doppelte Namen, unbekannte Texturen) werfen während des Aufbaus, und erst nach
/// vollständigem Erfolg entsteht eine Registry.
/// <para>
/// Zukünftige Systeme ersetzen hier ausschließlich einzelne Schritte:
/// ein Load-Order-System ändert die ID-Zuweisung (derzeit: Reihenfolge der Aufrufe
/// in <c>ContentLoader</c>), ein Block-Overwriting-System ersetzt die
/// Duplicate-Prüfung in <see cref="Palette{TDef}.Add"/>.
/// </para>
/// </summary>
public sealed class BlockRegistryBuilder
{
    private const int FaceCount = 6;

    private readonly Palette<BlockDefinition> _palette = new();
    private readonly TagIndex _tags = new();
    private TextureCatalog? _textures;

    /// <summary>
    /// Setzt den Texturkatalog, gegen den Block-Texturen aufgelöst werden.
    /// Muss vor dem ersten <see cref="AddBlock"/> aufgerufen werden.
    /// </summary>
    /// <param name="textures">Der CPU-seitige Texturkatalog.</param>
    /// <returns>Diesen Builder (fluent).</returns>
    public BlockRegistryBuilder UseTextures(TextureCatalog textures)
    {
        _textures = textures ?? throw new ArgumentNullException(nameof(textures));
        return this;
    }

    /// <summary>
    /// Reserviert Luft als festen Eintrag an ID 0 — ein hartes Invariant:
    /// Physik und Mesher testen gegen ID 0. Muss als erster Block hinzugefügt werden.
    /// </summary>
    /// <returns>Diesen Builder (fluent).</returns>
    public BlockRegistryBuilder ReserveAir()
    {
        ResourceId airId = ResourceId.Parse(ResourceId.AirName);
        _palette.Add(
            airId,
            new BlockDefinition(airId, new CubeShape(new ushort[FaceCount]), Array.Empty<string>()),
            "(interner Luft-Block)");
        _tags.Add(0, "tag:air");
        return this;
    }

    /// <summary>
    /// Fügt einen Block hinzu: löst die Textur-Ressourcennamen über den Texturkatalog
    /// auf, indiziert die Tags und weist die ID über die Palette zu.
    /// </summary>
    /// <param name="id">Die Ressourcen-ID des Blocks (aus dem Dateinamen).</param>
    /// <param name="spec">Der geparste, noch nicht aufgelöste Block.</param>
    /// <param name="sourcePath">Die Quelldatei, für aussagekräftige Fehlermeldungen.</param>
    /// <returns>Diesen Builder (fluent).</returns>
    /// <exception cref="InvalidDataException">Wird bei unbekannten Texturen ausgelöst.</exception>
    public BlockRegistryBuilder AddBlock(ResourceId id, BlockSpec spec, string sourcePath)
    {
        ushort[] faceLayers = ResolveFaceLayers(id, spec.Faces, sourcePath);
        BlockDefinition definition = new(id, new CubeShape(faceLayers), spec.Tags);
        ushort blockId = _palette.Add(id, definition, sourcePath);

        foreach (string tag in spec.Tags)
        {
            _tags.Add(blockId, tag);
        }

        return this;
    }

    /// <summary>
    /// Baut die unveränderliche Registry: friert die Palette ein und erzeugt die
    /// flachen Heißpfad-Tabellen. Danach ist der Builder leer verwertbar —
    /// für weitere Änderungen einen neuen Builder verwenden.
    /// </summary>
    /// <returns>Die fertige, unveränderliche <see cref="BlockRegistry"/>.</returns>
    /// <exception cref="InvalidOperationException">Wird ausgelöst, wenn kein Texturkatalog gesetzt wurde.</exception>
    public BlockRegistry Build()
    {
        if (_textures is null)
        {
            throw new InvalidOperationException("UseTextures() muss vor Build() aufgerufen werden.");
        }

        _palette.Freeze();
        return new BlockRegistry(_palette, _tags, _textures);
    }

    private ushort[] ResolveFaceLayers(ResourceId blockId, BlockTextureFaces? faces, string sourcePath)
    {
        if (faces is null)
        {
            return new ushort[FaceCount]; // Fallback-Block ohne Texturen: alle Layer 0
        }

        return new ushort[]
        {
            ResolveTexture(faces.Top ?? faces.Sides, blockId, "top", sourcePath),
            ResolveTexture(faces.Bottom ?? faces.Sides, blockId, "bottom", sourcePath),
            ResolveTexture(faces.Front ?? faces.Sides, blockId, "front", sourcePath),
            ResolveTexture(faces.Back ?? faces.Sides, blockId, "back", sourcePath),
            ResolveTexture(faces.Left ?? faces.Sides, blockId, "left", sourcePath),
            ResolveTexture(faces.Right ?? faces.Sides, blockId, "right", sourcePath),
        };
    }

    private ushort ResolveTexture(string? textureName, ResourceId blockId, string faceName, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(textureName))
        {
            throw new InvalidDataException(
                $"Block '{blockId.FullName}' hat keine Textur für Face '{faceName}': {sourcePath}");
        }

        ResourceId textureId = ResourceId.Parse(textureName, sourcePath);
        if (_textures!.TryLayerOf(textureId, out ushort layer))
        {
            return layer;
        }

        throw new InvalidDataException(
            $"Unbekannte Textur '{textureName}' für Block '{blockId.FullName}' (Face '{faceName}'): {sourcePath}");
    }
}
