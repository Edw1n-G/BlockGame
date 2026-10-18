namespace Basics.Game.Content.Registries;

/// <summary>
/// Index für Tags einer Registry: normalisierter Tag → Block-IDs.
/// Pro Registry-Typ gibt es einen eigenen Index, damit tag:dirt auf einem
/// Block und einem Item sich nicht vermischen.
/// </summary>
public sealed class TagIndex
{
    private readonly Dictionary<string, HashSet<ushort>> _idsByTag = new(StringComparer.Ordinal);

    /// <summary>
    /// Fügt eine Block-ID zu einem (bereits normalisierten) Tag hinzu.
    /// </summary>
    public void Add(ushort id, string normalizedTag)
    {
        if (!_idsByTag.TryGetValue(normalizedTag, out HashSet<ushort>? ids))
        {
            ids = new HashSet<ushort>();
            _idsByTag[normalizedTag] = ids;
        }

        ids.Add(id);
    }

    /// <summary>
    /// Gibt an, ob eine Block-ID einen Tag besitzt. Der Eingabe-Tag wird normalisiert.
    /// </summary>
    public bool Has(ushort id, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return _idsByTag.TryGetValue(Normalize(tag), out HashSet<ushort>? ids) && ids.Contains(id);
    }

    /// <summary>
    /// Gibt alle Block-IDs mit einem Tag als Snapshot zurück.
    /// Der Eingabe-Tag wird normalisiert; bei leerem/leerem Ergebnis eine leere Sammlung.
    /// </summary>
    public IReadOnlyCollection<ushort> Get(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || !_idsByTag.TryGetValue(Normalize(tag), out HashSet<ushort>? ids))
        {
            return Array.Empty<ushort>();
        }

        return ids.ToArray(); // Snapshot, damit Aufrufer die interne Menge nicht ändern können
    }

    private static string Normalize(string tag) => tag.Trim().ToLowerInvariant();
}
