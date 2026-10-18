namespace Basics.Game.Content.Registries;

/// <summary>
/// CPU-seitiger Katalog aller gefundenen Texturen: Ressourcenname → Layer-Index
/// (Index in der GL Texture2DArray), plus die sortierte Reihenfolge der Dateipfade.
/// Berührt nie OpenGL — der Renderer baut das GL-Texturarray separat
/// (TextureArrayBuilder) aus diesem Katalog.
/// </summary>
public sealed class TextureCatalog
{
    private readonly Dictionary<string, ushort> _layerByFullName = new(StringComparer.Ordinal);
    private readonly List<(ResourceId Id, string FullPath)> _ordered = new();

    /// <summary>
    /// Erstellt den Katalog aus den gegebenen Textur-Einträgen. Die Reihenfolge der
    /// Eingabe definiert die Layer-Indizes (der ContentLoader sortiert ordinal deterministisch).
    /// Doppelte Textur-IDs (z. B. gleicher Dateiname in Unterordnern) führen zu einer
    /// <see cref="InvalidDataException"/>, die beide kollidierenden Dateien nennt —
    /// damit keine Textur stillschweigend als unbenutzter Layer endet.
    /// </summary>
    /// <param name="entries">Die Textur-Einträge in deterministischer Reihenfolge.</param>
    public TextureCatalog(IEnumerable<(ResourceId Id, string FullPath)> entries)
    {
        var pathByFullName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((ResourceId id, string fullPath) in entries)
        {
            if (_layerByFullName.ContainsKey(id.FullName))
            {
                throw new InvalidDataException(
                    $"Doppelte Textur-ID '{id.FullName}': {fullPath} kollidiert mit {pathByFullName[id.FullName]}.");
            }

            _layerByFullName[id.FullName] = (ushort)_ordered.Count;
            _ordered.Add((id, fullPath));
            pathByFullName[id.FullName] = fullPath;
        }
    }

    /// <summary>Anzahl der Texturen im Katalog (= Anzahl der GL-Layer).</summary>
    public int Count => _ordered.Count;

    /// <summary>
    /// Die Einträge in Layer-Reihenfolge (Index = Layer-Index).
    /// Der Renderer baut daraus das GL-Texturarray. Gibt eine Momentaufnahme zurück,
    /// damit Aufrufer die interne Liste nicht verändern können.
    /// </summary>
    public IReadOnlyList<(ResourceId Id, string FullPath)> OrderedEntries => _ordered.ToArray();

    /// <summary>
    /// Gibt den Layer-Index für eine Textur zurück.
    /// </summary>
    /// <param name="id">Die Textur-ID.</param>
    /// <returns>Der Layer-Index.</returns>
    /// <exception cref="KeyNotFoundException">Wird ausgelöst, wenn die Textur nicht im Katalog ist.</exception>
    public ushort LayerOf(ResourceId id)
    {
        if (!TryLayerOf(id, out ushort layer))
        {
            throw new KeyNotFoundException($"Unbekannte Textur '{id.FullName}'.");
        }

        return layer;
    }

    /// <summary>
    /// Versucht, den Layer-Index für eine Textur zu ermitteln.
    /// </summary>
    public bool TryLayerOf(ResourceId id, out ushort layer)
    {
        return _layerByFullName.TryGetValue(id.FullName, out layer);
    }
}
