namespace Basics.Game.Content.Registries;

/// <summary>
/// Generische Palette: verwaltet die Zuordnung Name ↔ numerischer ID für einen
/// Definitions-Typ (Blöcke, später Items/Models). Die numerische ID ist ein
/// sitzungsspezifisches Handle; die kanonische Identität ist der namespaced Name.
/// <para>
/// Add weist IDs sequenziell zu (Reihenfolge der Aufrufe bestimmt die ID) und
/// wirft bei doppelten Namen eine <see cref="InvalidDataException"/>, die beide
/// kollidierenden Quellen nennt. Nach <see cref="Freeze"/> ist die Palette
/// unveränderlich — die Registry friert sie nach dem Laden ein.
/// </para>
/// </summary>
/// <typeparam name="TDef">Der Typ der Definitionen (z. B. BlockDefinition).</typeparam>
public sealed class Palette<TDef>
{
    private readonly List<TDef> _definitions = new();
    private readonly List<ResourceId> _ids = new();
    private readonly Dictionary<string, ushort> _idByFullName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _sourcePathByFullName = new(StringComparer.Ordinal);
    private bool _frozen;

    /// <summary>Anzahl der Einträge in der Palette.</summary>
    public int Count => _definitions.Count;

    /// <summary>
    /// Fügt eine Definition hinzu und weist ihr die nächste freie ID zu.
    /// </summary>
    /// <param name="id">Die Ressourcen-ID der Definition.</param>
    /// <param name="definition">Die Definition.</param>
    /// <param name="sourcePath">Die Quelle (Dateipfad), für aussagekräftige Fehlermeldungen.</param>
    /// <returns>Die zugewiesene ID.</returns>
    /// <exception cref="InvalidDataException">Wird bei doppeltem Namen ausgelöst (nennt beide Quellen).</exception>
    /// <exception cref="InvalidOperationException">Wird ausgelöst, wenn die Palette eingefroren ist.</exception>
    public ushort Add(ResourceId id, TDef definition, string sourcePath)
    {
        if (_frozen)
        {
            throw new InvalidOperationException(
                $"Die Palette ist eingefroren und kann nicht mehr erweitert werden: '{id.FullName}'.");
        }

        if (_idByFullName.TryGetValue(id.FullName, out ushort existingId))
        {
            throw new InvalidDataException(
                $"Doppelter Ressourcenname '{id.FullName}': {sourcePath} kollidiert mit {_sourcePathByFullName[id.FullName]}.");
        }

        if (_definitions.Count >= ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"Zu viele Einträge definiert ({_definitions.Count}): Das ushort-ID-Limit ist erreicht.");
        }

        ushort newId = (ushort)_definitions.Count;
        _definitions.Add(definition);
        _ids.Add(id);
        _idByFullName[id.FullName] = newId;
        _sourcePathByFullName[id.FullName] = sourcePath;
        return newId;
    }

    /// <summary>
    /// Gibt die ID für eine Ressourcen-ID zurück.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Wird ausgelöst, wenn der Name nicht in der Palette ist.</exception>
    public ushort IdOf(ResourceId id)
    {
        if (!TryIdOf(id, out ushort result))
        {
            throw new KeyNotFoundException($"Unbekannter Ressourcenname '{id.FullName}'.");
        }

        return result;
    }

    /// <summary>Versucht, die ID für eine Ressourcen-ID zu ermitteln.</summary>
    public bool TryIdOf(ResourceId id, out ushort result)
    {
        return _idByFullName.TryGetValue(id.FullName, out result);
    }

    /// <summary>Gibt die Ressourcen-ID für eine numerische ID zurück.</summary>
    public ResourceId NameOf(ushort id)
    {
        return _ids[id];
    }

    /// <summary>Gibt die Definition für eine numerische ID zurück.</summary>
    public TDef this[ushort id] => _definitions[id];

    /// <summary>
    /// Exportiert die Palette als Namensliste (Index = ID), z. B. für Saves/Netzwerk.
    /// </summary>
    public string[] Export()
    {
        var names = new string[_ids.Count];
        for (int i = 0; i < _ids.Count; i++)
        {
            names[i] = _ids[i].FullName;
        }

        return names;
    }

    /// <summary>
    /// Friert die Palette ein: danach wirft <see cref="Add"/>. Der Builder ruft
    /// dies nach dem Laden auf; die Registry hält die Palette danach nur lesend.
    /// </summary>
    public void Freeze()
    {
        _frozen = true;
    }
}
