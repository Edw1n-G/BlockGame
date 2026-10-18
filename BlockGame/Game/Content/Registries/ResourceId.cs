namespace Basics.Game.Content.Registries;

/// <summary>
/// Werttyp für einen normalisierten Ressourcen-Bezeichner "namespace:name" (z. B. "core:stone").
/// Parst, validiert und lowercased die Zeichenkette genau einmal — String-Normalisierung
/// verteilt sich danach nicht mehr über den Code.
/// Wird für Blöcke, Texturen und später Items/Models verwendet; falsch typisierte
/// Bezeichner werden damit zur Compile-Zeit unterscheidbar.
/// <para>
/// Ein nicht initialisierter <c>default(ResourceId)</c> ist ungültig — alle Member werfen
/// oder geben leere Strings zurück. Verwenden Sie <see cref="IsDefault"/>, um diesen Zustand
/// zu prüfen, bevor Sie auf <see cref="FullName"/>, <see cref="Namespace"/> oder
/// <see cref="Name"/> zugreifen.
/// </para>
/// </summary>
public readonly struct ResourceId : IEquatable<ResourceId>
{
    /// <summary>
    /// Der interne Name für Luft — ein hartes Invariant: Luft ist immer ID 0.
    /// </summary>
    public const string AirName = "core:air";

    private readonly string? _fullName;

    private ResourceId(string fullName)
    {
        _fullName = fullName;
    }

    /// <summary>Gibt an, ob diese Instanz ein nicht initialisierter Default-Wert ist.</summary>
    public bool IsDefault => string.IsNullOrEmpty(_fullName);

    /// <summary>Der normalisierte vollständige Name ("namespace:name", kleingeschrieben).</summary>
    /// <returns>Der Name oder <c>string.Empty</c>, wenn <see cref="IsDefault"/> wahr ist.</returns>
    public string FullName => _fullName ?? string.Empty;

    /// <summary>Der Namespace-Teil vor dem Doppelpunkt.</summary>
    /// <exception cref="InvalidOperationException">Wird ausgelöst, wenn <see cref="IsDefault"/> wahr ist.</exception>
    public string Namespace
    {
        get
        {
            if (string.IsNullOrEmpty(_fullName))
            {
                throw new InvalidOperationException("ResourceId ist nicht initialisiert.");
            }

            return _fullName[.._fullName.IndexOf(':')];
        }
    }

    /// <summary>Der Name-Teil nach dem Doppelpunkt.</summary>
    /// <exception cref="InvalidOperationException">Wird ausgelöst, wenn <see cref="IsDefault"/> wahr ist.</exception>
    public string Name
    {
        get
        {
            if (string.IsNullOrEmpty(_fullName))
            {
                throw new InvalidOperationException("ResourceId ist nicht initialisiert.");
            }

            return _fullName[(_fullName.IndexOf(':') + 1)..];
        }
    }

    /// <summary>
    /// Parst einen Bezeichner und normalisiert ihn (Trim + Kleinbuchstaben).
    /// </summary>
    /// <param name="value">Die zu parsende Zeichenkette ("namespace:name").</param>
    /// <param name="sourceDescription">Optionale Quelle (Dateipfad), für aussagekräftige Fehlermeldungen.</param>
    /// <returns>Der geparste <see cref="ResourceId"/>.</returns>
    /// <exception cref="FormatException">Wird bei fehlendem oder leerem Namespace/Name oder ungültigen Zeichen ausgelöst.</exception>
    public static ResourceId Parse(string value, string? sourceDescription = null)
    {
        if (TryParse(value, out ResourceId id))
        {
            return id;
        }

        string source = sourceDescription is null ? string.Empty : $" (Quelle: {sourceDescription})";
        throw new FormatException(
            $"Ungültiger Ressourcen-Bezeichner '{value}'{source}. " +
            $"Erwartetes Format: 'namespace:name' mit nicht-leerem Namespace und Namen " +
            $"(nur Kleinbuchstaben, Ziffern, Unterstrich, Bindestrich, Punkt).");
    }

    /// <summary>
    /// Versucht, einen Bezeichner zu parsen und zu normalisieren.
    /// Nur Kleinbuchstaben, Ziffern und die Zeichen _ - . sind erlaubt.
    /// </summary>
    public static bool TryParse(string? value, out ResourceId id)
    {
        id = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToLowerInvariant();
        int separator = normalized.IndexOf(':');
        if (separator <= 0 || separator == normalized.Length - 1 || normalized.IndexOf(':', separator + 1) >= 0)
        {
            return false;
        }

        string ns = normalized[..separator];
        string name = normalized[(separator + 1)..];

        // Nur erlaubte Zeichen: a-z, 0-9, _ - .
        foreach (char c in ns)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.'))
            {
                return false;
            }
        }

        foreach (char c in name)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.'))
            {
                return false;
            }
        }

        id = new ResourceId(normalized);
        return true;
    }

    public bool Equals(ResourceId other) => string.Equals(_fullName, other._fullName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ResourceId other && Equals(other);

    public override int GetHashCode() => string.IsNullOrEmpty(_fullName) ? 0 : _fullName.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => _fullName ?? string.Empty;

    public static bool operator ==(ResourceId left, ResourceId right) => left.Equals(right);

    public static bool operator !=(ResourceId left, ResourceId right) => !left.Equals(right);
}
